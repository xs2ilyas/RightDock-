using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Statistics
{
    internal enum StatsGranularity
    {
        Daily,
        Weekly,
        Monthly,
    }

    internal enum StatsMetric
    {
        Focus,
        Tasks,
        Habits,
        Medication,
    }

    internal sealed class StatsBucket
    {
        internal DateTime Start { get; set; }

        internal DateTime End { get; set; }

        internal string Label { get; set; } = string.Empty;

        internal string Detail { get; set; } = string.Empty;

        internal int Days => (End.Date - Start.Date).Days + 1;

        internal int FocusSeconds { get; set; }

        internal int FocusSessions { get; set; }

        internal int TasksCompleted { get; set; }

        internal int HabitCheckins { get; set; }

        internal int HabitPossible { get; set; }

        internal int MedicationTaken { get; set; }

        internal int MedicationDue { get; set; }

        internal double HabitRate => HabitPossible <= 0
            ? 0
            : Math.Min(1.0, (double)HabitCheckins / HabitPossible);

        internal double MedicationRate => MedicationDue <= 0
            ? 0
            : Math.Min(1.0, (double)MedicationTaken / MedicationDue);
    }

    internal sealed class StatsSnapshot
    {
        internal StatsGranularity Granularity { get; set; }

        internal StatsMetric Metric { get; set; }

        internal DateTime RangeStart { get; set; }

        internal DateTime RangeEnd { get; set; }

        internal string RangeLabel { get; set; } = string.Empty;

        internal List<StatsBucket> Buckets { get; set; } = new List<StatsBucket>();

        internal int TotalFocusSeconds { get; set; }

        internal int TotalFocusSessions { get; set; }

        internal int TotalTasksCompleted { get; set; }

        internal int TotalHabitCheckins { get; set; }

        internal int TotalHabitPossible { get; set; }

        internal int TotalMedicationTaken { get; set; }

        internal int TotalMedicationDue { get; set; }

        internal double OverallHabitRate => TotalHabitPossible <= 0
            ? 0
            : Math.Min(1.0, (double)TotalHabitCheckins / TotalHabitPossible);

        internal double OverallMedicationRate => TotalMedicationDue <= 0
            ? 0
            : Math.Min(1.0, (double)TotalMedicationTaken / TotalMedicationDue);

        internal List<HealthAverage> Health { get; set; } = new List<HealthAverage>();
    }

    internal sealed class StatisticsModule : ProductivityModule
    {
        private ModuleContext _context;
        private FocusStore _focus;
        private TaskStore _tasks;
        private HabitStore _habits;
        private MedicationStore _medication;
        private HealthStore _health;

        internal event Action Changed;

        internal StatsSnapshot Snapshot { get; private set; } = new StatsSnapshot();

        internal StatsGranularity Granularity { get; private set; } = StatsGranularity.Daily;

        internal StatsMetric Metric { get; private set; } = StatsMetric.Focus;

        public override string Id => "statistics";

        public override string Name => "Statistics";

        public override string Description => "Daily, weekly and monthly charts and tables for focus, tasks, habits and medication.";

        public override string IconKey => "lucide:chart-column";

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _focus = new FocusStore(context.Database);
            _tasks = new TaskStore(context.Database);
            _habits = new HabitStore(context.Database);
            _medication = new MedicationStore(context.Database);
            _health = new HealthStore(context.Database);
        }

        public override void Activate()
        {
            Refresh();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new StatisticsCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new StatisticsExpandedView(this);
        }

        internal void SetGranularity(StatsGranularity granularity)
        {
            Granularity = granularity;
            Refresh();
        }

        internal void SetMetric(StatsMetric metric)
        {
            Metric = metric;
            Refresh();
        }

        internal void Refresh()
        {
            try
            {
                var snapshot = new StatsSnapshot { Granularity = Granularity, Metric = Metric };
                List<StatsBucket> buckets = BuildBuckets(Granularity,
                    out DateTime rangeStart, out DateTime rangeEnd, out string rangeLabel);

                snapshot.RangeStart = rangeStart;
                snapshot.RangeEnd = rangeEnd;
                snapshot.RangeLabel = rangeLabel;

                DateTime fromUtc = rangeStart.Date.ToUniversalTime();
                DateTime toUtc = rangeEnd.Date.AddDays(1).ToUniversalTime();

                foreach (FocusSessionItem session in _focus?.Between(fromUtc, toUtc) ?? new List<FocusSessionItem>())
                {
                    StatsBucket bucket = FindBucket(buckets, session.StartedUtc.ToLocalTime().Date);
                    if (bucket == null) continue;

                    bucket.FocusSeconds += session.ActualSeconds;
                    bucket.FocusSessions++;
                    snapshot.TotalFocusSeconds += session.ActualSeconds;
                    snapshot.TotalFocusSessions++;
                }

                foreach (TaskItem task in _tasks?.CompletedBetween(fromUtc, toUtc) ?? new List<TaskItem>())
                {
                    if (!task.CompletedUtc.HasValue) continue;

                    StatsBucket bucket = FindBucket(buckets, task.CompletedUtc.Value.ToLocalTime().Date);
                    if (bucket == null) continue;

                    bucket.TasksCompleted++;
                    snapshot.TotalTasksCompleted++;
                }

                int habitCount = _habits?.CountActive() ?? 0;
                foreach (StatsBucket bucket in buckets)
                {
                    bucket.HabitPossible = habitCount * bucket.Days;
                }

                foreach (HabitLogEntry log in _habits?.LogsBetween(rangeStart, rangeEnd) ?? new List<HabitLogEntry>())
                {
                    if (!log.IsCompleted) continue;

                    StatsBucket bucket = FindBucket(buckets, log.Date);
                    if (bucket == null) continue;

                    bucket.HabitCheckins++;
                    snapshot.TotalHabitCheckins++;
                }

                foreach (MedicationDose dose in _medication?.Between(fromUtc, toUtc) ?? new List<MedicationDose>())
                {
                    if (dose.Status != "taken" && dose.Status != "missed" && dose.Status != "skipped") continue;

                    StatsBucket bucket = FindBucket(buckets, dose.ScheduledUtc.ToLocalTime().Date);
                    if (bucket == null) continue;

                    bucket.MedicationDue++;
                    snapshot.TotalMedicationDue++;

                    if (dose.Status == "taken")
                    {
                        bucket.MedicationTaken++;
                        snapshot.TotalMedicationTaken++;
                    }
                }

                foreach (StatsBucket bucket in buckets)
                {
                    snapshot.TotalHabitPossible += bucket.HabitPossible;
                }

                snapshot.Buckets = buckets;
                snapshot.Health = _health?.Averages(rangeStart, rangeEnd) ?? new List<HealthAverage>();

                Snapshot = snapshot;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not build statistics.", ex);
            }

            Changed?.Invoke();
        }

        internal void OpenStatistics()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }

        private static List<StatsBucket> BuildBuckets(StatsGranularity granularity,
            out DateTime rangeStart, out DateTime rangeEnd, out string rangeLabel)
        {
            var buckets = new List<StatsBucket>();
            DateTime today = DateTime.Today;

            if (granularity == StatsGranularity.Weekly)
            {
                int offset = ((int)today.DayOfWeek + 6) % 7;
                DateTime currentWeekStart = today.AddDays(-offset);
                DateTime firstStart = currentWeekStart.AddDays(-49);

                for (int i = 0; i < 8; i++)
                {
                    DateTime start = firstStart.AddDays(i * 7);
                    DateTime end = start.AddDays(6);
                    if (end > today) end = today;

                    buckets.Add(new StatsBucket
                    {
                        Start = start,
                        End = end,
                        Label = start.ToString("MMM d"),
                        Detail = start.ToString("MMM d") + " – " + end.ToString("MMM d"),
                    });
                }

                rangeStart = firstStart;
                rangeEnd = today;
                rangeLabel = "Last 8 weeks";
                return buckets;
            }

            if (granularity == StatsGranularity.Monthly)
            {
                DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
                DateTime firstStart = currentMonth.AddMonths(-5);

                for (int i = 0; i < 6; i++)
                {
                    DateTime start = firstStart.AddMonths(i);
                    DateTime end = start.AddMonths(1).AddDays(-1);
                    if (end > today) end = today;

                    buckets.Add(new StatsBucket
                    {
                        Start = start,
                        End = end,
                        Label = start.ToString("MMM"),
                        Detail = start.ToString("MMMM yyyy"),
                    });
                }

                rangeStart = firstStart;
                rangeEnd = today;
                rangeLabel = "Last 6 months";
                return buckets;
            }

            rangeStart = today.AddDays(-6);
            rangeEnd = today;
            rangeLabel = "Last 7 days";

            for (int i = 0; i < 7; i++)
            {
                DateTime date = rangeStart.AddDays(i);
                buckets.Add(new StatsBucket
                {
                    Start = date,
                    End = date,
                    Label = date.ToString("ddd"),
                    Detail = date.ToString("dddd, MMM d"),
                });
            }

            return buckets;
        }

        private static StatsBucket FindBucket(List<StatsBucket> buckets, DateTime localDate)
        {
            foreach (StatsBucket bucket in buckets)
            {
                if (localDate >= bucket.Start.Date && localDate <= bucket.End.Date) return bucket;
            }

            return null;
        }
    }
}
