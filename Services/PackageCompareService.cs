using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Quick_Sab.Models;

namespace Quick_Sab.Services
{
    /// <summary>One package of the source folder compared with its counterpart in the target folder.</summary>
    public class PackageComparison
    {
        /// <summary>Package name without the trailing version.</summary>
        public string BaseName { get; set; }

        public bool IsDirectory { get; set; }

        /// <summary>Entry name in the source folder (file or directory).</summary>
        public string SourceName { get; set; }
        public string SourcePath { get; set; }
        public string SourceVersion { get; set; }

        /// <summary>Matching entry in the target folder; null when the package is missing there.</summary>
        public string TargetName { get; set; }
        public string TargetPath { get; set; }
        public string TargetVersion { get; set; }

        public bool IsUpToDate =>
            TargetName != null && string.Equals(SourceName, TargetName, StringComparison.OrdinalIgnoreCase);

        public string Status =>
            TargetName == null ? "Missing in target" : (IsUpToDate ? "Up to date" : "Different version");
    }

    /// <summary>
    /// Compares packages (files or folders whose name ends with a version made of digits and '.')
    /// between the configured source and target folders.
    /// </summary>
    public static class PackageCompareService
    {
        /// <summary>
        /// Trailing version in a name: starts with a digit right after a separator (. - _ space)
        /// and takes everything up to the end of the name, e.g. "package_5.2_up" -> "5.2_up", "MyLib.1.2.3-beta2" -> "1.2.3-beta2".
        /// </summary>
        private static readonly Regex VersionSuffix = new Regex(@"(?<=^|[.\-_ ])\d[0-9A-Za-z.\-_ ]*$", RegexOptions.Compiled);

        private sealed class Entry
        {
            public string Name;
            public string FullPath;
            public bool IsDir;
            public string Base;
            public string Version;
            public string Ext;
            public string Key => (Base + "|" + Ext + "|" + (IsDir ? "d" : "f")).ToLowerInvariant();
        }

        /// <summary>Compiles the configured patterns; throws ArgumentException with a clear message on an invalid regex.</summary>
        public static List<Regex> CompilePatterns(PackageCompareConfig cfg)
        {
            var list = new List<Regex>();
            if (cfg?.Patterns == null) return list;
            foreach (var p in cfg.Patterns)
            {
                if (string.IsNullOrWhiteSpace(p?.Pattern)) continue;
                try
                {
                    list.Add(new Regex(p.Pattern, RegexOptions.IgnoreCase));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException("Invalid regex \"" + p.Pattern + "\": " + ex.Message);
                }
            }
            return list;
        }

        /// <summary>Resolves {{variables}} and %ENV% in a configured path.</summary>
        public static string ResolvePath(string path)
        {
            return Environment.ExpandEnvironmentVariables(VariableResolver.Resolve(path ?? "", null)).Trim();
        }

