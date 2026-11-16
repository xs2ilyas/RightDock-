using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using RightDock.Data;
using RightDock.Modules.Calendar;
using RightDock.Modules.Dashboard;
using RightDock.Modules.Focus;
using RightDock.Modules.Habits;
using RightDock.Modules.Health;
using RightDock.Modules.Learning;
using RightDock.Modules.Medication;
using RightDock.Modules.Notes;
using RightDock.Modules.QuickTools;
using RightDock.Modules.Reminders;
using RightDock.Modules.Statistics;
using RightDock.Modules.Tasks;
using RightDock.Services;

namespace RightDock
{
    public partial class App : Application
    {
        private Mutex _instanceMutex;
        private DockServices _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool restart = Array.Exists(e.Args,
                argument => string.Equals(argument, "--restart", StringComparison.OrdinalIgnoreCase));

            _instanceMutex = AcquireInstanceMutex(restart);
            if (_instanceMutex == null)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Log.Info("RightDock " + (version == null ? "1.0.0" : version.ToString(3))
                + " starting on " + Environment.OSVersion.VersionString
                + (restart ? " (restart)." : "."));

            RestoreOutcome restore = DataBackupService.ApplyPendingRestore();

            try
            {
                _services = new DockServices();
                _services.Modules.Register(new DashboardModule());
                _services.Modules.Register(new FocusModule());
                _services.Modules.Register(new TasksModule());
                _services.Modules.Register(new RemindersModule());
                _services.Modules.Register(new NotesModule());
                _services.Modules.Register(new HabitsModule());
                _services.Modules.Register(new MedicationModule());
                _services.Modules.Register(new HealthModule());
                _services.Modules.Register(new CalendarModule());
                _services.Modules.Register(new StatisticsModule());
                _services.Modules.Register(new QuickToolsModule());
                _services.Modules.Register(new LearningModule());
                _services.Start();

                if (restore != null)
                {
                    _services.Notifications.Show(
                        restore.Success ? "Backup restored" : "Restore failed",
                        restore.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not start RightDock services.", ex);
            }

            var window = new MainWindow(_services);
            MainWindow = window;
            window.Show();
        }

        private static Mutex AcquireInstanceMutex(bool waitForPrevious)
        {
            int attempts = waitForPrevious ? 40 : 1;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var mutex = new Mutex(true, @"Local\RightDock.SingleInstance", out bool createdNew);
                if (createdNew) return mutex;

                mutex.Dispose();
                if (attempt + 1 < attempts) Thread.Sleep(250);
            }

            return null;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
