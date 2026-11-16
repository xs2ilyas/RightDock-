using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class BreathingSessionItem
    {
        internal string Id { get; set; }

        internal string PatternKey { get; set; }

        internal string PatternName { get; set; }

        internal int Cycles { get; set; }

        internal int DurationSeconds { get; set; }

        internal DateTime StartedUtc { get; set; }

        internal DateTime? EndedUtc { get; set; }
    }

    internal sealed class BreathingStats
    {
        internal int TodaySessions { get; set; }

        internal int TodaySeconds { get; set; }

        internal int TotalSessions { get; set; }

        internal int TotalSeconds { get; set; }
    }

    internal sealed class BreathingStore
    {
        private readonly Database _database;

        internal BreathingStore(Database database)
        {
            _database = database;
        }

        internal string Add(string patternKey, string patternName, int cycles,
            int durationSeconds, DateTime startedUtc, DateTime endedUtc)
        {
            if (_database == null || !_database.IsAvailable) return null;
            if (string.IsNullOrWhiteSpace(patternKey)) return null;

            string id = ModuleUtil.NewId();

            try
            {
                _database.Execute(
                    @"INSERT INTO BreathingSessions (SessionId, PatternKey, PatternName, Cycles,
                        DurationSeconds, StartedUtc, EndedUtc, CreatedUtc)
                      VALUES (@id, @key, @name, @cycles, @duration, @started, @ended, @created);",
                    Database.Param("@id", id),
                    Database.Param("@key", patternKey),
                    Database.Param("@name", patternName ?? patternKey),
                    Database.Param("@cycles", Math.Max(0, cycles)),
                    Database.Param("@duration", Math.Max(0, durationSeconds)),
                    Database.Param("@started", ModuleUtil.Iso(startedUtc)),
                    Database.Param("@ended", ModuleUtil.Iso(endedUtc)),
                    Database.Param("@created", ModuleUtil.Iso(DateTime.UtcNow)));

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not save a breathing session.", ex);
                return null;
            }
        }

        internal List<BreathingSessionItem> Recent(int limit)
        {
            var items = new List<BreathingSessionItem>();
            if (_database == null || !_database.IsAvailable) return items;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT SessionId, PatternKey, PatternName, Cycles, DurationSeconds, StartedUtc, EndedUtc
                      FROM BreathingSessions ORDER BY StartedUtc DESC LIMIT @limit",
                    Database.Param("@limit", Math.Max(1, limit))))
                {
                    items.Add(new BreathingSessionItem
                    {
                        Id = row["SessionId"] as string,
                        PatternKey = row["PatternKey"] as string,
                        PatternName = row["PatternName"] as string,
                        Cycles = Convert.ToInt32(row["Cycles"], CultureInfo.InvariantCulture),
                        DurationSeconds = Convert.ToInt32(row["DurationSeconds"], CultureInfo.InvariantCulture),
                        StartedUtc = ModuleUtil.FromIso(row["StartedUtc"]) ?? DateTime.UtcNow,
                        EndedUtc = ModuleUtil.FromIso(row["EndedUtc"]),
                    });
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load breathing history.", ex);
            }

            return items;
        }

        internal BreathingStats GetStats()
        {
            var stats = new BreathingStats();
            if (_database == null || !_database.IsAvailable) return stats;

            try
            {
                DateTime startToday = DateTime.Today.ToUniversalTime();

                List<Dictionary<string, object>> rows = _database.Query(
                    @"SELECT COUNT(*) AS Sessions, IFNULL(SUM(DurationSeconds), 0) AS Seconds
                      FROM BreathingSessions;");
                if (rows.Count > 0)
                {
                    stats.TotalSessions = Convert.ToInt32(rows[0]["Sessions"], CultureInfo.InvariantCulture);
                    stats.TotalSeconds = Convert.ToInt32(rows[0]["Seconds"], CultureInfo.InvariantCulture);
                }

                rows = _database.Query(
                    @"SELECT COUNT(*) AS Sessions, IFNULL(SUM(DurationSeconds), 0) AS Seconds
                      FROM BreathingSessions WHERE StartedUtc >= @start;",
                    Database.Param("@start", ModuleUtil.Iso(startToday)));
                if (rows.Count > 0)
                {
                    stats.TodaySessions = Convert.ToInt32(rows[0]["Sessions"], CultureInfo.InvariantCulture);
                    stats.TodaySeconds = Convert.ToInt32(rows[0]["Seconds"], CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load breathing statistics.", ex);
            }

            return stats;
        }
    }
}
