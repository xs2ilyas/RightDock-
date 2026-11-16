using System;
using RightDock.Data;
using RightDock.Services;

namespace RightDock.Modules
{
    internal sealed class ModuleContext
    {
        internal ModuleContext(AppSettings settings, Database database, Scheduler scheduler,
            NotificationService notifications)
        {
            Settings = settings;
            Database = database;
            Scheduler = scheduler;
            Notifications = notifications;
        }

        internal AppSettings Settings { get; }

        internal Database Database { get; }

        internal Scheduler Scheduler { get; }

        internal NotificationService Notifications { get; }

        internal Action<string> OpenExpanded { get; set; }

        internal Action<string> RefreshModule { get; set; }

        internal Func<string, string> LoadModuleConfig { get; set; }

        internal Action<string, string> SaveModuleConfig { get; set; }

        internal void LogInfo(string message)
        {
            Log.Info("[module] " + message);
        }

        internal void LogError(string message, Exception exception = null)
        {
            Log.Error("[module] " + message, exception);
        }
    }
}
