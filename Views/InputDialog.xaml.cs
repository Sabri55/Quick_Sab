using System.Windows;

namespace Quick_Sab.Views
{
    /// <summary>Minimal single-line text prompt.</summary>
    public partial class InputDialog : Window
    {
        public string Value => ValueBox.Text;

        public InputDialog(string title, string prompt, string initial)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            ValueBox.Text = initial ?? "";
            Loaded += (s, e) =>
            {
                ValueBox.Focus();
                ValueBox.SelectAll();
            };
        }

        /// <summary>Shows the prompt. Returns the trimmed text, or null when cancelled / empty.</summary>
        public static string Show(Window owner, string title, string prompt, string initial = "")
        {
            var dlg = new InputDialog(title, prompt, initial);
            if (owner != null && owner.IsVisible) dlg.Owner = owner;
            if (dlg.ShowDialog() != true) return null;
            var text = dlg.Value.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
