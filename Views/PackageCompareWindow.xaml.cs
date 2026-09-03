using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using Quick_Sab.Models;
using Quick_Sab.Services;

namespace Quick_Sab.Views
{
    /// <summary>
    /// Independent (non-modal) window comparing the packages of the source folder with the target
    /// folder. Works on the PackageCompareConfig given to the constructor (e.g. the values being
    /// edited in the settings), or on the saved configuration by default.
    /// </summary>
    public partial class PackageCompareWindow : Window
    {
        /// <summary>Row of the comparison grid.</summary>
        public class PackageRow : NotifyBase
        {
            private bool _selected;
            public bool Selected { get => _selected; set => Set(ref _selected, value); }

            public PackageComparison Item { get; set; }
            public string Name => Item.SourceName;
            public string SourceVersion => Item.SourceVersion;
            public string TargetVersion => Item.TargetName == null ? "—" : Item.TargetVersion;
            public string Status => Item.Status;
        }

        private readonly ObservableCollection<PackageRow> _rows = new ObservableCollection<PackageRow>();
        private readonly PackageCompareConfig _cfg;
        private bool _updating;

        public PackageCompareWindow(PackageCompareConfig cfg = null)
        {
            InitializeComponent();
            _cfg = cfg ?? ConfigService.Current.PackageCompare;
            ResultGrid.ItemsSource = _rows;
            Loaded += (s, e) => RunCompare();
        }

        /// <summary>Raw configured path, with the resolved value appended when variables were used.</summary>
        private static string Describe(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "(not set)";
            var resolved = PackageCompareService.ResolvePath(raw);
            return string.Equals(raw, resolved, StringComparison.Ordinal) ? raw : raw + "  ->  " + resolved;
        }

        private void RunCompare()
        {
            _rows.Clear();
            SourceLinkText.Text = Describe(_cfg?.SourcePath);
            TargetLinkText.Text = Describe(_cfg?.TargetPath);

            List<PackageComparison> result;
            try
            {
                result = PackageCompareService.Compare(_cfg);
            }
            catch (Exception ex)
            {
                SummaryText.Text = "⚠ " + ex.Message;
                return;
            }

            foreach (var c in result)
                _rows.Add(new PackageRow { Item = c, Selected = !c.IsUpToDate });

            var mismatch = _rows.Count(r => !r.Item.IsUpToDate);
            SummaryText.Text = _rows.Count + " package(s) found, " + mismatch + " to update.";
        }

        private void SourceLink_Click(object sender, RoutedEventArgs e) => OpenFolder(_cfg?.SourcePath);
        private void TargetLink_Click(object sender, RoutedEventArgs e) => OpenFolder(_cfg?.TargetPath);

        /// <summary>Opens the (variable-resolved) configured folder in Explorer.</summary>
        private void OpenFolder(string raw)
        {
            var path = PackageCompareService.ResolvePath(raw);
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "The path is not set (Settings -> Packages).", "Quick_Sab",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!System.IO.Directory.Exists(path))
            {
                MessageBox.Show(this, "Folder not found:\n" + path, "Quick_Sab",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            RunCompare();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>Runs the copies in parallel on background threads; the UI stays responsive.</summary>
        private async void UpdateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_updating) return;

            var rows = _rows.Where(r => r.Selected).ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "Check at least one package to update.", "Quick_Sab",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var answer = MessageBox.Show(this,
                "Update " + rows.Count + " package(s)?\n\nThe old target entries will be deleted and replaced by the source versions.",
                "Quick_Sab", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            _updating = true;
            UpdateButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
            var total = rows.Count;
            var done = 0;
            SummaryText.Text = "Updating 0 / " + total + "...";
            CopyProgress.Maximum = total;
            CopyProgress.Value = 0;
            CopyProgress.Visibility = Visibility.Visible;
            var progress = new Progress<int>(d =>
            {
                CopyProgress.Value = d;
                SummaryText.Text = "Updating " + d + " / " + total + "...";
            });

            List<string> errors;
            try
            {
                errors = await PackageCompareService.UpdatePackagesAsync(
                    rows.Select(r => r.Item),
                    _cfg?.TargetPath,
                    _ => ((IProgress<int>)progress).Report(Interlocked.Increment(ref done)));
            }
            finally
            {
                _updating = false;
                UpdateButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
                CopyProgress.Visibility = Visibility.Collapsed;
            }

            RunCompare();
            SummaryText.Text = (total - errors.Count) + " package(s) updated" + (errors.Count > 0 ? ", " + errors.Count + " error(s)." : ".");
            if (errors.Count > 0)
                MessageBox.Show(this, string.Join("\n", errors), "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
