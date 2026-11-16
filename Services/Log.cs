using System;
using System.IO;

namespace RightDock.Services
{
    internal static class Log
    {
        private const long MaxBytes = 1_000_000;

        private static readonly object Sync = new object();

        internal static string Folder => Path.Combine(PinStore.DataFolder, "logs");

        internal static string FilePath => Path.Combine(Folder, "rightdock.log");

        internal static void Info(string message)
        {
            Write("INFO", message, null);
        }

        internal static void Error(string message, Exception exception = null)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Folder);

                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > MaxBytes)
                    {
                        string previous = FilePath + ".1";
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(FilePath, previous);
                    }

                    string line = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff") + "Z [" + level + "] " + message;
                    if (exception != null)
                    {
                        line += Environment.NewLine + exception;
                    }

                    File.AppendAllText(FilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never interrupt the dock.
            }
        }
    }
}