        /// <summary>Compares the source folder against the target folder. One row per source package.</summary>
        public static List<PackageComparison> Compare(PackageCompareConfig cfg)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SourcePath))
                throw new InvalidOperationException("Set the source path in Settings -> Packages.");

            var sourcePath = ResolvePath(cfg.SourcePath);
            var targetPath = ResolvePath(cfg.TargetPath);
            if (!Directory.Exists(sourcePath))
                throw new InvalidOperationException("Source path not found: " + sourcePath);

            var regexes = CompilePatterns(cfg);
            var source = Scan(sourcePath, regexes);
            var target = !string.IsNullOrWhiteSpace(targetPath) && Directory.Exists(targetPath)
                ? Scan(targetPath, regexes)
                : new Dictionary<string, Entry>();

            return source.Values
                .OrderBy(e => e.Base, StringComparer.OrdinalIgnoreCase)
                .Select(src =>
                {
                    target.TryGetValue(src.Key, out var tgt);
                    return new PackageComparison
                    {
                        BaseName = src.Base,
                        IsDirectory = src.IsDir,
                        SourceName = src.Name,
                        SourcePath = src.FullPath,
                        SourceVersion = src.Version,
                        TargetName = tgt?.Name,
                        TargetPath = tgt?.FullPath,
                        TargetVersion = tgt?.Version
                    };
                })
                .ToList();
        }

        /// <summary>Updates the given packages in parallel. Returns the errors ("name: message").</summary>
        public static async Task<List<string>> UpdatePackagesAsync(
            IEnumerable<PackageComparison> items, string targetRoot, Action<PackageComparison> onDone = null)
        {
            var errors = new ConcurrentBag<string>();
            var resolvedRoot = ResolvePath(targetRoot);
            await Task.WhenAll(items.Select(item => Task.Run(() =>
            {
                try
                {
                    UpdatePackage(item, resolvedRoot);
                    onDone?.Invoke(item);
                }
                catch (Exception ex)
                {
                    errors.Add(item.SourceName + ": " + ex.Message);
                }
            })));
            return errors.ToList();
        }

        /// <summary>
        /// Updates one package: deletes the old target entry (if any) and copies the source entry
        /// into the target folder, creating the target folder when it does not exist yet.
        /// </summary>
        public static void UpdatePackage(PackageComparison item, string targetRoot)
        {
            targetRoot = ResolvePath(targetRoot);
            if (string.IsNullOrWhiteSpace(targetRoot))
                throw new InvalidOperationException("Set the target path in Settings -> Packages.");

            Directory.CreateDirectory(targetRoot);

            if (item.TargetPath != null)
            {
                if (Directory.Exists(item.TargetPath)) Directory.Delete(item.TargetPath, true);
                else if (File.Exists(item.TargetPath)) File.Delete(item.TargetPath);
            }

            var dest = Path.Combine(targetRoot, item.SourceName);
            if (item.IsDirectory) CopyDirectory(item.SourcePath, dest);
            else File.Copy(item.SourcePath, dest, true);
        }

        private static Dictionary<string, Entry> Scan(string root, List<Regex> regexes)
        {
            var result = new Dictionary<string, Entry>();
            foreach (var path in Directory.EnumerateFileSystemEntries(root))
            {
                var isDir = Directory.Exists(path);
                var name = Path.GetFileName(path);
                if (regexes.Count > 0 && !regexes.Any(r => r.IsMatch(name))) continue;

                var e = Parse(name, path, isDir);
                // Several versions of the same package: keep the highest one.
                if (!result.TryGetValue(e.Key, out var existing) || CompareVersions(e.Version, existing.Version) > 0)
                    result[e.Key] = e;
            }
            return result;
        }

        private static Entry Parse(string name, string fullPath, bool isDir)
        {
            var ext = isDir ? "" : Path.GetExtension(name);
            var stem = isDir ? name : Path.GetFileNameWithoutExtension(name);
            var m = VersionSuffix.Match(stem);
            var version = m.Success ? m.Value : "";
            var baseName = m.Success ? stem.Substring(0, m.Index).TrimEnd('.', '-', '_', ' ') : stem;
            return new Entry { Name = name, FullPath = fullPath, IsDir = isDir, Base = baseName, Version = version, Ext = ext };
        }

        /// <summary>
        /// Segment-by-segment version comparison ("1.10" &gt; "1.9"): numeric when both segments
        /// are numbers, case-insensitive text otherwise ("1.2.3b" &gt; "1.2.3a").
        /// </summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = (a ?? "").Split(new[] { '.', '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var pb = (b ?? "").Split(new[] { '.', '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                var sa = i < pa.Length ? pa[i] : "";
                var sb = i < pb.Length ? pb[i] : "";
                if (long.TryParse(sa, out var va) && long.TryParse(sb, out var vb))
                {
                    if (va != vb) return va < vb ? -1 : 1;
                }
                else
                {
                    var c = string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
                    if (c != 0) return c < 0 ? -1 : 1;
                }
            }
            return 0;
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            Parallel.ForEach(Directory.EnumerateFiles(source), file =>
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true));
            Parallel.ForEach(Directory.EnumerateDirectories(source), dir =>
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir))));
        }
    }
}
