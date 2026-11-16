using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class ReminderItem
    {
        internal string Id { get; set; }

        internal string Title { get; set; }

        internal string Notes { get; set; }

        internal DateTime DueUtc { get; set; }

        internal string Recurrence { get; set; } = "none";

        internal bool IsCompleted { get; set; }

        internal DateTime? CompletedUtc { get; set; }

        internal DateTime? SnoozeUntilUtc { get; set; }

        internal DateTime CreatedUtc { get; set; }

        internal DateTime EffectiveDueUtc => SnoozeUntilUtc ?? DueUtc;

        internal bool IsOverdue => !IsCompleted && EffectiveDueUtc < DateTime.UtcNow;
    }

    internal sealed class ReminderStore
    {
        private const string BaseSelect = @"
SELECT ReminderId, Title, Notes, DueUtc, RecurrenceRule, IsCompleted, CompletedUtc,
       SnoozeUntilUtc, CreatedUtc
FROM Reminders";

        private readonly Database _database;

        internal ReminderStore(Database database)
        {
            _database = database;
        }

        internal List<ReminderItem> Active()
        {
            return ReadAll(
                BaseSelect + " WHERE IsCompleted = 0 ORDER BY COALESCE(SnoozeUntilUtc, DueUtc)");
        }

        internal List<ReminderItem> History()
        {
            return ReadAll(
                BaseSelect + " WHERE IsCompleted = 1 ORDER BY CompletedUtc DESC LIMIT 100");
        }

        internal List<ReminderItem> Between(DateTime fromUtc, DateTime toUtc)
        {
            var items = new List<ReminderItem>();
            if (_database == null || !_database.IsAvailable) return items;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    BaseSelect + @" WHERE COALESCE(SnoozeUntilUtc, DueUtc) >= @from
                        AND COALESCE(SnoozeUntilUtc, DueUtc) < @to
                      ORDER BY COALESCE(SnoozeUntilUtc, DueUtc)",
                    Database.Param("@from", ModuleUtil.Iso(fromUtc)),
                    Database.Param("@to", ModuleUtil.Iso(toUtc))))
                {
                    items.Add(Read(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load reminders for a date range.", ex);
            }

            return items;
        }

        internal ReminderItem Get(string reminderId)
        {
            if (string.IsNullOrWhiteSpace(reminderId) || _database == null || !_database.IsAvailable) return null;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    BaseSelect + " WHERE ReminderId = @id", Database.Param("@id", reminderId));
                return rows.Count == 0 ? null : Read(rows[0]);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load a reminder.", ex);
                return null;
            }
        }

        internal string Add(ReminderItem reminder)
        {
            if (reminder == null || _database == null || !_database.IsAvailable) return null;
            if (string.IsNullOrWhiteSpace(reminder.Title)) return null;

            string id = ModuleUtil.NewId();

            try
            {
                _database.Execute(
                    @"INSERT INTO Reminders (ReminderId, Title, Notes, DueUtc, RecurrenceRule,
                        IsCompleted, SnoozeUntilUtc, CreatedUtc, UpdatedUtc)
                      VALUES (@id, @title, @notes, @due, @recurrence, 0, NULL, @now, @now);",
                    Database.Param("@id", id),
                    Database.Param("@title", reminder.Title.Trim()),
                    Database.Param("@notes", string.IsNullOrWhiteSpace(reminder.Notes) ? null : reminder.Notes.Trim()),
                    Database.Param("@due", ModuleUtil.Iso(reminder.DueUtc)),
                    Database.Param("@recurrence", string.IsNullOrWhiteSpace(reminder.Recurrence) ? "none" : reminder.Recurrence),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)));

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not add a reminder.", ex);
                return null;
            }
        }

        internal bool Update(ReminderItem reminder)
        {
            if (reminder == null || string.IsNullOrWhiteSpace(reminder.Id)
                || _database == null || !_database.IsAvailable)
            {
                return false;
            }

            try
            {
                return _database.Execute(
                    @"UPDATE Reminders SET Title = @title, Notes = @notes, DueUtc = @due,
                        RecurrenceRule = @recurrence, SnoozeUntilUtc = NULL, UpdatedUtc = @now
                      WHERE ReminderId = @id;",
                    Database.Param("@title", reminder.Title.Trim()),
                    Database.Param("@notes", string.IsNullOrWhiteSpace(reminder.Notes) ? null : reminder.Notes.Trim()),
                    Database.Param("@due", ModuleUtil.Iso(reminder.DueUtc)),
                    Database.Param("@recurrence", string.IsNullOrWhiteSpace(reminder.Recurrence) ? "none" : reminder.Recurrence),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", reminder.Id)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not update a reminder.", ex);
                return false;
            }
        }

        internal bool SetCompleted(string reminderId)
        {
            ReminderItem reminder = Get(reminderId);
            if (reminder == null || reminder.IsCompleted || _database == null || !_database.IsAvailable)
            {
                return false;
            }

            DateTime now = DateTime.UtcNow;

            try
            {
                _database.Execute(
                    "UPDATE Reminders SET IsCompleted = 1, CompletedUtc = @now, UpdatedUtc = @now WHERE ReminderId = @id;",
                    Database.Param("@now", ModuleUtil.Iso(now)),
                    Database.Param("@id", reminderId));

                DateTime? next = ModuleUtil.NextOccurrence(reminder.Recurrence, reminder.EffectiveDueUtc);
                if (next.HasValue)
                {
                    var repeat = new ReminderItem
                    {
                        Title = reminder.Title,
                        Notes = reminder.Notes,
                        DueUtc = next.Value,
                        Recurrence = reminder.Recurrence,
                    };
                    Add(repeat);
                }

                return true;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not complete a reminder.", ex);
                return false;
            }
        }

        internal bool Snooze(string reminderId, int minutes)
        {
            ReminderItem reminder = Get(reminderId);
            if (reminder == null || reminder.IsCompleted || _database == null || !_database.IsAvailable)
            {
                return false;
            }

            DateTime baseTime = reminder.EffectiveDueUtc > DateTime.UtcNow
                ? reminder.EffectiveDueUtc
                : DateTime.UtcNow;

            try
            {
                return _database.Execute(
                    "UPDATE Reminders SET SnoozeUntilUtc = @until, UpdatedUtc = @now WHERE ReminderId = @id;",
                    Database.Param("@until", ModuleUtil.Iso(baseTime.AddMinutes(Math.Max(1, minutes)))),
                    Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)),
                    Database.Param("@id", reminderId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not snooze a reminder.", ex);
                return false;
            }
        }

        internal bool Delete(string reminderId)
        {
            if (string.IsNullOrWhiteSpace(reminderId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute(
                    "DELETE FROM Reminders WHERE ReminderId = @id;",
                    Database.Param("@id", reminderId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not delete a reminder.", ex);
                return false;
            }
        }

        private List<ReminderItem> ReadAll(string sql)
        {
            var items = new List<ReminderItem>();
            if (_database == null || !_database.IsAvailable) return items;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(sql))
                {
                    items.Add(Read(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not query reminders.", ex);
            }

            return items;
        }

        private static ReminderItem Read(Dictionary<string, object> row)
        {
            return new ReminderItem
            {
                Id = row["ReminderId"] as string,
                Title = row["Title"] as string ?? string.Empty,
                Notes = row["Notes"] as string,
                DueUtc = ModuleUtil.FromIso(row["DueUtc"]) ?? DateTime.UtcNow,
                Recurrence = row["RecurrenceRule"] as string ?? "none",
                IsCompleted = Convert.ToInt64(row["IsCompleted"], CultureInfo.InvariantCulture) != 0,
                CompletedUtc = ModuleUtil.FromIso(row["CompletedUtc"]),
                SnoozeUntilUtc = ModuleUtil.FromIso(row["SnoozeUntilUtc"]),
                CreatedUtc = ModuleUtil.FromIso(row["CreatedUtc"]) ?? DateTime.UtcNow,
            };
        }
    }
}
