using System;
using RightDock.Data;
using RightDock.Modules;
using RightDock.Services;

namespace RightDock
{
    internal sealed class DockServices : IDisposable
    {
        private bool _disposed;

        internal DockServices()
        {
            Settings = SettingsStore.Load();
            Data = new Database(Database.DefaultPath);
            Data.Initialize();
            Scheduler = new Scheduler();
            Notifications = new NotificationService(Settings);
            Modules = new ModuleManager(Data, Scheduler, Notifications, Settings);
        }

        internal AppSettings Settings { get; }

        internal Database Data { get; }

        internal Scheduler Scheduler { get; }

        internal NotificationService Notifications { get; }

        internal ModuleManager Modules { get; }

        internal void Start()
        {
            Modules.Start();
            Scheduler.Reconcile();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Modules.Dispose();
            Scheduler.Dispose();
            Notifications.Dispose();
            Data.Dispose();
        }
    }
}
