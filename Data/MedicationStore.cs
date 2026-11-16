using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class MedicationSchedule
    {
        internal string Id { get; set; }

        internal string TimeOfDay { get; set; } = "08:00";

        internal string DaysOfWeek { get; set; } = "0,1,2,3,4,5,6";

        internal bool ReminderEnabled { get; set; } = true;

        internal bool AppliesOn(DayOfWeek day)
        {
            string token = ((int)day).ToString(CultureInfo.InvariantCulture);
            return DaysOfWeek.Split(',').Any(part => part.Trim() == token);
        }

        internal TimeSpan? Time()
        {
            return TimeSpan.TryParseExact(TimeOfDay, new[] { @"h\:mm", @"hh\:mm" },
                CultureInfo.InvariantCulture, out TimeSpan parsed)
                ? parsed
                : (TimeSpan?)null;
        }
    }

    internal sealed class MedicationItem
    {
        internal string Id { get; set; }

        internal string Name { get; set; } = string.Empty;

        internal string Dosage { get; set; }

        internal string Notes { get; set; }

        internal DateTime? StartDate { get; set; }

        internal DateTime? EndDate { get; set; }

        internal DateTime CreatedUtc { get; set; }

        internal List<MedicationSchedule> Schedules { get; } = new List<MedicationSchedule>();

        internal string ScheduleSummary
        {
            get
            {
                if (Schedules.Count == 0) return "No times yet";

                string times = string.Join(", ", Schedules.Select(schedule => schedule.TimeOfDay));
                string days = MedicationDays.Label(Schedules[0].DaysOfWeek);
                return times + "  ·  " + days;
            }
        }
    }

    internal sealed class MedicationDose
    {
        internal string LogId { get; set; }

        internal string MedicationId { get; set; }

        internal string MedicationName { get; set; } = string.Empty;

        internal string Dosage { get; set; }

        internal string ScheduleId { get; set; }

        internal DateTime ScheduledUtc { get; set; }

        internal string Status { get; set; } = "pending";

        internal DateTime? TakenUtc { get; set; }

        internal DateTime? SnoozeUntilUtc { get; set; }

        internal DateTime EffectiveUtc => SnoozeUntilUtc ?? ScheduledUtc;

        internal bool IsActionable => Status == "pending" || Status == "snoozed";

        internal bool IsOverdue => IsActionable && EffectiveUtc < DateTime.UtcNow;

        internal DateTime LocalTime => EffectiveUtc.ToLocalTime();

        internal string TimeLabel => LocalTime.ToString("HH:mm");
    }

    internal sealed class MedicationOccurrence
    {
        internal DateTime LocalTime { get; set; }

        internal string MedicationName { get; set; } = string.Empty;

        internal string Dosage { get; set; }

        internal string Status { get; set; } = "planned";

        internal bool IsDone => Status == "taken" || Status == "skipped";
    }

    internal static class MedicationDays
    {
        internal const string EveryDay = "0,1,2,3,4,5,6";

        internal const string Weekdays = "1,2,3,4,5";

        internal const string Weekends = "0,6";

        internal static string Label(string daysOfWeek)
        {
            switch ((daysOfWeek ?? string.Empty).Trim())
            {
                case EveryDay: return "Every day";
                case Weekdays: return "Weekdays";
                case Weekends: return "Weekends";
                default: return "Selected days";
            }
        }
    }

    internal sealed class MedicationStore
    {
        private const string DoseSelect = @"
SELECT l.LogId, l.MedicationId, l.ScheduleId, l.ScheduledUtc, l.Status, l.TakenUtc,
       l.SnoozeUntilUtc, m.Name, m.Dosage
FROM MedicationLogs l
JOIN Medications m ON m.MedicationId = l.MedicationId";

        private readonly Database _database;

        internal MedicationStore(Database database)
        {
            _database = database;
        }

        internal List<MedicationItem> Load()
        {
            var items = new List<MedicationItem>();
            if (_database == null || !_database.IsAvailable) return items;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT MedicationId, Name, Dosage, Notes, StartDate, EndDate, CreatedUtc
                      FROM Medications
                      WHERE IsArchived = 0
                      ORDER BY Name"))
                {
                    items.Add(ReadMedication(row));
                }

                if (items.Count == 0) return items;

                var byId = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT MedicationId, ScheduleId, TimeOfDay, DaysOfWeek, ReminderEnabled
                      FROM MedicationSchedules
                      ORDER BY TimeOfDay"))
                {
                    string medicationId = row["MedicationId"] as string;
                    if (medicationId == null || !byId.TryGetValue(medicationId, out MedicationItem medication))
                    {
                        continue;
                    }

                    medication.Schedules.Add(new MedicationSchedule
                    {
                        Id = row["ScheduleId"] as string,
                        TimeOfDay = row["TimeOfDay"] as string ?? "08:00",
                        DaysOfWeek = row["DaysOfWeek"] as string ?? MedicationDays.EveryDay,
                        ReminderEnabled = Convert.ToInt64(row["ReminderEnabled"], CultureInfo.InvariantCulture) != 0,
                    });
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load medications.", ex);
            }

            return items;
        }

        internal MedicationItem Get(string medicationId)
        {
            if (string.IsNullOrWhiteSpace(medicationId) || _database == null || !_database.IsAvailable) return null;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    @"SELECT MedicationId, Name, Dosage, Notes, StartDate, EndDate, CreatedUtc
                      FROM Medications WHERE MedicationId = @id",
                    Database.Param("@id", medicationId));

                if (rows.Count == 0) return null;

                MedicationItem item = ReadMedication(rows[0]);
                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT MedicationId, ScheduleId, TimeOfDay, DaysOfWeek, ReminderEnabled
                      FROM MedicationSchedules WHERE MedicationId = @id ORDER BY TimeOfDay",
                    Database.Param("@id", medicationId)))
                {
                    item.Schedules.Add(new MedicationSchedule
                    {
                        Id = row["ScheduleId"] as string,
                        TimeOfDay = row["TimeOfDay"] as string ?? "08:00",
                        DaysOfWeek = row["DaysOfWeek"] as string ?? MedicationDays.EveryDay,
                        ReminderEnabled = Convert.ToInt64(row["ReminderEnabled"], CultureInfo.InvariantCulture) != 0,
                    });
                }

                return item;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load a medication.", ex);
                return null;
            }
        }

        internal string Save(MedicationItem medication, IList<string> times, string daysOfWeek)
        {
            if (medication == null || string.IsNullOrWhiteSpace(medication.Name)
                || _database == null || !_database.IsAvailable)
            {
                return null;
            }

            DateTime now = DateTime.UtcNow;
            string id = medication.Id;

            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = ModuleUtil.NewId();
                    _database.Execute(
                        @"INSERT INTO Medications (MedicationId, Name, Dosage, Notes, StartDate,
                            EndDate, IsArchived, CreatedUtc, UpdatedUtc)
                          VALUES (@id, @name, @dosage, @notes, NULL, NULL, 0, @now, @now);",
                        Database.Param("@id", id),
                        Database.Param("@name", medication.Name.Trim()),
                        Database.Param("@dosage", Clean(medication.Dosage)),
                        Database.Param("@notes", Clean(medication.Notes)),
                        Database.Param("@now", ModuleUtil.Iso(now)));
                }
                else
                {
                    _database.Execute(
                        @"UPDATE Medications SET Name = @name, Dosage = @dosage, Notes = @notes,
                            UpdatedUtc = @now WHERE MedicationId = @id;",
                        Database.Param("@name", medication.Name.Trim()),
                        Database.Param("@dosage", Clean(medication.Dosage)),
                        Database.Param("@notes", Clean(medication.Notes)),
                        Database.Param("@now", ModuleUtil.Iso(now)),
                        Database.Param("@id", id));
                }

                _database.Execute(
                    "DELETE FROM MedicationSchedules WHERE MedicationId = @id;",
                    Database.Param("@id", id));

                foreach (string time in times.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
                {
                    _database.Execute(
                        @"INSERT INTO MedicationSchedules (ScheduleId, MedicationId, TimeOfDay,
                            DaysOfWeek, ReminderEnabled, CreatedUtc)
                          VALUES (@schedule, @medication, @time, @days, 1, @now);",
                        Database.Param("@schedule", ModuleUtil.NewId()),
                        Database.Param("@medication", id),
                        Database.Param("@time", time),
                        Database.Param("@days", string.IsNullOrWhiteSpace(daysOfWeek)
                            ? MedicationDays.EveryDay
                            : daysOfWeek),
                        Database.Param("@now", ModuleUtil.Iso(now)));
                }

                return id;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not save a medication.", ex);
                return null;
            }
        }

        internal bool Delete(string medicationId)
        {
            if (string.IsNullOrWhiteSpace(medicationId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                return _database.Execute(
                    "DELETE FROM Medications WHERE MedicationId = @id;",
                    Database.Param("@id", medicationId)) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not delete a medication.", ex);
                return false;
            }
        }

        internal void EnsureDoses()
        {
            if (_database == null || !_database.IsAvailable) return;

            try
            {
                DateTime today = DateTime.Today;
                DateTime windowStart = today.AddDays(-6);

                foreach (MedicationItem medication in Load())
                {
                    DateTime lower = windowStart;
                    DateTime createdDate = medication.CreatedUtc.ToLocalTime().Date;
                    if (createdDate > lower) lower = createdDate;

                    if (medication.StartDate.HasValue && medication.StartDate.Value.Date > lower)
                    {
                        lower = medication.StartDate.Value.Date;
                    }

                    for (DateTime date = lower; date <= today; date = date.AddDays(1))
                    {
                        if (medication.EndDate.HasValue && date > medication.EndDate.Value.Date) break;

                        foreach (MedicationSchedule schedule in medication.Schedules)
                        {
                            if (!schedule.ReminderEnabled || !schedule.AppliesOn(date.DayOfWeek)) continue;

                            TimeSpan? time = schedule.Time();
                            if (!time.HasValue) continue;

                            DateTime scheduledUtc = date.Add(time.Value).ToUniversalTime();
                            if (scheduledUtc < medication.CreatedUtc) continue;

                            int exists = Convert.ToInt32(_database.Scalar(
                                @"SELECT COUNT(*) FROM MedicationLogs
                                  WHERE MedicationId = @medication AND ScheduleId = @schedule
                                    AND ScheduledUtc = @scheduled;",
                                Database.Param("@medication", medication.Id),
                                Database.Param("@schedule", schedule.Id),
                                Database.Param("@scheduled", ModuleUtil.Iso(scheduledUtc))),
                                CultureInfo.InvariantCulture);

                            if (exists > 0) continue;

                            _database.Execute(
                                @"INSERT INTO MedicationLogs (LogId, MedicationId, ScheduleId,
                                    ScheduledUtc, Status, TakenUtc, SnoozeUntilUtc, Notes, CreatedUtc)
                                  VALUES (@id, @medication, @schedule, @scheduled, 'pending',
                                    NULL, NULL, NULL, @now);",
                                Database.Param("@id", ModuleUtil.NewId()),
                                Database.Param("@medication", medication.Id),
                                Database.Param("@schedule", schedule.Id),
                                Database.Param("@scheduled", ModuleUtil.Iso(scheduledUtc)),
                                Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)));
                        }
                    }
                }

                MarkMissed();
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not prepare medication doses.", ex);
            }
        }

        internal void MarkMissed()
        {
            if (_database == null || !_database.IsAvailable) return;

            try
            {
                _database.Execute(
                    @"UPDATE MedicationLogs SET Status = 'missed'
                      WHERE Status IN ('pending', 'snoozed')
                        AND COALESCE(SnoozeUntilUtc, ScheduledUtc) < @cutoff;",
                    Database.Param("@cutoff", ModuleUtil.Iso(DateTime.UtcNow.AddHours(-2))));
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not mark missed medication doses.", ex);
            }
        }

        internal List<MedicationDose> Between(DateTime fromUtc, DateTime toUtc)
        {
            return ReadDoses(fromUtc, toUtc);
        }

        internal List<MedicationOccurrence> Occurrences(DateTime fromLocalDate, DateTime toLocalDate)
        {
            var occurrences = new List<MedicationOccurrence>();
            if (_database == null || !_database.IsAvailable) return occurrences;

            try
            {
                DateTime rangeStart = fromLocalDate.Date.ToUniversalTime();
                DateTime rangeEnd = toLocalDate.Date.AddDays(1).ToUniversalTime();
                var statuses = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT MedicationId, ScheduleId, ScheduledUtc, Status FROM MedicationLogs
                      WHERE ScheduledUtc >= @from AND ScheduledUtc < @to",
                    Database.Param("@from", ModuleUtil.Iso(rangeStart)),
                    Database.Param("@to", ModuleUtil.Iso(rangeEnd))))
                {
                    string key = (row["MedicationId"] as string) + "|" + (row["ScheduleId"] as string)
                        + "|" + (row["ScheduledUtc"] as string);
                    statuses[key] = row["Status"] as string ?? "pending";
                }

                foreach (MedicationItem medication in Load())
                {
                    for (DateTime date = fromLocalDate.Date; date <= toLocalDate.Date; date = date.AddDays(1))
                    {
                        if (medication.StartDate.HasValue && date < medication.StartDate.Value.Date) continue;
                        if (medication.EndDate.HasValue && date > medication.EndDate.Value.Date) continue;

                        foreach (MedicationSchedule schedule in medication.Schedules)
                        {
                            if (!schedule.AppliesOn(date.DayOfWeek)) continue;

                            TimeSpan? time = schedule.Time();
                            if (!time.HasValue) continue;

                            DateTime local = date.Add(time.Value);
                            string key = medication.Id + "|" + schedule.Id + "|"
                                + ModuleUtil.Iso(local.ToUniversalTime());

                            occurrences.Add(new MedicationOccurrence
                            {
                                LocalTime = local,
                                MedicationName = medication.Name,
                                Dosage = medication.Dosage,
                                Status = statuses.TryGetValue(key, out string status) ? status : "planned",
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not build medication occurrences.", ex);
            }

            return occurrences.OrderBy(occurrence => occurrence.LocalTime).ToList();
        }

        internal List<MedicationDose> Today()
        {
            DateTime start = DateTime.Today.ToUniversalTime();
            return ReadDoses(start, start.AddDays(1));
        }

        internal List<MedicationDose> LastWeek()
        {
            DateTime start = DateTime.Today.AddDays(-6).ToUniversalTime();
            return ReadDoses(start, start.AddDays(7));
        }

        internal MedicationDose GetDose(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId) || _database == null || !_database.IsAvailable) return null;

            try
            {
                List<Dictionary<string, object>> rows = _database.Query(
                    DoseSelect + " WHERE l.LogId = @id", Database.Param("@id", logId));
                return rows.Count == 0 ? null : ReadDose(rows[0]);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load a medication dose.", ex);
                return null;
            }
        }

        internal bool Take(string logId)
        {
            return Update(logId,
                "UPDATE MedicationLogs SET Status = 'taken', TakenUtc = @now, SnoozeUntilUtc = NULL WHERE LogId = @id;",
                Database.Param("@now", ModuleUtil.Iso(DateTime.UtcNow)));
        }

        internal bool Skip(string logId)
        {
            return Update(logId,
                "UPDATE MedicationLogs SET Status = 'skipped', SnoozeUntilUtc = NULL WHERE LogId = @id;");
        }

        internal bool Snooze(string logId, int minutes)
        {
            return Update(logId,
                @"UPDATE MedicationLogs SET Status = 'snoozed', SnoozeUntilUtc = @until
                  WHERE LogId = @id AND Status IN ('pending', 'snoozed');",
                Database.Param("@until", ModuleUtil.Iso(DateTime.UtcNow.AddMinutes(Math.Max(1, minutes)))));
        }

        private bool Update(string logId, string sql, params Microsoft.Data.Sqlite.SqliteParameter[] extra)
        {
            if (string.IsNullOrWhiteSpace(logId) || _database == null || !_database.IsAvailable) return false;

            try
            {
                var parameters = new List<Microsoft.Data.Sqlite.SqliteParameter>
                {
                    Database.Param("@id", logId),
                };
                parameters.AddRange(extra);

                return _database.Execute(sql, parameters.ToArray()) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not update a medication dose.", ex);
                return false;
            }
        }

        private List<MedicationDose> ReadDoses(DateTime fromUtc, DateTime toUtc)
        {
            var doses = new List<MedicationDose>();
            if (_database == null || !_database.IsAvailable) return doses;

            try
            {
                foreach (Dictionary<string, object> row in _database.Query(
                    DoseSelect + " WHERE l.ScheduledUtc >= @from AND l.ScheduledUtc < @to ORDER BY l.ScheduledUtc",
                    Database.Param("@from", ModuleUtil.Iso(fromUtc)),
                    Database.Param("@to", ModuleUtil.Iso(toUtc))))
                {
                    doses.Add(ReadDose(row));
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load medication doses.", ex);
            }

            return doses;
        }

        private static MedicationItem ReadMedication(Dictionary<string, object> row)
        {
            return new MedicationItem
            {
                Id = row["MedicationId"] as string,
                Name = row["Name"] as string ?? string.Empty,
                Dosage = row["Dosage"] as string,
                Notes = row["Notes"] as string,
                StartDate = ParseLocalDate(row["StartDate"]),
                EndDate = ParseLocalDate(row["EndDate"]),
                CreatedUtc = ModuleUtil.FromIso(row["CreatedUtc"]) ?? DateTime.UtcNow,
            };
        }

        private static MedicationDose ReadDose(Dictionary<string, object> row)
        {
            return new MedicationDose
            {
                LogId = row["LogId"] as string,
                MedicationId = row["MedicationId"] as string,
                MedicationName = row["Name"] as string ?? string.Empty,
                Dosage = row["Dosage"] as string,
                ScheduleId = row["ScheduleId"] as string,
                ScheduledUtc = ModuleUtil.FromIso(row["ScheduledUtc"]) ?? DateTime.UtcNow,
                Status = row["Status"] as string ?? "pending",
                TakenUtc = ModuleUtil.FromIso(row["TakenUtc"]),
                SnoozeUntilUtc = ModuleUtil.FromIso(row["SnoozeUntilUtc"]),
            };
        }

        private static DateTime? ParseLocalDate(object value)
        {
            string text = value as string;
            if (string.IsNullOrWhiteSpace(text)) return null;

            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : (DateTime?)null;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
