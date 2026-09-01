using System;
using System.Security.Cryptography;
using System.Windows;
using Quick_Sab.Services;

namespace Quick_Sab.Views
{
    /// <summary>AES encrypt / decrypt window using the Key / IV stored in the configuration.</summary>
    public partial class CryptoWindow : Window
    {
        public CryptoWindow()
        {
            InitializeComponent();
            RefreshStatus();
            Activated += (s, e) => RefreshStatus(); // key may change while the settings dialog is open
            Loaded += (s, e) => InputBox.Focus();
        }

        private static string KeyStr => ConfigService.Current.Crypto?.Key ?? "";
        private static string IvStr => ConfigService.Current.Crypto?.IV ?? "";

        private void RefreshStatus()
        {
            var error = CryptoService.Validate(KeyStr, IvStr);
            StatusText.Text = error == null
                ? "AES-256-CBC — Key / IV loaded from the configuration (Settings → Crypto tab). Encrypted output is Base64."
                : "⚠ " + error + " Set them in Settings → \"Crypto (AES)\".";
        }

        private bool EnsureKeys()
        {
            var error = CryptoService.Validate(KeyStr, IvStr);
            if (error == null) return true;
            MessageBox.Show(this, error + "\n\nSet the Key (32 chars) and IV (16 chars) in Settings → \"Crypto (AES)\".",
                "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void Encrypt_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureKeys()) return;
            try
            {
                OutputBox.Text = CryptoService.Encrypt(InputBox.Text, KeyStr, IvStr);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Encryption failed:\n" + ex.Message, "Quick_Sab",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Decrypt_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureKeys()) return;
            try
            {
                OutputBox.Text = CryptoService.Decrypt(InputBox.Text, KeyStr, IvStr);
            }
            catch (FormatException)
            {
                MessageBox.Show(this, "The input is not valid Base64 text.", "Quick_Sab",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CryptographicException)
            {
                MessageBox.Show(this, "Decryption failed: wrong Key / IV or corrupted data.", "Quick_Sab",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Decryption failed:\n" + ex.Message, "Quick_Sab",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Swap_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OutputBox.Text)) return;
            InputBox.Text = OutputBox.Text;
            OutputBox.Clear();
            InputBox.Focus();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(OutputBox.Text))
                Clipboard.SetText(OutputBox.Text);
        }
    }
}
