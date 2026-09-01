using System;
using System.Text.RegularExpressions;
using Quick_Sab.Models;

namespace Quick_Sab.Services
{
    /// <summary>
    /// Replaces {{...}} variables in a string:
    ///   {{git_path_N}}  -> path of the N-th git repository (1-based)
    ///   {{git_name_N}}  -> name of the N-th git repository
    ///   {{git:name}}    -> path of the git repository named "name"
    ///   {{name}}        -> git repository named "name" (path) or free variable "name"
    ///   {{env:VAR}}     -> environment variable
    ///   {{CurrentGitRepo}} -> path of the repository selected in the launcher list
    /// </summary>
    public static class VariableResolver
    {
        private static readonly Regex Pattern = new Regex(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);

        /// <summary>Git repository currently selected in the launcher list (set by MainWindow; may be null).</summary>
        public static GitRepo CurrentGitRepo { get; set; }

        public static string Resolve(string input, AppConfig config)
        {
            if (string.IsNullOrEmpty(input)) return input ?? "";
            config ??= ConfigService.Current;

            return Pattern.Replace(input, m =>
            {
                var name = m.Groups[1].Value;
                var resolved = ResolveOne(name, config);
                return resolved ?? m.Value; // unknown variable: left untouched
            });
        }

        private static string ResolveOne(string name, AppConfig config)
        {
            var lower = name.ToLowerInvariant();

            if (lower == "currentgitrepo")
                return CurrentGitRepo?.Path;

            if (lower.StartsWith("git_path_") && int.TryParse(name.Substring(9), out var idx))
                return idx >= 1 && idx <= config.GitRepos.Count ? config.GitRepos[idx - 1].Path : null;

            if (lower.StartsWith("git_name_") && int.TryParse(name.Substring(9), out var idx2))
                return idx2 >= 1 && idx2 <= config.GitRepos.Count ? config.GitRepos[idx2 - 1].Name : null;

            if (lower.StartsWith("git:"))
                return FindRepo(name.Substring(4), config);

            if (lower.StartsWith("env:"))
                return Environment.GetEnvironmentVariable(name.Substring(4));

            var repo = FindRepo(name, config);
            if (repo != null) return repo;

            foreach (var v in config.Variables)
            {
                if (string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
                    return v.Value;
            }

            return null;
        }

        private static string FindRepo(string name, AppConfig config)
        {
            foreach (var r in config.GitRepos)
            {
                if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                    return r.Path;
            }
            return null;
        }
    }
}
