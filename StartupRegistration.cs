using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace RightDock
{
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "RightDock";

        internal static string ExecutablePath
        {
            get
            {
                try
                {
                    return Process.GetCurrentProcess().MainModule.FileName;
                }
                catch
                {
                    return System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty;
                }
            }
        }

        internal static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key?.GetValue(ValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, true))
            {
                if (key == null) throw new InvalidOperationException("The startup registry key is unavailable.");

                if (enabled)
                {
                    key.SetValue(ValueName, "\"" + ExecutablePath + "\"");
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
