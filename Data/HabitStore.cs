using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class HabitItem
    {
        internal string Id { get; set; }

        internal string Name { get; set; } = string.Empty;

        internal string Description { get; set; }

        internal string TrackingType { get; set; } = "boolean";

        internal double TargetValue { get; set; } = 1;

        internal string Unit { get; set; }

        internal int SortOrder { get; set; }

        internal double TodayValue { get; set; }

        internal bool TodayCompleted { get; set; }

        internal int CurrentStreak { get; set; }

        internal int BestStreak { get; set; }

        internal bool[] WeekCompleted { get; } = new bool[7];

        internal bool IsBoolean => TrackingType == "boolean";

        internal bool IsDuration => TrackingType == "duration";

        internal string TypeName => IsBoolean ? "Yes / no" : IsDuration ? "Duration" : "Counter";

        internal string TargetText => IsBoolean
            ? (TodayCompleted ? "Completed today" : "Not done yet")
            : FormatValue(TodayValue) + " / " + FormatValue(TargetValue)
                + (string.IsNullOrWhiteSpace(Unit) ? string.Empty : " " + Unit);

        internal static string FormatValue(double value)
        {
            return value == Math.Floor(value)
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class HabitStore
    {
        private const string BaseSelect = @"
SELECT HabitId, Name, Description, TrackingType, TargetValue, Unit, SortOrder
FROM Habits";

        private readonly Database _database;

        internal HabitStore(Database database)
        {
            _database = database;
        }

        internal void EnsureSeeded()
        {
            if (_database == null || !_database.IsAvailable) return;

            try
            {
                long count = Convert.ToInt64(
                    _database.Scalar("SELECT COUNT(*) FROM Habits;"), CultureInfo.InvariantCulture);
                if (count > 0) return;

                var seeds = new (string Name, string Type, double Target, string Unit, string Description)[]
                {
                    ("Drink water", "counter", 8, "glasses", "Eight glasses across the day."),
                    ("Exercise", "duration", 30, "min", "Movement that raises your heart rate."),
                    ("Read", "duration", 20, "min", "Fifteen to thirty minutes of reading."),
                    ("Meditate", "duration", 10, "min", "A quiet breathing or mindfulness break."),
                    ("Walk outside", "duration", 20, "min", "A short walk away from the desk."),
                    ("Journal", "boolean", 1, null, "Write a few lines about the day."),
                    ("No sugar", "boolean", 1, null, "Skip added sugar for the day."),
                    ("Stretch", "boolean", 1, null, "Five minutes of stretching."),
                    ("Vitamins", "boolean", 1, null, "Take your daily vitamins."),
                    ("Eat vegetables", "counter", 3, "servings", "Three servings of vegetables."),
                };

                int order = 0;
                foreach ((string name, string type, double target, string unit, string description) in seeds)
                {
                    AddInternal(new HabitItem
                    {
                        Name = name,
                        Description = description,
                        TrackingType = type,
                        TargetValue = target,
                        Unit = unit,
                        SortOrder = order++,
                    });
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not seed habits.", ex);
            }
        }

        internal List<HabitItem> Load()
        {
            var habits = new List<HabitItem>();
            if (_database == null || !_database.IsAvailable) return habits;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    BaseSelect + " WHERE IsArchived = 0 ORDER BY SortOrder, Name"))
                {
                    habits.Add(ReadHabit(row));
                }

                if (habits.Count == 0) return habits;

                DateTime today = DateTime.Today;
                string todayKey = DayKey(today);

                List<Dictionary<string, object>> logs = _database.Query(
                    @"SELECT HabitId, LogDate, Value, IsCompleted FROM HabitLogs
                      WHERE LogDate >= @start AND LogDate <= @end",
                    Database.Param("@start", DayKey(today.AddDays(-399))),
                    Database.Param("@end", todayKey));

                var completedDates = new Dictionary<string, HashSet<DateTime>>(StringComparer.Ordinal);
                var todayValues = new Dictionary<string, double>(StringComparer.Ordinal);
                var todayDone = new Dictionary<string, bool>(StringComparer.Ordinal);

                foreach (Dictionary<string, object> row in logs)
                {
                    string habitId = row["HabitId"] as string;
                    DateTime? date = ParseDayKey(row["LogDate"]);
                    if (habitId == null || !date.HasValue) continue;

                    bool done = Convert.ToInt64(row["IsCompleted"], CultureInfo.InvariantCulture) != 0;
                    double value = Convert.ToDouble(row["Value"], CultureInfo.InvariantCulture);

                    if (done)
                    {
                        if (!completedDates.TryGetValue(habitId, out HashSet<DateTime> set))
                        {
                            set = new HashSet<DateTime>();
                            completedDates[habitId] = set;
                        }
                        set.Add(date.Value.Date);
                    }

                    if (date.Value.Date == today)
                    {
                        todayValues[habitId] = value;
                        todayDone[habitId] = done;
                    }
                }

                foreach (HabitItem habit in habits)
                {
                    if (todayValues.TryGetValue(habit.Id, out double value)) habit.TodayValue = value;
                    if (todayDone.TryGetValue(habit.Id, out bool done)) habit.TodayCompleted = done;

                    completedDates.TryGetValue(habit.Id, out HashSet<DateTime> dates);
                    dates ??= new HashSet<DateTime>();

                    for (int i = 0; i < 7; i++)
                    {
                        habit.WeekCompleted[i] = dates.Contains(today.AddDays(i - 6));
                    }

                    DateTime cursor = dates.Contains(today) ? today : today.AddDays(-1);
                    int streak = 0;
                    while (dates.Contains(cursor))
                    {
                        streak++;
                        cursor = cursor.AddDays(-1);
                    }
                    habit.CurrentStreak = streak;

                    int best = 0;
                    int run = 0;
                    DateTime? previous = null;
                    foreach (DateTime date in dates.OrderBy(d => d))
                    {
                        run = previous.HasValue && date == previous.Value.AddDays(1) ? run + 1 : 1;
                        if (run > best) best = run;
                        previous = date;
                    }
                    habit.BestStreak = best;
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load habits.", ex);
            }

            return habits;
        }

        internal HabitItem Get(string habitId)
        {
            if (string.IsNullOrWhiteSpace(habitId) || _database == null || !_database.IsAvailable) return null;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    BaseSelect + " WHERE HabitId = @id", Database.Param("@id", habitId));
                return rows.Count == 0 ? null : ReadHabit(rows[0]);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load a habit.", ex);
                return null;
            }
        }

        internal string Add(HabitItem habit)
        {
            if (habit == null || string.IsNullOrWhiteSpace(habit.Name)
                || _database == null || !_database.IsAvailable)
            {
                return null;
            }

            try
            {
                habit.SortOrder = NextSortOrder();
                return AddInternal(habit);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not add a habit.", ex);
                return null;
            }
        }

        internal bool Update(HabitItem habit)
        {
            if (habit == null || string.IsNullOrWhiteSpace(habit.Id)
                || _database == null || !_database.IsAvailable)
            {
                return false;
            }

            try
            {
                return _database.Execute(
                    @"UPDATE Habits SET Name = @name, Description = @description,
                        TrackingType = @type, TargetValue = @target, Unit = @unit,
                        UpdatedUtc = @now
                      WHERE HabitId = @id;",
                    Database.Param("@name", habit.Name.Trim()),
                    Database.Param("@description", string.IsNullOrWhiteSpace(habit.Description) ? null : habit.Description.Trim()),
                    Database.Param("@type", NormalizeType(habit.TrackingType)),
                    Database.Param("@target", Math.Max(0.01, habit.TargetValue)),
                    Database.Param("@unit", string.IsNullOrWhiteSpace(habit.Unit) ? null : habit.Unit.Trim()),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", habit.Id)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not update a habit.", ex);
                return false;
            }
        }

        internal bool Delete(string habitId)
        {
            if (string.IsNullOrWhiteSpace(habitId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute(
                    "DELETE FROM Habits WHERE HabitId = @id;",
                    Database.Param("@id", habitId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not delete a habit.", ex);
                return false;
            }
        }

        internal bool ToggleToday(string habitId)
        {
            HabitItem habit = Get(habitId);
            if (habit == null || _database == null || !_database.IsAvailable) return false;

            double current = TodayValue(habitId);
            bool completed = IsTodayCompleted(habitId);

            return completed
                ? Upsert(habitId, 0, false)
                : Upsert(habitId, Math.Max(1, habit.TargetValue), true);
        }

        internal bool AddDelta(string habitId, double direction)
        {
            HabitItem habit = Get(habitId);
            if (habit == null || direction == 0) return false;

            double current = TodayValue(habitId);
            double step = habit.IsDuration ? 5 : 1;
            double next = current + step * Math.Sign(direction);

            if (habit.IsBoolean)
            {
                return Upsert(habitId, next > 0 ? 1 : 0, next > 0);
            }

            next = Math.Max(0, Math.Min(habit.TargetValue, next));
            bool done = next >= habit.TargetValue - 0.0001;
            return Upsert(habitId, done ? habit.TargetValue : next, done);
        }

        internal bool SetToday(string habitId, double value)
        {
            HabitItem habit = Get(habitId);
            if (habit == null) return false;

            value = Math.Max(0, Math.Min(1_000_000, value));
            bool done = value >= habit.TargetValue - 0.0001;
            return Upsert(habitId, done ? habit.TargetValue : value, done);
        }

        private string AddInternal(HabitItem habit)
        {
            string id = ModuleUtil.NewId();
            string type = NormalizeType(habit.TrackingType);

            _database.Execute(
                @"INSERT INTO Habits (HabitId, Name, Description, TrackingType, TargetValue,
                    Unit, RecurrenceRule, IsPaused, IsArchived, SortOrder, CreatedUtc, UpdatedUtc)
                  VALUES (@id, @name, @description, @type, @target, @unit, 'daily', 0, 0, @order, @now, @now);",
                Database.Param("@id", id),
                Database.Param("@name", habit.Name.Trim()),
                Database.Param("@description", string.IsNullOrWhiteSpace(habit.Description) ? null : habit.Description.Trim()),
                Database.Param("@type", type),
                Database.Param("@target", type == "boolean" ? 1 : Math.Max(0.01, habit.TargetValue)),
                Database.Param("@unit", type == "boolean" || string.IsNullOrWhiteSpace(habit.Unit) ? null : habit.Unit.Trim()),
                Database.Param("@order", habit.SortOrder),
                Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)));

            return id;
        }

        private int NextSortOrder()
        {
            object value = _database.Scalar("SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Habits;");
            return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private double TodayValue(string habitId)
        {
            object value = _database.Scalar(
                "SELECT Value FROM HabitLogs WHERE HabitId = @id AND LogDate = @date;",
                Database.Param("@id", habitId),
                Database.Param("@date", DayKey(DateTime.Today)));

            return value == null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private bool IsTodayCompleted(string habitId)
        {
            object value = _database.Scalar(
                "SELECT IsCompleted FROM HabitLogs WHERE HabitId = @id AND LogDate = @date;",
                Database.Param("@id", habitId),
                Database.Param("@date", DayKey(DateTime.Today)));

            return value != null && Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
        }

        private bool Upsert(string habitId, double value, bool completed)
        {
            DateTime now = DateTime.UtcNow;

            try
            {
                return _database.Execute(
                    @"INSERT INTO HabitLogs (LogId, HabitId, LogDate, Value, IsCompleted,
                        Notes, CreatedUtc, UpdatedUtc)
                      VALUES (@id, @habit, @date, @value, @done, NULL, @now, @now)
                      ON CONFLICT(HabitId, LogDate) DO UPDATE SET
                        Value = excluded.Value, IsCompleted = excluded.IsCompleted,
                        UpdatedUtc = excluded.UpdatedUtc;",
                    Database.Param("@id", ModuleUtil.NewId()),
                    Database.Param("@habit", habitId),
                    Database.Param("@date", DayKey(DateTime.Today)),
                    Database.Param("@value", value),
                    Database.Param("@done", completed ? 1 : 0),
                    Database.Param("@now", ModuleUtil.Iso(now))) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not save a habit log.", ex);
                return false;
            }
        }

        private static HabitItem ReadHabit(Dictionary<string, object> row)
        {
            return new HabitItem
            {
                Id = row["HabitId"] as string,
                Name = row["Name"] as string ?? string.Empty,
                Description = row["Description"] as string,
                TrackingType = NormalizeType(row["TrackingType"] as string),
                TargetValue = Convert.ToDouble(row["TargetValue"], CultureInfo.InvariantCulture),
                Unit = row["Unit"] as string,
                SortOrder = Convert.ToInt32(row["SortOrder"], CultureInfo.InvariantCulture),
            };
        }

        private static string NormalizeType(string type)
        {
            return type == "counter" || type == "duration" ? type : "boolean";
        }

        private static string DayKey(DateTime localDate)
        {
            return localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static DateTime? ParseDayKey(object value)
        {
            string text = value as string;
            if (string.IsNullOrWhiteSpace(text)) return null;

            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : (DateTime?)null;
        }
    }
}
