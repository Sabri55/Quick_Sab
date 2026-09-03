using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Quick_Sab.Models;
using Quick_Sab.Services;
using WinForms = System.Windows.Forms;

namespace Quick_Sab.Views
{
    public partial class MainWindow : Window
    {
        /// <summary>View model of a pinned panel: the panel plus its resolved ActionItems.</summary>
        public class PanelVm
        {
            public PinPanel Panel { get; set; }
            public string Name => Panel.Name;
            public ObservableCollection<PinVm> Items { get; } = new ObservableCollection<PinVm>();
            public string CountText => Items.Count == 1 ? "1 shortcut" : Items.Count + " shortcuts";
        }

        /// <summary>A pinned shortcut inside a panel.</summary>
        public class PinVm
        {
            public ActionItem Item { get; set; }
            public PanelVm Panel { get; set; }
        }

        private readonly ObservableCollection<ActionItem> _suggestions = new ObservableCollection<ActionItem>();
        private readonly ObservableCollection<PanelVm> _panels = new ObservableCollection<PanelVm>();

        private HotkeyManager _hotkey;
        private WinForms.NotifyIcon _tray;
        private bool _exiting;
        private bool _suppressHide;
        private bool _refreshingRepos;

        public MainWindow()
        {
            InitializeComponent();
            SuggestionList.ItemsSource = _suggestions;
            PanelsList.ItemsSource = _panels;

            SourceInitialized += (s, e) =>
            {
                _hotkey = new HotkeyManager(this, ToggleLauncher);
                ApplyHotkey();
            };

            ConfigService.ConfigChanged += OnConfigChanged;
            Deactivated += MainWindow_Deactivated;

            CreateTrayIcon();
            RefreshFromConfig();
        }

        // ------------------------------------------------------------------
        // Show / hide
        // ------------------------------------------------------------------

        public void ShowLauncher()
        {
            FilterBox.Text = "";
            UpdateSuggestions();

            if (!IsVisible) Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            FilterBox.Focus();
            Keyboard.Focus(FilterBox);
        }

        public void HideLauncher()
        {
            Hide();
        }

        private void ToggleLauncher()
        {
            if (IsVisible && IsActive)
                HideLauncher();
            else
                ShowLauncher();
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            if (_suppressHide || _exiting) return;
            if (ConfigService.Current.HideOnFocusLost)
                HideLauncher();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_exiting)
            {
                e.Cancel = true;
                HideLauncher();
                return;
            }
            base.OnClosing(e);
        }

        private void ExitApplication()
        {
            _exiting = true;
            _hotkey?.Dispose();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            Application.Current.Shutdown();
        }

        /// <summary>Runs a modal dialog without the auto-hide-on-focus-lost kicking in.</summary>
        private T RunDialog<T>(Func<T> dialog)
        {
            _suppressHide = true;
            var wasTopmost = Topmost;
            try
            {
                Topmost = false;
                return dialog();
            }
            finally
            {
                Topmost = wasTopmost;
                _suppressHide = false;
                if (IsVisible)
                {
                    Activate();
                    FilterBox.Focus();
                }
            }
        }

        // ------------------------------------------------------------------
        // Config
        // ------------------------------------------------------------------

        private void OnConfigChanged()
        {
            RefreshFromConfig();
            ApplyHotkey();
        }

