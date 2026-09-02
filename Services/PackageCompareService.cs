using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        /// <summary>Trailing version in a name: digits and dots, e.g. "MyLib.1.2.3" -> "1.2.3".</summary>
        private static readonly Regex VersionSuffix = new Regex(@"\d[\d.]*$", RegexOptions.Compiled);

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

        /// <summary>Compares the source folder against the target folder. One row per source package.</summary>
        public static List<PackageComparison> Compare(PackageCompareConfig cfg)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SourcePath))
                throw new InvalidOperationException("Set the source path in Settings -> Packages.");
            if (!Directory.Exists(cfg.SourcePath))
                throw new InvalidOperationException("Source path not found: " + cfg.SourcePath);

            var regexes = CompilePatterns(cfg);
            var source = Scan(cfg.SourcePath, regexes);
            var target = !string.IsNullOrWhiteSpace(cfg.TargetPath) && Directory.Exists(cfg.TargetPath)
                ? Scan(cfg.TargetPath, regexes)
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

        /// <summary>
        /// Updates one package: deletes the old target entry (if any) and copies the source entry
        /// into the target folder, creating the target folder when it does not exist yet.
        /// </summary>
        public static void UpdatePackage(PackageComparison item, string targetRoot)
        {
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

        /// <summary>Numeric segment-by-segment version comparison ("1.10" &gt; "1.9").</summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = (a ?? "").Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var pb = (b ?? "").Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                long va = 0, vb = 0;
                if (i < pa.Length) long.TryParse(pa[i], out va);
                if (i < pb.Length) long.TryParse(pb[i], out vb);
                if (va != vb) return va < vb ? -1 : 1;
            }
            return 0;
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.EnumerateFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.EnumerateDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
