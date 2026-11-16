using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RightDock.Services;

namespace RightDock.Data
{
    internal sealed class BackupInfo
    {
        internal string SourcePath { get; set; }

        internal DateTime CreatedUtc { get; set; }

        internal int SchemaVersion { get; set; }

        internal long DatabaseBytes { get; set; }

        internal string Integrity { get; set; } = "unknown";

        internal Dictionary<string, long> Counts { get; set; } = new Dictionary<string, long>();

        internal long TotalRows => Counts.Values.Sum();

        internal string Summary
        {
            get
            {
                return "schema v" + SchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " · " + FormatBytes(DatabaseBytes)
                    + " · " + TotalRows.ToString("N0", CultureInfo.InvariantCulture) + " rows";
            }
        }

        internal static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }
    }

    internal sealed class RestoreOutcome
    {
        internal bool Success { get; set; }

        internal string Message { get; set; }
    }

    internal sealed class DataBackupService
    {
        private const string ManifestName = "backup.json";
        private const string DatabaseName = "rightdock.db";

        private static readonly string StagingFolder = Path.Combine(PinStore.DataFolder, "restore-staging");
        private static readonly string MarkerPath = Path.Combine(PinStore.DataFolder, "restore.pending");
        private static readonly string BackupsFolder = Path.Combine(PinStore.DataFolder, "backups");

        private readonly Database _database;

        internal DataBackupService(Database database)
        {
            _database = database;
        }

        internal static string AppVersion
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                return version == null ? "1.0.0" : version.ToString(3);
            }
        }

        internal BackupInfo GetCurrentInfo()
        {
            var info = new BackupInfo
            {
                SourcePath = _database?.Path,
                CreatedUtc = DateTime.UtcNow,
                SchemaVersion = _database?.SchemaVersion ?? 0,
                DatabaseBytes = _database?.SizeBytes ?? 0,
                Integrity = _database?.IntegrityCheck() ?? "database unavailable",
            };

            if (info.SchemaVersion <= 0) return info;

            foreach (string table in Database.KnownTables)
            {
                info.Counts[table] = _database.CountRows(table);
            }

            return info;
        }

        internal BackupInfo CreateBackup(string destinationZip)
        {
            if (string.IsNullOrWhiteSpace(destinationZip))
                throw new ArgumentException("Choose a destination for the backup.");

            string temp = Path.Combine(Path.GetTempPath(), "RightDock-backup-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(temp);

                string databaseCopy = Path.Combine(temp, DatabaseName);
                if (!_database.BackupTo(databaseCopy))
                    throw new InvalidOperationException("The database could not be copied. Check the log for details.");

                BackupInfo info = ReadDatabaseInfo(databaseCopy, DateTime.UtcNow);
                if (info.SchemaVersion > Database.CurrentSchemaVersion)
                    throw new InvalidOperationException("The database schema is newer than this version of RightDock.");

                CopyIfExists(SettingsStore.SettingsPath, Path.Combine(temp, "settings.json"));
                CopyIfExists(Path.Combine(PinStore.DataFolder, "pins.json"), Path.Combine(temp, "pins.json"));

                string iconsFolder = Path.Combine(PinStore.DataFolder, "icons");
                if (Directory.Exists(iconsFolder))
                {
                    CopyFolder(iconsFolder, Path.Combine(temp, "icons"));
                }

                var manifest = new BackupManifest
                {
                    App = "RightDock",
                    Version = AppVersion,
                    CreatedUtc = DateTime.UtcNow,
                    SchemaVersion = info.SchemaVersion,
                    Counts = info.Counts,
                };

                File.WriteAllText(Path.Combine(temp, ManifestName),
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

                string folder = Path.GetDirectoryName(destinationZip);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                if (File.Exists(destinationZip)) File.Delete(destinationZip);

                ZipFile.CreateFromDirectory(temp, destinationZip, CompressionLevel.Optimal, false);

                return ReadBackupInfo(destinationZip, info.CreatedUtc);
            }
            finally
            {
                TryDeleteFolder(temp);
            }
        }

        internal BackupInfo InspectBackup(string backupZip)
        {
            if (string.IsNullOrWhiteSpace(backupZip) || !File.Exists(backupZip))
                throw new FileNotFoundException("The selected backup file was not found.", backupZip);

            string temp = Path.Combine(Path.GetTempPath(), "RightDock-inspect-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(temp);

                using (ZipArchive archive = ZipFile.OpenRead(backupZip))
                {
                    ZipArchiveEntry entry = archive.GetEntry(DatabaseName);
                    if (entry == null)
                        throw new InvalidDataException("This archive does not contain a RightDock database.");

                    string target = Path.Combine(temp, DatabaseName);
                    entry.ExtractToFile(target, true);

                    DateTime created = File.GetLastWriteTimeUtc(backupZip);
                    BackupManifest manifest = ReadManifest(archive);
                    if (manifest?.CreatedUtc != default) created = manifest.CreatedUtc;

                    BackupInfo info = ReadDatabaseInfo(target, created);
                    info.SourcePath = backupZip;
                    return info;
                }
            }
            finally
            {
                TryDeleteFolder(temp);
            }
        }

        internal BackupInfo StageRestore(string backupZip)
        {
            BackupInfo info = InspectBackup(backupZip);

            if (Directory.Exists(StagingFolder)) Directory.Delete(StagingFolder, true);
            Directory.CreateDirectory(StagingFolder);

            string stagingFull = Path.GetFullPath(StagingFolder) + Path.DirectorySeparatorChar;

            using (ZipArchive archive = ZipFile.OpenRead(backupZip))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(StagingFolder, entry.FullName));
                    if (!target.StartsWith(stagingFull, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The backup contains an invalid file path.");

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    string folder = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                    entry.ExtractToFile(target, true);
                }
            }

            string stagedDb = Path.Combine(StagingFolder, DatabaseName);
            if (!File.Exists(stagedDb))
                throw new InvalidDataException("The backup is missing its database.");

            foreach (string json in new[] { "settings.json", "pins.json" })
            {
                string path = Path.Combine(StagingFolder, json);
                if (File.Exists(path)) ValidateJson(path, json == "pins.json");
            }

            var marker = new RestoreMarker
            {
                Source = Path.GetFullPath(backupZip),
                StagedUtc = DateTime.UtcNow,
                SchemaVersion = info.SchemaVersion,
            };

            File.WriteAllText(MarkerPath,
                JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true }));

            Log.Info("Restore staged from " + backupZip + ".");
            return info;
        }

        internal static bool HasPendingRestore => File.Exists(MarkerPath);

        internal static RestoreOutcome ApplyPendingRestore()
        {
            if (!HasPendingRestore) return null;

            string stagedDb = Path.Combine(StagingFolder, DatabaseName);
            string safetyFolder = Path.Combine(BackupsFolder,
                "pre-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

            try
            {
                if (!File.Exists(stagedDb))
                    throw new FileNotFoundException("The staged restore is missing its database.", stagedDb);

                BackupInfo info = ReadDatabaseInfo(stagedDb, DateTime.UtcNow);

                Directory.CreateDirectory(safetyFolder);
                CopyIfExists(Database.DefaultPath, Path.Combine(safetyFolder, DatabaseName));
                CopyIfExists(Database.DefaultPath + "-wal", Path.Combine(safetyFolder, DatabaseName + "-wal"));
                CopyIfExists(Database.DefaultPath + "-shm", Path.Combine(safetyFolder, DatabaseName + "-shm"));
                CopyIfExists(SettingsStore.SettingsPath, Path.Combine(safetyFolder, "settings.json"));
                CopyIfExists(Path.Combine(PinStore.DataFolder, "pins.json"), Path.Combine(safetyFolder, "pins.json"));

                TryDeleteFile(Database.DefaultPath + "-wal");
                TryDeleteFile(Database.DefaultPath + "-shm");

                File.Copy(stagedDb, Database.DefaultPath, true);
                CopyIfExists(stagedDb + "-wal", Database.DefaultPath + "-wal");
                CopyIfExists(stagedDb + "-shm", Database.DefaultPath + "-shm");

                string stagedSettings = Path.Combine(StagingFolder, "settings.json");
                if (File.Exists(stagedSettings)) File.Copy(stagedSettings, SettingsStore.SettingsPath, true);

                string stagedPins = Path.Combine(StagingFolder, "pins.json");
                if (File.Exists(stagedPins)) File.Copy(stagedPins, Path.Combine(PinStore.DataFolder, "pins.json"), true);

                string stagedIcons = Path.Combine(StagingFolder, "icons");
                if (Directory.Exists(stagedIcons))
                {
                    CopyFolder(stagedIcons, Path.Combine(PinStore.DataFolder, "icons"));
                }

                TryDeleteFolder(StagingFolder);
                TryDeleteFile(MarkerPath);
                Log.Info("Backup restored: " + info.Summary + ". Previous data saved to " + safetyFolder + ".");

                return new RestoreOutcome
                {
                    Success = true,
                    Message = "Restored " + info.Summary + " from a backup created "
                        + info.CreatedUtc.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture) + ".",
                };
            }
            catch (Exception ex)
            {
                Log.Error("Could not apply the staged backup.", ex);

                try
                {
                    string safetyDb = Path.Combine(safetyFolder, DatabaseName);
                    if (File.Exists(safetyDb))
                    {
                        TryDeleteFile(Database.DefaultPath + "-wal");
                        TryDeleteFile(Database.DefaultPath + "-shm");
                        File.Copy(safetyDb, Database.DefaultPath, true);
                        CopyIfExists(safetyDb + "-wal", Database.DefaultPath + "-wal");
                        CopyIfExists(safetyDb + "-shm", Database.DefaultPath + "-shm");
                    }
                }
                catch (Exception rollbackError)
                {
                    Log.Error("Could not roll back the database after a failed restore.", rollbackError);
                }

                TryDeleteFolder(StagingFolder);
                TryDeleteFile(MarkerPath);

                return new RestoreOutcome
                {
                    Success = false,
                    Message = "The backup could not be applied. Your previous data was kept.",
                };
            }
        }

        private static BackupInfo ReadBackupInfo(string backupZip, DateTime createdUtc)
        {
            BackupInfo info = null;

            using (ZipArchive archive = ZipFile.OpenRead(backupZip))
            {
                ZipArchiveEntry entry = archive.GetEntry(DatabaseName);
                if (entry == null)
                    throw new InvalidDataException("This archive does not contain a RightDock database.");

                string temp = Path.Combine(Path.GetTempPath(), "RightDock-info-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(temp);
                    string target = Path.Combine(temp, DatabaseName);
                    entry.ExtractToFile(target, true);
                    info = ReadDatabaseInfo(target, createdUtc);
                }
                finally
                {
                    TryDeleteFolder(temp);
                }
            }

            info.SourcePath = backupZip;
            return info;
        }

        private static BackupInfo ReadDatabaseInfo(string databasePath, DateTime createdUtc)
        {
            var info = new BackupInfo
            {
                SourcePath = databasePath,
                CreatedUtc = createdUtc,
                DatabaseBytes = new FileInfo(databasePath).Length,
            };

            SQLitePCL.Batteries_V2.Init();

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
            };

            using (var connection = new SqliteConnection(builder.ToString()))
            {
                connection.Open();

                info.Integrity = Convert.ToString(Scalar(connection, "PRAGMA integrity_check;"), CultureInfo.InvariantCulture)
                    ?? "no result";
                if (!string.Equals(info.Integrity, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The backup database failed its integrity check: " + info.Integrity);

                info.SchemaVersion = Convert.ToInt32(Scalar(connection, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
                if (info.SchemaVersion < 1 || info.SchemaVersion > Database.CurrentSchemaVersion)
                {
                    throw new InvalidDataException("The backup uses unsupported schema version "
                        + info.SchemaVersion.ToString(CultureInfo.InvariantCulture) + ".");
                }

                var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read()) tables.Add(reader.GetString(0));
                    }
                }

                if (!tables.Contains("ModuleSettings"))
                    throw new InvalidDataException("The backup does not look like a RightDock database.");

                foreach (string table in Database.KnownTables)
                {
                    if (tables.Contains(table))
                    {
                        info.Counts[table] = Convert.ToInt64(
                            Scalar(connection, "SELECT COUNT(*) FROM " + table + ";"), CultureInfo.InvariantCulture);
                    }
                }
            }

            return info;
        }

        private static object Scalar(SqliteConnection connection, string sql)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return command.ExecuteScalar();
            }
        }

        private static BackupManifest ReadManifest(ZipArchive archive)
        {
            try
            {
                ZipArchiveEntry entry = archive.GetEntry(ManifestName);
                if (entry == null) return null;

                using (Stream stream = entry.Open())
                using (var reader = new StreamReader(stream))
                {
                    return JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd());
                }
            }
            catch
            {
                return null;
            }
        }

        private static void ValidateJson(string path, bool expectArray)
        {
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                if (expectArray && document.RootElement.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("The backup contains an invalid pins file.");
            }
        }

        private static void CopyIfExists(string source, string destination)
        {
            if (!File.Exists(source)) return;

            string folder = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.Copy(source, destination, true);
        }

        private static void CopyFolder(string source, string destination)
        {
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(destination, relative);
                string folder = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.Copy(file, target, true);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Cleanup is best-effort.
            }
        }

        private static void TryDeleteFolder(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch
            {
                // Cleanup is best-effort.
            }
        }

        private sealed class BackupManifest
        {
            public string App { get; set; }
            public string Version { get; set; }
            public DateTime CreatedUtc { get; set; }
            public int SchemaVersion { get; set; }
            public Dictionary<string, long> Counts { get; set; }
        }

        private sealed class RestoreMarker
        {
            public string Source { get; set; }
            public DateTime StagedUtc { get; set; }
            public int SchemaVersion { get; set; }
        }
    }
}