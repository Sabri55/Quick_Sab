using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Quick_Sab.Models;
using Quick_Sab.Services;
using WinForms = System.Windows.Forms;

namespace Quick_Sab.Views
{
    public partial class ConfigWindow : Window
    {
        private class ColorEntry : NotifyBase
        {
            private string _hex;
            public string Name { get; set; }
            public string Hex { get => _hex; set => Set(ref _hex, value); }
        }

        private readonly AppConfig _cfg;
        private readonly ObservableCollection<ColorEntry> _colors = new ObservableCollection<ColorEntry>();

        public ConfigWindow()
        {
            InitializeComponent();

            // Deep copy: "Cancel" never touches the live configuration.
            _cfg = Clone(ConfigService.Current);

            ItemsGrid.ItemsSource = _cfg.Items;
            PanelsGrid.ItemsSource = _cfg.Panels;
            ReposGrid.ItemsSource = _cfg.GitRepos;
            VarsGrid.ItemsSource = _cfg.Variables;
            ReposGrid.LoadingRow += (s, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();

            CtrlBox.IsChecked = _cfg.Hotkey.Ctrl;
            AltBox.IsChecked = _cfg.Hotkey.Alt;
            ShiftBox.IsChecked = _cfg.Hotkey.Shift;
            WinBox.IsChecked = _cfg.Hotkey.Win;
            KeyBox.Text = _cfg.Hotkey.Key;
            UpdateHotkeyPreview();

            HideAfterActionBox.IsChecked = _cfg.HideAfterAction;
            HideOnFocusLostBox.IsChecked = _cfg.HideOnFocusLost;
            ConfigPathBox.Text = ConfigService.ConfigPath;

            foreach (var name in new[] { "Share", "Web", "Command" })
            {
                _cfg.Colors.TryGetValue(name, out var hex);
                _colors.Add(new ColorEntry { Name = name, Hex = hex ?? "#808080" });
            }
            ColorsList.ItemsSource = _colors;
        }

        private static AppConfig Clone(AppConfig source)
        {
            var opts = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
            var json = JsonSerializer.Serialize(source, opts);
            var copy = JsonSerializer.Deserialize<AppConfig>(json, opts);
            ConfigService.Normalize(copy);
            return copy;
        }

        // ---------------- Actions ----------------

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            var item = new ActionItem { Key = "new_key", Type = ActionType.Web };
            _cfg.Items.Add(item);
            ItemsGrid.SelectedItem = item;
            ItemsGrid.ScrollIntoView(item);
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsGrid.SelectedItem is ActionItem item) _cfg.Items.Remove(item);
        }

        private void MoveItemUp_Click(object sender, RoutedEventArgs e) => Move(_cfg.Items, ItemsGrid, -1);
        private void MoveItemDown_Click(object sender, RoutedEventArgs e) => Move(_cfg.Items, ItemsGrid, 1);

        private static void Move<T>(ObservableCollection<T> list, DataGrid grid, int delta) where T : class
        {
            if (!(grid.SelectedItem is T item)) return;
            var idx = list.IndexOf(item);
            var target = idx + delta;
            if (idx < 0 || target < 0 || target >= list.Count) return;
            list.Move(idx, target);
            grid.SelectedItem = item;
        }

        // ---------------- Panels ----------------

        private void AddPanel_Click(object sender, RoutedEventArgs e)
        {
            var panel = new PinPanel { Name = "New panel" };
            _cfg.Panels.Add(panel);
            PanelsGrid.SelectedItem = panel;
            PanelsGrid.ScrollIntoView(panel);
        }

        private void RemovePanel_Click(object sender, RoutedEventArgs e)
        {
            if (PanelsGrid.SelectedItem is PinPanel p) _cfg.Panels.Remove(p);
        }

        private void MovePanelUp_Click(object sender, RoutedEventArgs e) => Move(_cfg.Panels, PanelsGrid, -1);
        private void MovePanelDown_Click(object sender, RoutedEventArgs e) => Move(_cfg.Panels, PanelsGrid, 1);

        // ---------------- Git ----------------

