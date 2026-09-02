using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Quick_Sab.Models;
using Quick_Sab.Services;

namespace Quick_Sab.Views
{
    /// <summary>Compares the packages of the configured source folder with the target folder.</summary>
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

        public PackageCompareWindow()
        {
            InitializeComponent();
            ResultGrid.ItemsSource = _rows;
            Loaded += (s, e) => RunCompare();
        }

        private static PackageCompareConfig Cfg => ConfigService.Current.PackageCompare;

        private void RunCompare()
        {
            _rows.Clear();
            PathsText.Text = "Source: " + (string.IsNullOrWhiteSpace(Cfg?.SourcePath) ? "(not set)" : Cfg.SourcePath)
                + "\nTarget: " + (string.IsNullOrWhiteSpace(Cfg?.TargetPath) ? "(not set)" : Cfg.TargetPath);

            List<PackageComparison> result;
            try
            {
                result = PackageCompareService.Compare(Cfg);
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

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RunCompare();
        }

        private void UpdateSelected_Click(object sender, RoutedEventArgs e)
        {
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

            var errors = new List<string>();
            var done = 0;
            foreach (var row in rows)
            {
                try
                {
                    PackageCompareService.UpdatePackage(row.Item, Cfg?.TargetPath);
                    done++;
                }
                catch (Exception ex)
                {
                    errors.Add(row.Name + ": " + ex.Message);
                }
            }

            RunCompare();
            SummaryText.Text = done + " package(s) updated" + (errors.Count > 0 ? ", " + errors.Count + " error(s)." : ".");
            if (errors.Count > 0)
                MessageBox.Show(this, string.Join("\n", errors), "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
