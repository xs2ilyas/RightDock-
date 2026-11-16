using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class LearningGoalItem
    {
        internal string Id { get; set; }

        internal string Title { get; set; }

        internal string Description { get; set; } = string.Empty;

        internal string Category { get; set; }

        internal double TargetValue { get; set; }

        internal string Unit { get; set; } = "minutes";

        internal double ProgressValue { get; set; }

        internal DateTime? DueDate { get; set; }

        internal bool IsCompleted { get; set; }

        internal DateTime CreatedUtc { get; set; }

        internal DateTime UpdatedUtc { get; set; }

        internal double ProgressFraction
        {
            get
            {
                if (IsCompleted) return 1;
                if (TargetValue <= 0) return 0;
                return Math.Max(0, Math.Min(1, ProgressValue / TargetValue));
            }
        }

        internal string ProgressText
        {
            get
            {
                string unit = string.IsNullOrWhiteSpace(Unit) ? string.Empty : " " + Unit;
                if (TargetValue <= 0) return FormatNumber(ProgressValue) + unit;

                return FormatNumber(ProgressValue) + " / " + FormatNumber(TargetValue) + unit;
            }
        }

        internal string DueText
        {
            get
            {
                if (!DueDate.HasValue) return string.Empty;

                DateTime due = DueDate.Value;
                return due.Year == DateTime.Today.Year
                    ? "Due " + due.ToString("MMM d")
                    : "Due " + due.ToString("MMM d, yyyy");
            }
        }

        internal static string FormatNumber(double value)
        {
            return Math.Abs(value - Math.Round(value)) < 0.05
                ? Math.Round(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class LearningSessionItem
    {
        internal string Id { get; set; }

        internal string GoalId { get; set; }

        internal string GoalTitle { get; set; }

        internal DateTime StartedUtc { get; set; }

        internal int DurationSeconds { get; set; }

        internal string Notes { get; set; }

        internal DateTime CreatedUtc { get; set; }
    }

    internal sealed class LearningStats
    {
        internal int ActiveGoals { get; set; }

        internal int CompletedGoals { get; set; }

        internal int WeekSessions { get; set; }

        internal int WeekSeconds { get; set; }

        internal int TotalSessions { get; set; }

        internal int TotalSeconds { get; set; }
    }

    internal sealed class LearningStore
    {
        private readonly Database _database;

        internal LearningStore(Database database)
        {
            _database = database;
        }

        internal List<LearningGoalItem> Query(string search, string category, string status)
        {
            var items = new List<LearningGoalItem>();
            if (_database == null || !_database.IsAvailable) return items;

            string filter = string.Empty;
            var parameters = new List<Microsoft.Data.Sqlite.SqliteParameter>();

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                filter += " WHERE IsCompleted = 0";
            }
            else if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                filter += " WHERE IsCompleted = 1";
            }
            else
            {
                filter += " WHERE 1 = 1";
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                filter += @" AND (Title LIKE @search ESCAPE '\'
                    OR IFNULL(Description, '') LIKE @search ESCAPE '\'
                    OR IFNULL(Category, '') LIKE @search ESCAPE '\')";
                parameters.Add(Database.Param("@search", "%" + ModuleUtil.EscapeLike(search.Trim()) + "%"));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                filter += " AND Category = @category COLLATE NOCASE";
                parameters.Add(Database.Param("@category", category.Trim()));
            }

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT GoalId, Title, Description, Category, TargetValue, Unit, ProgressValue,
                             DueDate, IsCompleted, CreatedUtc, UpdatedUtc
                      FROM LearningGoals" + filter + @"
                      ORDER BY IsCompleted ASC, UpdatedUtc DESC LIMIT 300",
                    parameters.ToArray()))
                {
                    items.Add(ReadGoal(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not query learning goals.", ex);
            }

            return items;
        }

        internal List<string> Categories()
        {
            var categories = new List<string>();
            if (_database == null || !_database.IsAvailable) return categories;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT DISTINCT Category FROM LearningGoals
                      WHERE Category IS NOT NULL AND TRIM(Category) <> ''
                      ORDER BY Category COLLATE NOCASE"))
                {
                    string category = row["Category"] as string;
                    if (!string.IsNullOrWhiteSpace(category)) categories.Add(category);
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load learning categories.", ex);
            }

            return categories;
        }

        internal LearningGoalItem Get(string goalId)
        {
            if (string.IsNullOrWhiteSpace(goalId) || _database == null || !_database.IsAvailable) return null;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    @"SELECT GoalId, Title, Description, Category, TargetValue, Unit, ProgressValue,
                             DueDate, IsCompleted, CreatedUtc, UpdatedUtc
                      FROM LearningGoals WHERE GoalId = @id",
                    Database.Param("@id", goalId));
                return rows.Count == 0 ? null : ReadGoal(rows[0]);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load a learning goal.", ex);
                return null;
            }
        }

        internal string Add(LearningGoalItem goal)
        {
            if (goal == null || string.IsNullOrWhiteSpace(goal.Title)
                || _database == null || !_database.IsAvailable)
            {
                return null;
            }

            string id = ModuleUtil.NewId();
            DateTime now = DateTime.UtcNow;

            try
            {
                _database.Execute(
                    @"INSERT INTO LearningGoals (GoalId, Title, Description, Category, TargetValue, Unit,
                        ProgressValue, DueDate, IsCompleted, CreatedUtc, UpdatedUtc)
                      VALUES (@id, @title, @description, @category, @target, @unit,
                        @progress, @due, 0, @now, @now);",
                    Database.Param("@id", id),
                    Database.Param("@title", goal.Title.Trim()),
                    Database.Param("@description", string.IsNullOrWhiteSpace(goal.Description) ? null : goal.Description.Trim()),
                    Database.Param("@category", string.IsNullOrWhiteSpace(goal.Category) ? null : goal.Category.Trim()),
                    Database.Param("@target", goal.TargetValue),
                    Database.Param("@unit", string.IsNullOrWhiteSpace(goal.Unit) ? null : goal.Unit.Trim()),
                    Database.Param("@progress", goal.ProgressValue),
                    Database.Param("@due", ToDate(goal.DueDate)),
                    Database.Param("@now", ModuleUtil.Iso(now)));

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not add a learning goal.", ex);
                return null;
            }
        }

        internal bool Update(LearningGoalItem goal)
        {
            if (goal == null || string.IsNullOrWhiteSpace(goal.Id) || string.IsNullOrWhiteSpace(goal.Title)
                || _database == null || !_database.IsAvailable)
            {
                return false;
            }

            try
            {
                return _database.Execute(
                    @"UPDATE LearningGoals SET Title = @title, Description = @description, Category = @category,
                        TargetValue = @target, Unit = @unit, ProgressValue = @progress, DueDate = @due,
                        IsCompleted = @completed, UpdatedUtc = @now
                      WHERE GoalId = @id;",
                    Database.Param("@title", goal.Title.Trim()),
                    Database.Param("@description", string.IsNullOrWhiteSpace(goal.Description) ? null : goal.Description.Trim()),
                    Database.Param("@category", string.IsNullOrWhiteSpace(goal.Category) ? null : goal.Category.Trim()),
                    Database.Param("@target", goal.TargetValue),
                    Database.Param("@unit", string.IsNullOrWhiteSpace(goal.Unit) ? null : goal.Unit.Trim()),
                    Database.Param("@progress", goal.ProgressValue),
                    Database.Param("@due", ToDate(goal.DueDate)),
                    Database.Param("@completed", goal.IsCompleted ? 1 : 0),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", goal.Id)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not update a learning goal.", ex);
                return false;
            }
        }

        internal bool SetCompleted(string goalId, bool completed)
        {
            if (string.IsNullOrWhiteSpace(goalId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute(
                    "UPDATE LearningGoals SET IsCompleted = @completed, UpdatedUtc = @now WHERE GoalId = @id;",
                    Database.Param("@completed", completed ? 1 : 0),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", goalId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not complete a learning goal.", ex);
                return false;
            }
        }

        internal bool Delete(string goalId)
        {
            if (string.IsNullOrWhiteSpace(goalId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute(
                    "DELETE FROM LearningGoals WHERE GoalId = @id;",
                    Database.Param("@id", goalId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not delete a learning goal.", ex);
                return false;
            }
        }

        internal string AddSession(string goalId, int durationSeconds, string notes, DateTime startedUtc)
        {
            if (_database == null || !_database.IsAvailable) return null;

            string id = ModuleUtil.NewId();

            try
            {
                _database.Execute(
                    @"INSERT INTO LearningSessions (SessionId, GoalId, StartedUtc, DurationSeconds, Notes, CreatedUtc)
                      VALUES (@id, @goal, @started, @duration, @notes, @created);",
                    Database.Param("@id", id),
                    Database.Param("@goal", string.IsNullOrWhiteSpace(goalId) ? null : goalId),
                    Database.Param("@started", ModuleUtil.Iso(startedUtc)),
                    Database.Param("@duration", Math.Max(0, durationSeconds)),
                    Database.Param("@notes", string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()),
                    Database.Param("@created", ModuleUtil.Iso(DateTime.UtcNow)));

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not log a learning session.", ex);
                return null;
            }
        }

        internal List<LearningSessionItem> RecentSessions(int limit, string goalId = null)
        {
            var items = new List<LearningSessionItem>();
            if (_database == null || !_database.IsAvailable) return items;

            string filter = string.IsNullOrWhiteSpace(goalId) ? " WHERE 1 = 1" : " WHERE s.GoalId = @goal";

            try
            {
                var parameters = new List<Microsoft.Data.Sqlite.SqliteParameter>
                {
                    Database.Param("@limit", Math.Max(1, limit)),
                };
                if (!string.IsNullOrWhiteSpace(goalId)) parameters.Add(Database.Param("@goal", goalId));

                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT s.SessionId, s.GoalId, s.StartedUtc, s.DurationSeconds, s.Notes, s.CreatedUtc,
                             g.Title AS GoalTitle
                      FROM LearningSessions s
                      LEFT JOIN LearningGoals g ON g.GoalId = s.GoalId" + filter + @"
                      ORDER BY s.StartedUtc DESC LIMIT @limit",
                    parameters.ToArray()))
                {
                    items.Add(new LearningSessionItem
                    {
                        Id = row["SessionId"] as string,
                        GoalId = row["GoalId"] as string,
                        GoalTitle = row["GoalTitle"] as string,
                        StartedUtc = ModuleUtil.FromIso(row["StartedUtc"]) ?? DateTime.UtcNow,
                        DurationSeconds = Convert.ToInt32(row["DurationSeconds"], CultureInfo.InvariantCulture),
                        Notes = row["Notes"] as string,
                        CreatedUtc = ModuleUtil.FromIso(row["CreatedUtc"]) ?? DateTime.UtcNow,
                    });
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load learning sessions.", ex);
            }

            return items;
        }

        internal LearningStats GetStats()
        {
            var stats = new LearningStats();
            if (_database == null || !_database.IsAvailable) return stats;

            DateTime today = DateTime.Today;
            DateTime startWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).ToUniversalTime();

            try
            {
                stats.ActiveGoals = Convert.ToInt32(
                    _database.Scalar("SELECT COUNT(*) FROM LearningGoals WHERE IsCompleted = 0;"),
                    CultureInfo.InvariantCulture);
                stats.CompletedGoals = Convert.ToInt32(
                    _database.Scalar("SELECT COUNT(*) FROM LearningGoals WHERE IsCompleted = 1;"),
                    CultureInfo.InvariantCulture);

                List<Dictionary<string, object>> rows = _database.Query(
                    @"SELECT COUNT(*) AS Sessions, IFNULL(SUM(DurationSeconds), 0) AS Seconds
                      FROM LearningSessions;");
                if (rows.Count > 0)
                {
                    stats.TotalSessions = Convert.ToInt32(rows[0]["Sessions"], CultureInfo.InvariantCulture);
                    stats.TotalSeconds = Convert.ToInt32(rows[0]["Seconds"], CultureInfo.InvariantCulture);
                }

                rows = _database.Query(
                    @"SELECT COUNT(*) AS Sessions, IFNULL(SUM(DurationSeconds), 0) AS Seconds
                      FROM LearningSessions WHERE StartedUtc >= @start;",
                    Database.Param("@start", ModuleUtil.Iso(startWeek)));
                if (rows.Count > 0)
                {
                    stats.WeekSessions = Convert.ToInt32(rows[0]["Sessions"], CultureInfo.InvariantCulture);
                    stats.WeekSeconds = Convert.ToInt32(rows[0]["Seconds"], CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load learning statistics.", ex);
            }

            return stats;
        }

        private static LearningGoalItem ReadGoal(Dictionary<string, object> row)
        {
            return new LearningGoalItem
            {
                Id = row["GoalId"] as string,
                Title = row["Title"] as string,
                Description = row["Description"] as string ?? string.Empty,
                Category = row["Category"] as string,
                TargetValue = Convert.ToDouble(row["TargetValue"], CultureInfo.InvariantCulture),
                Unit = row["Unit"] as string ?? string.Empty,
                ProgressValue = Convert.ToDouble(row["ProgressValue"], CultureInfo.InvariantCulture),
                DueDate = FromDate(row["DueDate"]),
                IsCompleted = Convert.ToInt64(row["IsCompleted"], CultureInfo.InvariantCulture) != 0,
                CreatedUtc = ModuleUtil.FromIso(row["CreatedUtc"]) ?? DateTime.UtcNow,
                UpdatedUtc = ModuleUtil.FromIso(row["UpdatedUtc"]) ?? DateTime.UtcNow,
            };
        }

        private static string ToDate(DateTime? date)
        {
            return date.HasValue ? date.Value.Date.ToString("yyyy-MM-dd", ModuleUtil.Invariant) : null;
        }

        private static DateTime? FromDate(object value)
        {
            string text = value as string;
            if (string.IsNullOrWhiteSpace(text)) return null;

            if (DateTime.TryParseExact(text, "yyyy-MM-dd", ModuleUtil.Invariant,
                    DateTimeStyles.None, out DateTime parsed))
            {
                return parsed.Date;
            }

            return ModuleUtil.FromIso(value)?.ToLocalTime().Date;
        }
    }
}
