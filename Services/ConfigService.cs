using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Quick_Sab.Models;

namespace Quick_Sab.Services
{
    /// <summary>Loads / saves config.json (stored in %AppData%\Quick_Sab).</summary>
    public static class ConfigService
    {
        public const string DefaultPanelName = "Pinned";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string ConfigDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Quick_Sab");

        public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

        public static AppConfig Current { get; private set; } = new AppConfig();

        public static event Action ConfigChanged;

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    Current = JsonSerializer.Deserialize<AppConfig>(json, Options) ?? CreateDefault();
                    if (Normalize(Current))
                        Save(Current); // persist the migrated format right away
                }
                else
                {
                    Current = CreateDefault();
                    Save(Current);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Unable to read the configuration file:\n" + ConfigPath + "\n\n" + ex.Message,
                    "Quick_Sab", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                Current = CreateDefault();
            }

            ConfigChanged?.Invoke();
            return Current;
        }

        public static void Save(AppConfig config)
        {
            Normalize(config);
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(config, Options);
            File.WriteAllText(ConfigPath, json);
            Current = config;
            ConfigChanged?.Invoke();
        }

        /// <summary>
        /// Fills missing collections and migrates the legacy "Pinned" list into a panel.
        /// Returns true when a legacy migration was performed.
        /// </summary>
        public static bool Normalize(AppConfig c)
        {
            var migrated = false;
            c.Hotkey ??= new HotkeyConfig();
            c.Colors ??= CreateDefault().Colors;
            c.GitRepos ??= new ObservableCollection<GitRepo>();
            c.CurrentGitRepo ??= "";
            c.Crypto ??= new CryptoConfig();
            c.Variables ??= new ObservableCollection<VariableEntry>();
            c.Panels ??= new ObservableCollection<PinPanel>();
            c.Items ??= new ObservableCollection<ActionItem>();

            foreach (var name in new[] { "Share", "Web", "Command" })
            {
                if (!c.Colors.ContainsKey(name))
                    c.Colors[name] = CreateDefault().Colors[name];
            }

            foreach (var p in c.Panels)
            {
                p.Keys ??= new ObservableCollection<string>();
                if (string.IsNullOrWhiteSpace(p.Name)) p.Name = DefaultPanelName;
            }

            // Legacy migration: flat "Pinned" list -> default panel.
            if (c.Pinned != null)
            {
                if (c.Pinned.Count > 0)
                {
                    var panel = new PinPanel { Name = DefaultPanelName };
                    foreach (var k in c.Pinned) panel.Keys.Add(k);
                    c.Panels.Insert(0, panel);
                    migrated = true;
                }
                c.Pinned = null;
            }

            return migrated;
        }

        private static AppConfig CreateDefault()
        {
            var c = new AppConfig();
            c.GitRepos.Add(new GitRepo { Name = "quick_sab", Path = @"C:\_Projects\Quick_Sab" });
            c.Items.Add(new ActionItem
            {
                Key = "google",
                Value = "https://www.google.com",
                Type = ActionType.Web,
                Description = "Search engine"
            });
            c.Items.Add(new ActionItem
            {
                Key = "github",
                Value = "https://github.com",
                Type = ActionType.Web
            });
            c.Items.Add(new ActionItem
            {
                Key = "temp",
                Value = @"C:\Windows\Temp",
                Type = ActionType.Share,
                Description = "Windows temp folder"
            });
            c.Items.Add(new ActionItem
            {
                Key = "projects",
                Value = @"C:\_Projects",
                Type = ActionType.Share
            });
            c.Items.Add(new ActionItem
            {
                Key = "git status",
                Value = "git status",
                Type = ActionType.Command,
                WorkingDirectory = "{{git_path_1}}",
                KeepWindowOpen = true,
                Description = "Status of the first git repository"
            });
            c.Items.Add(new ActionItem
            {
                Key = "explore repo",
                Value = "{{git_path_1}}",
                Type = ActionType.Share,
                Description = "Open the first git repository"
            });

            var favorites = new PinPanel { Name = "Favorites" };
            favorites.Keys.Add("google");
            favorites.Keys.Add("projects");
            c.Panels.Add(favorites);

            var git = new PinPanel { Name = "Git" };
            git.Keys.Add("git status");
            git.Keys.Add("explore repo");
            c.Panels.Add(git);

            return c;
        }
    }
}
