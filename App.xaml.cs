using System.Threading;
using System.Windows;

namespace RightDock
{
    public partial class App : Application
    {
        private Mutex _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _instanceMutex = new Mutex(true, @"Local\RightDock.SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);
            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
