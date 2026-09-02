using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Quick_Sab.Views
{
    /// <summary>About dialog: application info and author contact links.</summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            var v = Application.ResourceAssembly.GetName().Version;
            VersionText.Text = v == null ? "" : "Version " + v.ToString(3);
        }

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch
            {
                // no handler for mailto/https: ignore
            }
            e.Handled = true;
        }
    }
}
