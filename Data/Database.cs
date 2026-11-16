using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RightDock.Services;

namespace RightDock.Data
{
    internal sealed class Database : IDisposable
    {
        internal static readonly string DefaultPath = System.IO.Path.Combine(PinStore.DataFolder, "rightdock.db");

        private static readonly (int Version, string Sql)[] Migrations =
        {
            (1, @"
CREATE TABLE IF NOT EXISTS Tasks (
    TaskId TEXT PRIMARY KEY NOT NULL,
    Title TEXT NOT NULL,
    Description TEXT,
    Priority INTEGER NOT NULL DEFAULT 1,
    DueUtc TEXT,
    CategoryId TEXT,
    ProjectId TEXT,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    CompletedUtc TEXT,
    RecurrenceRule TEXT,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL,
    ArchivedUtc TEXT
);
CREATE INDEX IF NOT EXISTS IX_Tasks_Due ON Tasks(DueUtc);
CREATE INDEX IF NOT EXISTS IX_Tasks_Completed ON Tasks(IsCompleted, DueUtc);
CREATE INDEX IF NOT EXISTS IX_Tasks_Category ON Tasks(CategoryId);

CREATE TABLE IF NOT EXISTS TaskCategories (
    CategoryId TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    Color TEXT,
    SortOrder INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Projects (
    ProjectId TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    Color TEXT,
    CreatedUtc TEXT NOT NULL,
    ArchivedUtc TEXT
);

CREATE TABLE IF NOT EXISTS Reminders (
    ReminderId TEXT PRIMARY KEY NOT NULL,
    Title TEXT NOT NULL,
    Notes TEXT,
    DueUtc TEXT NOT NULL,
    RecurrenceRule TEXT,
    CategoryId TEXT,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    CompletedUtc TEXT,
    SnoozeUntilUtc TEXT,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Reminders_Due ON Reminders(IsCompleted, DueUtc);

CREATE TABLE IF NOT EXISTS FocusSessions (
    SessionId TEXT PRIMARY KEY NOT NULL,
    StartedUtc TEXT NOT NULL,
    EndedUtc TEXT,
    PlannedSeconds INTEGER NOT NULL DEFAULT 0,
    ActualSeconds INTEGER NOT NULL DEFAULT 0,
    SessionType TEXT NOT NULL DEFAULT 'focus',
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    TaskId TEXT,
    CreatedUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_FocusSessions_Started ON FocusSessions(StartedUtc);

CREATE TABLE IF NOT EXISTS Habits (
    HabitId TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    TrackingType TEXT NOT NULL DEFAULT 'boolean',
    TargetValue REAL NOT NULL DEFAULT 1,
    Unit TEXT,
    RecurrenceRule TEXT,
    IsPaused INTEGER NOT NULL DEFAULT 0,
    IsArchived INTEGER NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS HabitLogs (
    LogId TEXT PRIMARY KEY NOT NULL,
    HabitId TEXT NOT NULL REFERENCES Habits(HabitId) ON DELETE CASCADE,
    LogDate TEXT NOT NULL,
    Value REAL NOT NULL DEFAULT 0,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    Notes TEXT,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL,
    UNIQUE(HabitId, LogDate)
);
CREATE INDEX IF NOT EXISTS IX_HabitLogs_Date ON HabitLogs(LogDate);

CREATE TABLE IF NOT EXISTS Medications (
    MedicationId TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    Dosage TEXT,
    Notes TEXT,
    StartDate TEXT,
    EndDate TEXT,
    IsArchived INTEGER NOT NULL DEFAULT 0,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS MedicationSchedules (
    ScheduleId TEXT PRIMARY KEY NOT NULL,
    MedicationId TEXT NOT NULL REFERENCES Medications(MedicationId) ON DELETE CASCADE,
    TimeOfDay TEXT NOT NULL,
    DaysOfWeek TEXT NOT NULL DEFAULT '0,1,2,3,4,5,6',
    ReminderEnabled INTEGER NOT NULL DEFAULT 1,
    CreatedUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_MedicationSchedules_Medication ON MedicationSchedules(MedicationId);

CREATE TABLE IF NOT EXISTS MedicationLogs (
    LogId TEXT PRIMARY KEY NOT NULL,
    MedicationId TEXT NOT NULL REFERENCES Medications(MedicationId) ON DELETE CASCADE,
    ScheduleId TEXT,
    ScheduledUtc TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'pending',
    TakenUtc TEXT,
    Notes TEXT,
    CreatedUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_MedicationLogs_Scheduled ON MedicationLogs(ScheduledUtc);

CREATE TABLE IF NOT EXISTS HealthLogs (
    LogId TEXT PRIMARY KEY NOT NULL,
    LogDate TEXT NOT NULL,
    MetricType TEXT NOT NULL,
    Value REAL NOT NULL DEFAULT 0,
    Unit TEXT,
    Notes TEXT,
    CreatedUtc TEXT NOT NULL,
    UNIQUE(LogDate, MetricType)
);
CREATE INDEX IF NOT EXISTS IX_HealthLogs_Date ON HealthLogs(LogDate);

CREATE TABLE IF NOT EXISTS Notes (
    NoteId TEXT PRIMARY KEY NOT NULL,
    Title TEXT,
    Body TEXT NOT NULL DEFAULT '',
    Category TEXT,
    IsPinned INTEGER NOT NULL DEFAULT 0,
    IsArchived INTEGER NOT NULL DEFAULT 0,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Notes_Updated ON Notes(UpdatedUtc);

CREATE TABLE IF NOT EXISTS LearningGoals (
    GoalId TEXT PRIMARY KEY NOT NULL,
    Title TEXT NOT NULL,
    Description TEXT,
    Category TEXT,
    TargetValue REAL NOT NULL DEFAULT 0,
    Unit TEXT,
    ProgressValue REAL NOT NULL DEFAULT 0,
    DueDate TEXT,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS LearningSessions (
    SessionId TEXT PRIMARY KEY NOT NULL,
    GoalId TEXT REFERENCES LearningGoals(GoalId) ON DELETE SET NULL,
    StartedUtc TEXT NOT NULL,
    DurationSeconds INTEGER NOT NULL DEFAULT 0,
    Notes TEXT,
    CreatedUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_LearningSessions_Started ON LearningSessions(StartedUtc);

CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT PRIMARY KEY NOT NULL,
    Value TEXT,
    UpdatedUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ModuleSettings (
    ModuleId TEXT PRIMARY KEY NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    IsVisible INTEGER NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    ConfigJson TEXT NOT NULL DEFAULT '{}',
    UpdatedUtc TEXT NOT NULL
);
")
        };

        private readonly object _gate = new object();
        private readonly string _path;

        private SqliteConnection _connection;
        private bool _disposed;

        internal Database(string path)
        {
            _path = path;
        }

        internal string Path => _path;

        internal bool IsAvailable { get; private set; }

        internal string LastError { get; private set; }

        internal void Initialize()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Database));
                if (_connection != null) return;

                try
                {
                    string folder = System.IO.Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                    SQLitePCL.Batteries_V2.Init();

                    var builder = new SqliteConnectionStringBuilder
                    {
                        DataSource = _path,
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        Cache = SqliteCacheMode.Private,
                    };

                    _connection = new SqliteConnection(builder.ToString());
                    _connection.Open();
                    _connection.DefaultTimeout = 5;

                    using (SqliteCommand pragmas = _connection.CreateCommand())
                    {
                        pragmas.CommandText =
                            "PRAGMA journal_mode=WAL;" +
                            "PRAGMA foreign_keys=ON;" +
                            "PRAGMA busy_timeout=5000;";
                        pragmas.ExecuteNonQuery();
                    }

                    ApplyMigrations();

                    IsAvailable = true;
                    LastError = null;
                    Log.Info("Database ready: " + _path);
                }
                catch (Exception ex)
                {
                    IsAvailable = false;
                    LastError = ex.Message;
                    Log.Error("Database initialization failed.", ex);
                }
            }
        }

        internal int Execute(string sql, params SqliteParameter[] parameters)
        {
            lock (_gate)
            {
                using (SqliteCommand command = CreateCommand(sql, parameters))
                {
                    return command.ExecuteNonQuery();
                }
            }
        }

        internal object Scalar(string sql, params SqliteParameter[] parameters)
        {
            lock (_gate)
            {
                using (SqliteCommand command = CreateCommand(sql, parameters))
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? reader.GetValue(0) : null;
                }
            }
        }

        internal List<Dictionary<string, object>> Query(string sql, params SqliteParameter[] parameters)
        {
            var rows = new List<Dictionary<string, object>>();

            lock (_gate)
            {
                using (SqliteCommand command = CreateCommand(sql, parameters))
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        internal static SqliteParameter Param(string name, object value)
        {
            return new SqliteParameter(name, value ?? DBNull.Value);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                IsAvailable = false;

                try
                {
                    _connection?.Dispose();
                }
                catch
                {
                    // Closing the database is best-effort.
                }

                _connection = null;
            }
        }

        private void ApplyMigrations()
        {
            int current = Convert.ToInt32(ScalarCore("PRAGMA user_version;", null), CultureInfo.InvariantCulture);

            foreach ((int version, string sql) in Migrations)
            {
                if (version <= current) continue;

                using (SqliteTransaction transaction = _connection.BeginTransaction())
                {
                    using (SqliteCommand schema = _connection.CreateCommand())
                    {
                        schema.Transaction = transaction;
                        schema.CommandText = sql;
                        schema.ExecuteNonQuery();
                    }

                    using (SqliteCommand stamp = _connection.CreateCommand())
                    {
                        stamp.Transaction = transaction;
                        stamp.CommandText = "PRAGMA user_version = " + version.ToString(CultureInfo.InvariantCulture) + ";";
                        stamp.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }

                Log.Info("Applied database migration v" + version + ".");
            }
        }

        private object ScalarCore(string sql, SqliteParameter[] parameters)
        {
            using (SqliteCommand command = CreateCommand(sql, parameters))
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                return reader.Read() ? reader.GetValue(0) : null;
            }
        }

        private SqliteCommand CreateCommand(string sql, SqliteParameter[] parameters)
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("The RightDock database is not available.");
            }

            SqliteCommand command = _connection.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (SqliteParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            return command;
        }
    }
}
