using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Calendar
{
    internal enum CalendarEntryKind
    {
        Task,
        Reminder,
        Medication,
        Focus,
    }

    internal sealed class CalendarEntry
    {
        internal DateTime LocalTime { get; set; }

        internal CalendarEntryKind Kind { get; set; }

        internal string Title { get; set; } = string.Empty;

        internal string Detail { get; set; }

        internal bool IsDone { get; set; }

        internal bool IsOverdue { get; set; }

        internal string KindLabel
        {
            get
            {
                switch (Kind)
                {
                    case CalendarEntryKind.Task: return "Task";
                    case CalendarEntryKind.Reminder: return "Reminder";
                    case CalendarEntryKind.Medication: return "Medication";
                    default: return "Focus";
                }
            }
        }
    }

    internal sealed class CalendarModule : ProductivityModule
    {
        private ModuleContext _context;
        private TaskStore _tasks;
        private ReminderStore _reminders;
        private MedicationStore _medication;
        private FocusStore _focus;

        internal event Action Changed;

        internal DateTime VisibleMonth { get; private set; } =
            new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        internal DateTime SelectedDate { get; private set; } = DateTime.Today;

        public override string Id => "calendar";

        public override string Name => "Calendar";

        public override string Description => "Daily planner that aggregates tasks, reminders, medication and focus sessions.";

        public override string IconKey => "lucide:calendar-days";

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _tasks = new TaskStore(context.Database);
            _reminders = new ReminderStore(context.Database);
            _medication = new MedicationStore(context.Database);
            _focus = new FocusStore(context.Database);
        }

        public override void Activate()
        {
            ShowToday();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new CalendarCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new CalendarExpandedView(this);
        }

        internal DateTime MonthGridStart()
        {
            DateTime first = new DateTime(VisibleMonth.Year, VisibleMonth.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7;
            return first.AddDays(-offset);
        }

        internal List<CalendarEntry> LoadEntries(DateTime fromLocalDate, DateTime toLocalDate)
        {
            var entries = new List<CalendarEntry>();

            DateTime fromUtc = fromLocalDate.Date.ToUniversalTime();
            DateTime toUtc = toLocalDate.Date.AddDays(1).ToUniversalTime();
            DateTime now = DateTime.UtcNow;

            try
            {
                foreach (TaskItem task in _tasks?.DueBetween(fromUtc, toUtc) ?? new List<TaskItem>())
                {
                    DateTime local = (task.DueUtc ?? now).ToLocalTime();
                    string detail = "Task";
                    if (!string.IsNullOrWhiteSpace(task.CategoryName)) detail += " · " + task.CategoryName;
                    if (task.IsCompleted) detail += " · Completed";
                    else if (local < DateTime.Now) detail += " · Overdue";

                    entries.Add(new CalendarEntry
                    {
                        LocalTime = local,
                        Kind = CalendarEntryKind.Task,
                        Title = task.Title,
                        Detail = detail,
                        IsDone = task.IsCompleted,
                        IsOverdue = task.IsOverdue,
                    });
                }

                foreach (ReminderItem reminder in _reminders?.Between(fromUtc, toUtc) ?? new List<ReminderItem>())
                {
                    DateTime local = reminder.EffectiveDueUtc.ToLocalTime();
                    string detail = "Reminder";
                    if (reminder.Recurrence != "none")
                    {
                        detail += " · " + ModuleUtil.RecurrenceLabel(reminder.Recurrence);
                    }
                    if (reminder.IsCompleted) detail += " · Done";
                    else if (reminder.EffectiveDueUtc < now) detail += " · Overdue";

                    entries.Add(new CalendarEntry
                    {
                        LocalTime = local,
                        Kind = CalendarEntryKind.Reminder,
                        Title = reminder.Title,
                        Detail = detail,
                        IsDone = reminder.IsCompleted,
                        IsOverdue = reminder.IsOverdue,
                    });
                }

                foreach (MedicationOccurrence occurrence in _medication?.Occurrences(fromLocalDate, toLocalDate)
                    ?? new List<MedicationOccurrence>())
                {
                    string status = StatusLabel(occurrence.Status);
                    string detail = "Medication · " + status;
                    if (!string.IsNullOrWhiteSpace(occurrence.Dosage)) detail += " · " + occurrence.Dosage;

                    entries.Add(new CalendarEntry
                    {
                        LocalTime = occurrence.LocalTime,
                        Kind = CalendarEntryKind.Medication,
                        Title = occurrence.MedicationName,
                        Detail = detail,
                        IsDone = occurrence.IsDone,
                        IsOverdue = occurrence.Status == "missed"
                            || ((occurrence.Status == "pending" || occurrence.Status == "snoozed")
                                && occurrence.LocalTime < DateTime.Now),
                    });
                }

                foreach (FocusSessionItem session in _focus?.Between(fromUtc, toUtc) ?? new List<FocusSessionItem>())
                {
                    entries.Add(new CalendarEntry
                    {
                        LocalTime = session.StartedUtc.ToLocalTime(),
                        Kind = CalendarEntryKind.Focus,
                        Title = string.IsNullOrWhiteSpace(session.TaskTitle) ? "Focus session" : session.TaskTitle,
                        Detail = "Focus · " + ModuleUtil.FormatMinutes(session.ActualSeconds),
                        IsDone = session.IsCompleted,
                    });
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load calendar entries.", ex);
            }

            return entries.OrderBy(entry => entry.LocalTime).ToList();
        }

        internal int CountOn(DateTime day, IEnumerable<CalendarEntry> entries)
        {
            return entries.Count(entry => entry.LocalTime.Date == day.Date);
        }

        internal void ShowMonth(DateTime month)
        {
            VisibleMonth = new DateTime(month.Year, month.Month, 1);

            if (SelectedDate.Year != VisibleMonth.Year || SelectedDate.Month != VisibleMonth.Month)
            {
                SelectedDate = DateTime.Today.Year == VisibleMonth.Year && DateTime.Today.Month == VisibleMonth.Month
                    ? DateTime.Today
                    : VisibleMonth;
            }

            Changed?.Invoke();
        }

        internal void MoveMonth(int delta)
        {
            ShowMonth(VisibleMonth.AddMonths(delta));
        }

        internal void ShowToday()
        {
            VisibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            SelectedDate = DateTime.Today;
            Changed?.Invoke();
        }

        internal void SelectDate(DateTime date)
        {
            SelectedDate = date.Date;
            if (SelectedDate.Year != VisibleMonth.Year || SelectedDate.Month != VisibleMonth.Month)
            {
                VisibleMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
            }

            Changed?.Invoke();
        }

        internal string AddQuickEntry(DateTime dateLocal, string title, bool isTask, string timeText, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(title))
            {
                error = "Enter a title first.";
                return null;
            }

            if (!TryParseTime(timeText, out TimeSpan time))
            {
                error = "Use a time like 09:30.";
                return null;
            }

            DateTime local = dateLocal.Date.Add(time);
            string id;

            if (isTask)
            {
                id = _tasks?.Add(new TaskItem
                {
                    Title = title.Trim(),
                    Priority = 2,
                    DueUtc = local.ToUniversalTime(),
                    Recurrence = "none",
                });

                _context?.RefreshModule?.Invoke("tasks");
            }
            else
            {
                id = _reminders?.Add(new ReminderItem
                {
                    Title = title.Trim(),
                    DueUtc = local.ToUniversalTime(),
                    Recurrence = "none",
                });

                _context?.RefreshModule?.Invoke("reminders");
            }

            if (id == null)
            {
                error = "Could not save the entry.";
                return null;
            }

            SelectedDate = dateLocal.Date;
            Changed?.Invoke();
            return (isTask ? "Task" : "Reminder") + " added at " + time.ToString(@"hh\:mm", CultureInfo.InvariantCulture) + ".";
        }

        internal void OpenPlanner()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }

        private static bool TryParseTime(string text, out TimeSpan time)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                time = new TimeSpan(9, 0, 0);
                return true;
            }

            text = text.Trim();

            if (TimeSpan.TryParseExact(text, new[] { @"h\:mm", @"hh\:mm" }, CultureInfo.InvariantCulture, out time))
            {
                return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
            }

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault,
                    out DateTime parsed))
            {
                time = parsed.TimeOfDay;
                return true;
            }

            return false;
        }

        private static string StatusLabel(string status)
        {
            switch (status)
            {
                case "taken": return "Taken";
                case "skipped": return "Skipped";
                case "missed": return "Missed";
                case "snoozed": return "Snoozed";
                case "pending": return "Pending";
                default: return "Planned";
            }
        }
    }
}
