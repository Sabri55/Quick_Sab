using System;
using System.Threading;
using System.Windows;
using Quick_Sab.Services;
using Quick_Sab.Views;

namespace Quick_Sab
{
    public partial class App : Application
    {
        private Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mutex = new Mutex(true, "Quick_Sab_SingleInstance_Mutex", out var createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Quick_Sab is already running (see the icon in the notification area).",
                    "Quick_Sab", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(args.Exception.Message, "Quick_Sab - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            ConfigService.Load();

            var main = new MainWindow();
            MainWindow = main;
            main.ShowLauncher();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