        private void RefreshFromConfig()
        {
            var cfg = ConfigService.Current;
            HotkeyLabel.Text = cfg.Hotkey?.ToString() ?? "";

            _panels.Clear();
            foreach (var panel in cfg.Panels)
            {
                var vm = new PanelVm { Panel = panel };
                foreach (var key in panel.Keys)
                {
                    var item = FindByKey(key);
                    if (item != null) vm.Items.Add(new PinVm { Item = item, Panel = vm });
                }
                _panels.Add(vm);
            }

            _refreshingRepos = true;
            try
            {
                GitRepoCombo.ItemsSource = cfg.GitRepos;
                GitRepoCombo.SelectedItem = string.IsNullOrWhiteSpace(cfg.CurrentGitRepo)
                    ? null
                    : cfg.GitRepos.FirstOrDefault(
                          r => string.Equals(r.Path, cfg.CurrentGitRepo, StringComparison.OrdinalIgnoreCase))
                      ?? cfg.GitRepos.FirstOrDefault( // legacy configs stored the name
                          r => string.Equals(r.Name, cfg.CurrentGitRepo, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _refreshingRepos = false;
            }
            CurrentGitRepo = GitRepoCombo.SelectedItem as GitRepo;

            UpdateSuggestions();
        }

        private void ApplyHotkey()
        {
            if (_hotkey == null) return;
            var error = _hotkey.Register(ConfigService.Current.Hotkey);
            if (error != null)
            {
                HotkeyLabel.Text = "⚠ " + error;
                _tray?.ShowBalloonTip(4000, "Quick_Sab", error, WinForms.ToolTipIcon.Warning);
            }
        }

        /// <summary>Configured actions plus the named scripts wrapped as Script-typed actions.</summary>
        private static List<ActionItem> AllItems()
        {
            var cfg = ConfigService.Current;
            var list = new List<ActionItem>(cfg.Items);
            foreach (var s in cfg.Scripts)
            {
                if (string.IsNullOrWhiteSpace(s.Name)) continue;
                list.Add(new ActionItem
                {
                    Key = s.Name,
                    Value = s.Content,
                    Type = ActionType.Script,
                    Description = s.Description,
                    WorkingDirectory = s.WorkingDirectory,
                    KeepWindowOpen = s.KeepWindowOpen,
                    ScriptShell = s.Shell
                });
            }
            return list;
        }

        private static ActionItem FindByKey(string key)
        {
            return AllItems().FirstOrDefault(
                i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        private void Config_Click(object sender, RoutedEventArgs e)
        {
            OpenConfig();
        }

        private void OpenConfig()
        {
            RunDialog(() =>
            {
                var win = new ConfigWindow();
                if (IsVisible) win.Owner = this;
                return win.ShowDialog();
            });
        }

        private void Crypto_Click(object sender, RoutedEventArgs e)
        {
            RunDialog(() =>
            {
                var win = new CryptoWindow();
                if (IsVisible) win.Owner = this;
                return win.ShowDialog();
            });
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            OpenAbout();
        }

        private void OpenAbout()
        {
            RunDialog(() =>
            {
                var win = new AboutWindow();
                if (IsVisible) win.Owner = this;
                return win.ShowDialog();
            });
        }

        // ------------------------------------------------------------------
        // Filter / autocomplete
        // ------------------------------------------------------------------

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Placeholder.Visibility = string.IsNullOrEmpty(FilterBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            UpdateSuggestions();
        }

        private void UpdateSuggestions()
        {
            var text = (FilterBox.Text ?? "").Trim();
            var items = AllItems();

            IEnumerable<ActionItem> result;
            if (string.IsNullOrEmpty(text))
            {
                result = items;
            }
            else
            {
                // Priority: key starts with > key contains > description / value contains
                var starts = items.Where(i => (i.Key ?? "").StartsWith(text, StringComparison.OrdinalIgnoreCase));
                var contains = items.Where(i => !(i.Key ?? "").StartsWith(text, StringComparison.OrdinalIgnoreCase)
                                             && (i.Key ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
                var other = items.Where(i => (i.Key ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0
                                          && ((i.Description ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                                           || (i.Value ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
                result = starts.Concat(contains).Concat(other);
            }

            _suggestions.Clear();
            foreach (var i in result) _suggestions.Add(i);

            if (_suggestions.Count > 0)
            {
                SuggestionList.SelectedIndex = 0;
                SuggestionList.ScrollIntoView(_suggestions[0]);
            }
        }

        private ActionItem SelectedOrFirst()
        {
            return SuggestionList.SelectedItem as ActionItem ?? _suggestions.FirstOrDefault();
        }

        private void FilterBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (!string.IsNullOrEmpty(FilterBox.Text)) FilterBox.Text = "";
                    else HideLauncher();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ExecuteItem(SelectedOrFirst());
                    e.Handled = true;
                    break;

                case Key.Tab:
                    var sel = SelectedOrFirst();
                    if (sel != null)
                    {
                        FilterBox.Text = sel.Key;
                        FilterBox.CaretIndex = FilterBox.Text.Length;
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    break;

                case Key.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    break;

                case Key.P:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // Ctrl+P: quick toggle in the first panel
                        var item = SelectedOrFirst();
                        if (item != null) TogglePin(item, FirstOrCreatePanel());
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void MoveSelection(int delta)
        {
            if (_suggestions.Count == 0) return;
            var idx = SuggestionList.SelectedIndex + delta;
            if (idx < 0) idx = _suggestions.Count - 1;
            if (idx >= _suggestions.Count) idx = 0;
            SuggestionList.SelectedIndex = idx;
            SuggestionList.ScrollIntoView(SuggestionList.SelectedItem);
        }

        private void Suggestion_Click(object sender, MouseButtonEventArgs e)
        {
            // Clicks on the 📌 button inside the row must not execute the action.
            if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null) return;

            if (sender is ListBoxItem lbi && lbi.DataContext is ActionItem item)
            {
                ExecuteItem(item);
                e.Handled = true;
            }
        }

        private static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(d)
                    : LogicalTreeHelper.GetParent(d);
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Execution
        // ------------------------------------------------------------------

        private void ExecuteItem(ActionItem item)
        {
            if (item == null) return;

            try
            {
                ActionExecutor.Execute(item);
            }
            catch (Exception ex)
            {
                RunDialog(() => MessageBox.Show(this, "Action \"" + item.Key + "\" failed:\n" + ex.Message,
                    "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Error));
                return;
            }

            FilterBox.Text = "";
            if (ConfigService.Current.HideAfterAction)
                HideLauncher();
        }

        // ------------------------------------------------------------------
        // Git repositories
        // ------------------------------------------------------------------

        /// <summary>Git repository currently selected in the combo box below the panels (null if none).
        /// Also available as {{CurrentGitRepo}} in action values.</summary>
        public GitRepo CurrentGitRepo
        {
            get => VariableResolver.CurrentGitRepo;
            private set => VariableResolver.CurrentGitRepo = value;
        }

        private void GitRepoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CurrentGitRepo = GitRepoCombo.SelectedItem as GitRepo;
            if (_refreshingRepos) return;

            // Persist the selection (repository path) so it is restored on the next start.
            var cfg = ConfigService.Current;
            var path = CurrentGitRepo?.Path ?? "";
            if (!string.Equals(cfg.CurrentGitRepo ?? "", path, StringComparison.OrdinalIgnoreCase))
            {
                cfg.CurrentGitRepo = path;
                ConfigService.Save(cfg);
            }
        }

        private void GitRepoCombo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(GitRepoCombo.SelectedItem is GitRepo repo) || string.IsNullOrWhiteSpace(repo.Path)) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = repo.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                RunDialog(() => MessageBox.Show(this, "Cannot open \"" + repo.Path + "\":\n" + ex.Message,
                    "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Error));
                return;
            }

            if (ConfigService.Current.HideAfterAction)
                HideLauncher();
        }

        // ------------------------------------------------------------------
        // Pinning
        // ------------------------------------------------------------------

        private static bool IsPinned(ActionItem item, PinPanel panel)
        {
            return panel.Keys.Any(k => string.Equals(k, item.Key, StringComparison.OrdinalIgnoreCase));
        }

        private static void TogglePin(ActionItem item, PinPanel panel)
        {
            if (item == null || panel == null || string.IsNullOrWhiteSpace(item.Key)) return;

            var existing = panel.Keys.FirstOrDefault(k => string.Equals(k, item.Key, StringComparison.OrdinalIgnoreCase));
            if (existing != null) panel.Keys.Remove(existing);
            else panel.Keys.Add(item.Key);

            ConfigService.Save(ConfigService.Current);
        }

        private PinPanel FirstOrCreatePanel()
        {
            var cfg = ConfigService.Current;
            if (cfg.Panels.Count == 0)
                cfg.Panels.Add(new PinPanel { Name = ConfigService.DefaultPanelName });
            return cfg.Panels[0];
        }

        /// <summary>📌 button on a suggestion row: menu listing the panels (checked = already pinned there).</summary>
        private void PinMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.DataContext is ActionItem item)) return;

            var cfg = ConfigService.Current;
            var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

            foreach (var panel in cfg.Panels)
            {
                var p = panel;
                var mi = new MenuItem
                {
                    Header = panel.Name,
                    IsCheckable = true,
                    IsChecked = IsPinned(item, panel),
                    StaysOpenOnClick = false
                };
                mi.Click += (s, a) => TogglePin(item, p);
                menu.Items.Add(mi);
            }

            if (cfg.Panels.Count > 0) menu.Items.Add(new Separator());

            var newPanel = new MenuItem { Header = "Pin to a new panel..." };
            newPanel.Click += (s, a) =>
            {
                var panel = CreatePanelInteractive();
                if (panel != null) TogglePin(item, panel);
            };
            menu.Items.Add(newPanel);

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void Pinned_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is PinVm pin)
                ExecuteItem(pin.Item);
        }

        private void Unpin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is PinVm pin)
                TogglePin(pin.Item, pin.Panel.Panel);
        }

        // ------------------------------------------------------------------
        // Panels
        // ------------------------------------------------------------------

        private PinPanel CreatePanelInteractive()
        {
            var name = RunDialog(() => InputDialog.Show(this, "New panel", "Panel name:", "New panel"));
            if (name == null) return null;

            var cfg = ConfigService.Current;
            var panel = new PinPanel { Name = name };
            cfg.Panels.Add(panel);
            ConfigService.Save(cfg);
            return panel;
        }

        private void NewPanel_Click(object sender, RoutedEventArgs e)
        {
            CreatePanelInteractive();
        }

        private static PanelVm PanelFrom(object sender)
        {
            return (sender as FrameworkElement)?.DataContext as PanelVm;
        }

        private void RenamePanel_Click(object sender, RoutedEventArgs e)
        {
            var vm = PanelFrom(sender);
            if (vm == null) return;

            var name = RunDialog(() => InputDialog.Show(this, "Rename panel", "Panel name:", vm.Panel.Name));
            if (name == null || name == vm.Panel.Name) return;

            vm.Panel.Name = name;
            ConfigService.Save(ConfigService.Current);
        }

        private void DeletePanel_Click(object sender, RoutedEventArgs e)
        {
            var vm = PanelFrom(sender);
            if (vm == null) return;

            if (vm.Panel.Keys.Count > 0)
            {
                var answer = RunDialog(() => MessageBox.Show(this,
                    "Delete panel \"" + vm.Panel.Name + "\" and its " + vm.Panel.Keys.Count + " shortcut(s)?",
                    "Quick_Sab", MessageBoxButton.YesNo, MessageBoxImage.Question));
                if (answer != MessageBoxResult.Yes) return;
            }

            var cfg = ConfigService.Current;
            cfg.Panels.Remove(vm.Panel);
            ConfigService.Save(cfg);
        }

        private void MovePanelUp_Click(object sender, RoutedEventArgs e) => MovePanel(PanelFrom(sender), -1);
        private void MovePanelDown_Click(object sender, RoutedEventArgs e) => MovePanel(PanelFrom(sender), 1);

        private static void MovePanel(PanelVm vm, int delta)
        {
            if (vm == null) return;
            var cfg = ConfigService.Current;
            var idx = cfg.Panels.IndexOf(vm.Panel);
            var target = idx + delta;
            if (idx < 0 || target < 0 || target >= cfg.Panels.Count) return;
            cfg.Panels.Move(idx, target);
            ConfigService.Save(cfg);
        }

        // ------------------------------------------------------------------
        // Top bar / tray
        // ------------------------------------------------------------------

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { /* ignore */ }
            }
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            HideLauncher();
        }

        private void CreateTrayIcon()
        {
            _tray = new WinForms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "Quick_Sab",
                Visible = true
            };

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => ShowLauncher());
            menu.Items.Add("Settings...", null, (s, e) => { ShowLauncher(); OpenConfig(); });
            menu.Items.Add("Open config.json", null, (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ConfigService.ConfigPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Quick_Sab"); }
            });
            menu.Items.Add("About...", null, (s, e) => { ShowLauncher(); OpenAbout(); });
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApplication());
            _tray.ContextMenuStrip = menu;

            _tray.MouseClick += (s, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left) ToggleLauncher();
            };
        }
    }
}
