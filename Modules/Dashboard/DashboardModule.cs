using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Dashboard
{
    internal sealed class DashboardSnapshot
    {
        internal DateTime UpdatedLocal { get; set; }

        internal int FocusSessionsToday { get; set; }

        internal int FocusSecondsToday { get; set; }

        internal int[] FocusWeekSeconds { get; set; } = new int[7];

        internal TaskCounts Tasks { get; set; } = new TaskCounts();

        internal int HabitsDone { get; set; }

        internal int HabitsTotal { get; set; }

        internal int BestHabitStreak { get; set; }

        internal int MedicationTaken { get; set; }

        internal int MedicationPlanned { get; set; }

        internal string NextDoseText { get; set; } = "No medication scheduled";

        internal int RemindersActive { get; set; }

        internal int RemindersOverdue { get; set; }

        internal string NextReminderText { get; set; } = "No active reminders";

        internal int NotesActive { get; set; }

        internal int NotesPinned { get; set; }

        internal int NotesArchived { get; set; }

        internal List<HealthMetricState> Health { get; set; } = new List<HealthMetricState>();
    }

    internal sealed class DashboardModule : ProductivityModule
    {
        private ModuleContext _context;
        private TaskStore _tasks;
        private FocusStore _focus;
        private HabitStore _habits;
        private MedicationStore _medication;
        private ReminderStore _reminders;
        private NoteStore _notes;
        private HealthStore _health;

        internal event Action Changed;

        internal DashboardSnapshot Snapshot { get; private set; } = new DashboardSnapshot();

        public override string Id => "dashboard";

        public override string Name => "Dashboard";

        public override string Description => "Daily overview with focus, task, habit, medication and health widgets.";

        public override string IconKey => "lucide:layout-dashboard";

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _tasks = new TaskStore(context.Database);
            _focus = new FocusStore(context.Database);
            _habits = new HabitStore(context.Database);
            _medication = new MedicationStore(context.Database);
            _reminders = new ReminderStore(context.Database);
            _notes = new NoteStore(context.Database);
            _health = new HealthStore(context.Database);
        }

        public override void Activate()
        {
            Refresh();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new DashboardCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new DashboardExpandedView(this);
        }

        internal void Refresh()
        {
            try
            {
                var snapshot = new DashboardSnapshot { UpdatedLocal = DateTime.Now };

                FocusStats focus = _focus?.GetStats() ?? new FocusStats();
                snapshot.FocusSessionsToday = focus.TodaySessions;
                snapshot.FocusSecondsToday = focus.TodaySeconds;
                snapshot.FocusWeekSeconds = focus.DailySecondsOldestFirst;

                snapshot.Tasks = _tasks?.Counts(DateTime.Today) ?? new TaskCounts();

                List<HabitItem> habits = _habits?.Load() ?? new List<HabitItem>();
                snapshot.HabitsDone = habits.Count(habit => habit.TodayCompleted);
                snapshot.HabitsTotal = habits.Count;
                snapshot.BestHabitStreak = habits.Count == 0 ? 0 : habits.Max(habit => habit.CurrentStreak);

                List<MedicationDose> doses = _medication?.Today() ?? new List<MedicationDose>();
                snapshot.MedicationTaken = doses.Count(dose => dose.Status == "taken");
                snapshot.MedicationPlanned = doses.Count;

                MedicationDose nextDose = doses
                    .Where(dose => dose.IsActionable)
                    .OrderBy(dose => dose.EffectiveUtc)
                    .FirstOrDefault();
                snapshot.NextDoseText = nextDose == null
                    ? (doses.Count == 0 ? "No medication scheduled" : "No doses left today")
                    : nextDose.MedicationName + " · " + nextDose.TimeLabel;

                List<ReminderItem> reminders = _reminders?.Active() ?? new List<ReminderItem>();
                DateTime now = DateTime.UtcNow;
                snapshot.RemindersActive = reminders.Count;
                snapshot.RemindersOverdue = reminders.Count(reminder => reminder.EffectiveDueUtc < now);

                ReminderItem nextReminder = reminders.FirstOrDefault(reminder => reminder.EffectiveDueUtc >= now)
                    ?? reminders.FirstOrDefault();
                snapshot.NextReminderText = nextReminder == null
                    ? "No active reminders"
                    : nextReminder.Title + " · " + ModuleUtil.FormatDue(nextReminder.EffectiveDueUtc);

                List<NoteItem> notes = _notes?.Query(null) ?? new List<NoteItem>();
                snapshot.NotesActive = notes.Count;
                snapshot.NotesPinned = notes.Count(note => note.IsPinned);
                snapshot.NotesArchived = _notes?.CountArchived() ?? 0;

                snapshot.Health = _health?.Load() ?? new List<HealthMetricState>();

                Snapshot = snapshot;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not build the dashboard overview.", ex);
            }

            Changed?.Invoke();
        }

        internal void OpenModule(string moduleId)
        {
            _context?.OpenExpanded?.Invoke(moduleId);
        }

        internal void OpenDashboard()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }
    }
}