        private void AddRepo_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new WinForms.FolderBrowserDialog { Description = "Select the git repository folder" })
            {
                if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
                var path = dlg.SelectedPath;
                var repo = new GitRepo { Name = Path.GetFileName(path.TrimEnd('\\', '/')), Path = path };
                _cfg.GitRepos.Add(repo);
                ReposGrid.SelectedItem = repo;
            }
        }

        private void RemoveRepo_Click(object sender, RoutedEventArgs e)
        {
            if (ReposGrid.SelectedItem is GitRepo r) _cfg.GitRepos.Remove(r);
        }

        private void MoveRepoUp_Click(object sender, RoutedEventArgs e)
        {
            Move(_cfg.GitRepos, ReposGrid, -1);
            ReposGrid.Items.Refresh();
        }

        private void MoveRepoDown_Click(object sender, RoutedEventArgs e)
        {
            Move(_cfg.GitRepos, ReposGrid, 1);
            ReposGrid.Items.Refresh();
        }

        // ---------------- Variables ----------------

        private void AddVar_Click(object sender, RoutedEventArgs e)
        {
            var v = new VariableEntry { Name = "my_variable", Value = "" };
            _cfg.Variables.Add(v);
            VarsGrid.SelectedItem = v;
        }

        private void RemoveVar_Click(object sender, RoutedEventArgs e)
        {
            if (VarsGrid.SelectedItem is VariableEntry v) _cfg.Variables.Remove(v);
        }

        // ---------------- Hotkey ----------------

        private void Modifier_Changed(object sender, RoutedEventArgs e) => UpdateHotkeyPreview();

        private void KeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            e.Handled = true;

            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LWin:
                case Key.RWin:
                case Key.None:
                    return; // waiting for the final key
            }

            KeyBox.Text = key.ToString();
            UpdateHotkeyPreview();
        }

        private HotkeyConfig BuildHotkey()
        {
            return new HotkeyConfig
            {
                Ctrl = CtrlBox.IsChecked == true,
                Alt = AltBox.IsChecked == true,
                Shift = ShiftBox.IsChecked == true,
                Win = WinBox.IsChecked == true,
                Key = KeyBox.Text
            };
        }

        private void UpdateHotkeyPreview()
        {
            if (HotkeyPreview == null) return;
            HotkeyPreview.Text = BuildHotkey().ToString();
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(ConfigService.ConfigDirectory);
            Process.Start(new ProcessStartInfo { FileName = ConfigService.ConfigDirectory, UseShellExecute = true });
        }

        private void OpenConfigFile_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(ConfigService.ConfigPath)) ConfigService.Save(ConfigService.Current);
            Process.Start(new ProcessStartInfo { FileName = ConfigService.ConfigPath, UseShellExecute = true });
        }

        // ---------------- Colours ----------------

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is ColorEntry entry)) return;

            using (var dlg = new WinForms.ColorDialog { FullOpen = true })
            {
                try
                {
                    var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(entry.Hex);
                    dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
                }
                catch { /* invalid hex */ }

                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                    entry.Hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            }
        }

        // ---------------- Save ----------------

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            PanelsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            ReposGrid.CommitEdit(DataGridEditingUnit.Row, true);
            VarsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var hotkey = BuildHotkey();
            if (string.IsNullOrWhiteSpace(hotkey.Key))
            {
                MessageBox.Show(this, "Pick a key for the hotkey.", "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!hotkey.Ctrl && !hotkey.Alt && !hotkey.Shift && !hotkey.Win)
            {
                MessageBox.Show(this, "The hotkey must include at least one modifier (Ctrl, Alt, Shift or Win).",
                    "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var c in _colors)
            {
                try
                {
                    System.Windows.Media.ColorConverter.ConvertFromString(c.Hex);
                }
                catch
                {
                    MessageBox.Show(this, "Invalid color for " + c.Name + ": " + c.Hex, "Quick_Sab",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _cfg.Colors[c.Name] = c.Hex;
            }

            // Cleanup: empty rows, orphan pinned keys
            foreach (var empty in _cfg.Items.Where(i => string.IsNullOrWhiteSpace(i.Key)).ToList()) _cfg.Items.Remove(empty);
            foreach (var empty in _cfg.GitRepos.Where(r => string.IsNullOrWhiteSpace(r.Path)).ToList()) _cfg.GitRepos.Remove(empty);
            foreach (var empty in _cfg.Variables.Where(v => string.IsNullOrWhiteSpace(v.Name)).ToList()) _cfg.Variables.Remove(empty);
            foreach (var panel in _cfg.Panels)
            {
                if (string.IsNullOrWhiteSpace(panel.Name)) panel.Name = ConfigService.DefaultPanelName;
                foreach (var orphan in panel.Keys
                             .Where(k => !_cfg.Items.Any(i => string.Equals(i.Key, k, StringComparison.OrdinalIgnoreCase)))
                             .ToList())
                    panel.Keys.Remove(orphan);
            }

            _cfg.Hotkey = hotkey;
            _cfg.HideAfterAction = HideAfterActionBox.IsChecked == true;
            _cfg.HideOnFocusLost = HideOnFocusLostBox.IsChecked == true;

            try
            {
                ConfigService.Save(_cfg);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed:\n" + ex.Message, "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
