using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Quick_Sab.Models
{
    /// <summary>Action types supported by a configuration entry.</summary>
    public enum ActionType
    {
        /// <summary>Opens a folder / network share in Explorer.</summary>
        Share,

        /// <summary>Opens a URL in the default browser.</summary>
        Web,

        /// <summary>Runs a command through cmd.exe.</summary>
        Command
    }

    public class NotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>A key / value / action type entry.</summary>
    public class ActionItem : NotifyBase
    {
        private string _key = "";
        private string _value = "";
        private ActionType _type = ActionType.Web;
        private string _description = "";
        private string _workingDirectory = "";
        private bool _keepWindowOpen;

        /// <summary>Key displayed and used by the filter.</summary>
        public string Key { get => _key; set => Set(ref _key, value); }

        /// <summary>Value: UNC path, URL or command line. May contain {{variables}}.</summary>
        public string Value { get => _value; set => Set(ref _value, value); }

        public ActionType Type { get => _type; set => Set(ref _type, value); }

        public string Description { get => _description; set => Set(ref _description, value); }

        /// <summary>Working directory for Command entries (supports {{variables}}).</summary>
        public string WorkingDirectory { get => _workingDirectory; set => Set(ref _workingDirectory, value); }

        /// <summary>Command: keep the cmd window open after execution (cmd /k).</summary>
        public bool KeepWindowOpen { get => _keepWindowOpen; set => Set(ref _keepWindowOpen, value); }
    }

    /// <summary>Local git repository, usable as {{Name}} / {{git_path_N}} in values.</summary>
    public class GitRepo : NotifyBase
    {
        private string _name = "";
        private string _path = "";

        public string Name { get => _name; set => Set(ref _name, value); }
        public string Path { get => _path; set => Set(ref _path, value); }
    }

    /// <summary>Free variable {{Name}} -> Value.</summary>
    public class VariableEntry : NotifyBase
    {
        private string _name = "";
        private string _value = "";

        public string Name { get => _name; set => Set(ref _name, value); }
        public string Value { get => _value; set => Set(ref _value, value); }
    }

    /// <summary>A named panel shown at the bottom of the launcher, holding pinned shortcut keys.</summary>
    public class PinPanel : NotifyBase
    {
        private string _name = "";
        private ObservableCollection<string> _keys = new ObservableCollection<string>();

        public string Name { get => _name; set => Set(ref _name, value); }

        /// <summary>Keys of the pinned ActionItems, in display order.</summary>
        public ObservableCollection<string> Keys
        {
            get => _keys;
            set => Set(ref _keys, value ?? new ObservableCollection<string>());
        }

        /// <summary>Read-only summary used by the configuration grid.</summary>
        [JsonIgnore]
        public string KeysDisplay => string.Join(", ", Keys);
    }

    /// <summary>Global keyboard shortcut (2 or 3 keys: modifiers + key).</summary>
    public class HotkeyConfig
    {
        public bool Ctrl { get; set; } = true;
        public bool Alt { get; set; } = true;
        public bool Shift { get; set; }
        public bool Win { get; set; }

        /// <summary>Name of a System.Windows.Input.Key value (e.g. Space, Q, F12).</summary>
        public string Key { get; set; } = "Space";

        public override string ToString()
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(string.IsNullOrWhiteSpace(Key) ? "?" : Key);
            return string.Join(" + ", parts);
        }
    }

    public class AppConfig
    {
        public HotkeyConfig Hotkey { get; set; } = new HotkeyConfig();

        /// <summary>Hide the window right after an action is executed.</summary>
        public bool HideAfterAction { get; set; } = true;

        /// <summary>Hide the window as soon as it loses focus (launcher-like behaviour).</summary>
        public bool HideOnFocusLost { get; set; } = true;

        /// <summary>Hex colour per action type: key = Share / Web / Command.</summary>
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>
        {
            ["Share"] = "#F59E0B",
            ["Web"] = "#e41414",
            ["Command"] = "#3B82F6"
        };

        public ObservableCollection<GitRepo> GitRepos { get; set; } = new ObservableCollection<GitRepo>();

        public ObservableCollection<VariableEntry> Variables { get; set; } = new ObservableCollection<VariableEntry>();

        /// <summary>Named panels of pinned shortcuts shown at the bottom of the launcher.</summary>
        public ObservableCollection<PinPanel> Panels { get; set; } = new ObservableCollection<PinPanel>();

        /// <summary>Legacy flat list of pinned keys (older config files). Migrated into a panel on load.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ObservableCollection<string> Pinned { get; set; }

        public ObservableCollection<ActionItem> Items { get; set; } = new ObservableCollection<ActionItem>();
    }
}
