using System;
using System.IO;
using System.Text.Json;

namespace RightDock
{
    public sealed class AppSettings
    {
        public bool AutoHide { get; set; } = true;
        public string IconColor { get; set; } = "#A9A9A9";
        public bool NotificationsEnabled { get; set; } = true;
    }

    internal static class SettingsStore
    {
        internal static readonly string SettingsPath = Path.Combine(PinStore.DataFolder, "settings.json");

        internal static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new AppSettings();

                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (settings == null) return new AppSettings();

                if (string.IsNullOrWhiteSpace(settings.IconColor)) settings.IconColor = "#A9A9A9";
                return settings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        internal static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(PinStore.DataFolder);
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Losing a preference write should not interrupt the dock.
            }
        }
    }
}
