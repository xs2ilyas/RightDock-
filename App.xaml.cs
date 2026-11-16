using System;
using System.Threading;
using System.Windows;
using RightDock.Modules.Calendar;
using RightDock.Modules.Dashboard;
using RightDock.Modules.Focus;
using RightDock.Modules.Habits;
using RightDock.Modules.Health;
using RightDock.Modules.Medication;
using RightDock.Modules.Notes;
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
