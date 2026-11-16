using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RightDock
{
    public sealed class Pin
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Icon { get; set; }
    }

    internal static class PinStore
    {
        internal static readonly string DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RightDock");

        private static readonly string FilePath = Path.Combine(DataFolder, "pins.json");

        public static List<Pin> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<Pin>();
                return JsonSerializer.Deserialize<List<Pin>>(File.ReadAllText(FilePath)) ?? new List<Pin>();
            }
            catch
            {
                return new List<Pin>();
            }
        }

        public static void Save(List<Pin> pins)
        {
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(pins, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
