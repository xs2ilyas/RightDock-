using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class FocusSessionItem
    {
        internal string Id { get; set; }

        internal DateTime StartedUtc { get; set; }

        internal DateTime? EndedUtc { get; set; }

        internal int PlannedSeconds { get; set; }

        internal int ActualSeconds { get; set; }

        internal string SessionType { get; set; } = "focus";

        internal bool IsCompleted { get; set; }

        internal string TaskId { get; set; }

        internal string TaskTitle { get; set; }
    }

    internal sealed class FocusStore
    {
        private readonly Database _database;

        internal FocusStore(Database database)
        {
            _database = database;
        }

        internal string Start(DateTime startedUtc, int plannedSeconds, string taskId)
        {
            if (_database == null || !_database.IsAvailable) return null;

            string id = ModuleUtil.NewId();

            try
            {
                _database.Execute(
                    @"INSERT INTO FocusSessions (SessionId, StartedUtc, PlannedSeconds, ActualSeconds,
                        SessionType, IsCompleted, TaskId, CreatedUtc)
                      VALUES (@id, @started, @planned, 0, 'focus', 0, @task, @started);",
                    Database.Param("@id", id),
                    Database.Param("@started", ModuleUtil.Iso(startedUtc)),
                    Database.Param("@planned", plannedSeconds),
                    Database.Param("@task", string.IsNullOrWhiteSpace(taskId) ? null : taskId));

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not start a focus session.", ex);
                return null;
            }
        }

        internal void Finish(string sessionId, DateTime endedUtc, int actualSeconds, bool completed)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || _database == null || !_database.IsAvailable) return;

            try
            {
                _database.Execute(
                    @"UPDATE FocusSessions SET EndedUtc = @ended, ActualSeconds = @actual,
                        IsCompleted = @completed WHERE SessionId = @id;",
                    Database.Param("@ended", ModuleUtil.Iso(endedUtc)),
                    Database.Param("@actual", Math.Max(0, actualSeconds)),
                    Database.Param("@completed", completed ? 1 : 0),
                    Database.Param("@id", sessionId));
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not finish a focus session.", ex);
            }
        }

        internal void FinalizeStaleSessions()
        {
            if (_database == null || !_database.IsAvailable) return;

            try
            {
                _database.Execute(
                    @"UPDATE FocusSessions SET EndedUtc = StartedUtc, IsCompleted = 0
                      WHERE EndedUtc IS NULL;");
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not finalize stale focus sessions.", ex);
            }
        }

        internal void Discard(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || _database == null || !_database.IsAvailable) return;

            try
            {
                _database.Execute(
                    "DELETE FROM FocusSessions WHERE SessionId = @id AND EndedUtc IS NULL;",
                    Database.Param("@id", sessionId));
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not discard a focus session.", ex);
            }
        }

        internal List<FocusSessionItem> Between(DateTime fromUtc, DateTime toUtc)
        {
            var items = new List<FocusSessionItem>();
            if (_database == null || !_database.IsAvailable) return items;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT f.SessionId, f.StartedUtc, f.EndedUtc, f.PlannedSeconds, f.ActualSeconds,
                             f.SessionType, f.IsCompleted, f.TaskId, t.Title AS TaskTitle
                      FROM FocusSessions f
                      LEFT JOIN Tasks t ON t.TaskId = f.TaskId
                      WHERE f.EndedUtc IS NOT NULL AND f.StartedUtc >= @from AND f.StartedUtc < @to
                      ORDER BY f.StartedUtc",
                    Database.Param("@from", ModuleUtil.Iso(fromUtc)),
                    Database.Param("@to", ModuleUtil.Iso(toUtc))))
                {
                    items.Add(Read(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load focus sessions for a date range.", ex);
            }

            return items;
        }

        internal List<FocusSessionItem> Recent(int limit)
        {
            var items = new List<FocusSessionItem>();
            if (_database == null || !_database.IsAvailable) return items;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT f.SessionId, f.StartedUtc, f.EndedUtc, f.PlannedSeconds, f.ActualSeconds,
                             f.SessionType, f.IsCompleted, f.TaskId, t.Title AS TaskTitle
                      FROM FocusSessions f
                      LEFT JOIN Tasks t ON t.TaskId = f.TaskId
                      WHERE f.EndedUtc IS NOT NULL
                      ORDER BY f.StartedUtc DESC LIMIT @limit",
                    Database.Param("@limit", Math.Max(1, limit))))
                {
                    items.Add(Read(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load focus history.", ex);
            }

            return items;
        }

        internal FocusStats GetStats()
        {
            var stats = new FocusStats();
            if (_database == null || !_database.IsAvailable) return stats;

            DateTime startToday = DateTime.Today.ToUniversalTime();
            DateTime startWindow = DateTime.Today.AddDays(-6).ToUniversalTime();

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    @"SELECT StartedUtc, ActualSeconds, IsCompleted FROM FocusSessions
                      WHERE StartedUtc >= @start",
                    Database.Param("@start", ModuleUtil.Iso(startWindow)));

                var daily = new int[7];

                foreach (Dictionary<string, object> row in rows)
                {
                    DateTime? started = ModuleUtil.FromIso(row["StartedUtc"]);
                    if (!started.HasValue) continue;

                    int seconds = Convert.ToInt32(row["ActualSeconds"], CultureInfo.InvariantCulture);
                    if (seconds <= 0) continue;

                    DateTime local = started.Value.ToLocalTime();

                    if (started.Value >= startToday)
                    {
                        stats.TodaySessions++;
                        stats.TodaySeconds += seconds;
                    }

                    int index = (DateTime.Today - local.Date).Days;
                    if (index >= 0 && index < 7) daily[index] += seconds;
                }

                for (int i = 0; i < 7; i++)
                {
                    stats.DailySecondsOldestFirst[i] = daily[6 - i];
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load focus statistics.", ex);
            }

            return stats;
        }

        private static FocusSessionItem Read(Dictionary<string, object> row)
        {
            return new FocusSessionItem
            {
                Id = row["SessionId"] as string,
                StartedUtc = ModuleUtil.FromIso(row["StartedUtc"]) ?? DateTime.UtcNow,
                EndedUtc = ModuleUtil.FromIso(row["EndedUtc"]),
                PlannedSeconds = Convert.ToInt32(row["PlannedSeconds"], CultureInfo.InvariantCulture),
                ActualSeconds = Convert.ToInt32(row["ActualSeconds"], CultureInfo.InvariantCulture),
                SessionType = row["SessionType"] as string ?? "focus",
                IsCompleted = Convert.ToInt64(row["IsCompleted"], CultureInfo.InvariantCulture) != 0,
                TaskId = row["TaskId"] as string,
                TaskTitle = row["TaskTitle"] as string,
            };
        }
    }

    internal sealed class FocusStats
    {
        internal int TodaySessions { get; set; }

        internal int TodaySeconds { get; set; }

        internal int[] DailySecondsOldestFirst { get; } = new int[7];
    }
}
