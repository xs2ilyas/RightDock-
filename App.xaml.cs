using System;
using System.Threading;
using System.Windows;
using RightDock.Services;

namespace RightDock
{
    public partial class App : Application
    {
        private Mutex _instanceMutex;
        private DockServices _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            _instanceMutex = new Mutex(true, @"Local\RightDock.SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                _services = new DockServices();
                _services.Start();
            }
            catch (Exception ex)
            {
                Log.Error("Could not start RightDock services.", ex);
            }

            var window = new MainWindow(_services);
            MainWindow = window;
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
