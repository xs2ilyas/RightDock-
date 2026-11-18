using System;
using System.IO;
using System.Text.Json;
using RightDock.Services;

namespace RightDock
{
    public sealed class AppSettings
    {
        public const int DefaultIconSize = 26;
        public const int MinIconSize = 20;
        public const int MaxIconSize = 34;

        public static readonly int[] IconSizeOptions = { 20, 26, 30, 34 };

        public bool AutoHide { get; set; } = true;
        public string IconColor { get; set; } = "#A9A9A9";
        public int IconSize { get; set; } = DefaultIconSize;
        public bool NotificationsEnabled { get; set; } = true;
        public string FocusSound { get; set; } = BellSynth.DefaultFocus;
        public string ReminderSound { get; set; } = BellSynth.DefaultReminder;
        public bool MiniTimerPinned { get; set; } = true;
        public double? MiniTimerLeft { get; set; }
        public double? MiniTimerTop { get; set; }

        public static int NormalizeIconSize(int size)
        {
            return Array.IndexOf(IconSizeOptions, size) >= 0 ? size : DefaultIconSize;
        }
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
                settings.IconSize = AppSettings.NormalizeIconSize(settings.IconSize);
                if (!BellSynth.IsKnown(settings.FocusSound)) settings.FocusSound = BellSynth.DefaultFocus;
                if (!BellSynth.IsKnown(settings.ReminderSound)) settings.ReminderSound = BellSynth.DefaultReminder;
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
