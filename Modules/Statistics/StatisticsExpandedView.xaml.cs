using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RightDock.Modules.Statistics
{
    public partial class StatisticsExpandedView : UserControl
    {
        private static readonly Brush HeaderBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush PeriodBrush = ModuleUtil.Brush("#E4E4E4");
        private static readonly Brush ValueBrush = ModuleUtil.Brush("#F0F0F0");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly StatisticsModule _module;
        private readonly DispatcherTimer _timer;
        private bool _syncing;

        internal StatisticsExpandedView(StatisticsModule module)
        {
            _module = module;
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _timer.Tick += (_, __) => _module.Refresh();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;
            _timer.Start();
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _module.Changed -= Refresh;
            _timer.Stop();
        }

        private void Refresh()
        {
            StatsSnapshot snapshot = _module.Snapshot;
            if (snapshot == null) return;

            _syncing = true;
            try
            {
                RangeText.Text = snapshot.RangeLabel;

                DailyChip.IsChecked = snapshot.Granularity == StatsGranularity.Daily;
                WeeklyChip.IsChecked = snapshot.Granularity == StatsGranularity.Weekly;
                MonthlyChip.IsChecked = snapshot.Granularity == StatsGranularity.Monthly;

                MetricFocusChip.IsChecked = snapshot.Metric == StatsMetric.Focus;
                MetricTaskChip.IsChecked = snapshot.Metric == StatsMetric.Tasks;
                MetricHabitChip.IsChecked = snapshot.Metric == StatsMetric.Habits;
                MetricMedicationChip.IsChecked = snapshot.Metric == StatsMetric.Medication;

                SummaryFocusValue.Text = ModuleUtil.FormatMinutes(snapshot.TotalFocusSeconds);
                SummaryFocusMeta.Text = snapshot.TotalFocusSessions == 1
                    ? "1 session"
                    : snapshot.TotalFocusSessions + " sessions";

                SummaryTaskValue.Text = snapshot.TotalTasksCompleted.ToString();
                SummaryTaskMeta.Text = "completed in range";

                SummaryHabitValue.Text = Percent(snapshot.OverallHabitRate);
                SummaryHabitMeta.Text = snapshot.TotalHabitCheckins + " check-ins";

                SummaryMedValue.Text = Percent(snapshot.OverallMedicationRate);
                SummaryMedMeta.Text = snapshot.TotalMedicationTaken + " of "
                    + snapshot.TotalMedicationDue + " doses";

                ChartTitle.Text = StatsChart.MetricTitle(snapshot.Metric).ToUpperInvariant();
                ChartCaption.Text = CaptionFor(snapshot.Metric);
                StatsChart.Build(ChartPanel, snapshot.Buckets, snapshot.Metric, 150, 9.5, 9);

                TableRange.Text = snapshot.RangeLabel + " · " + snapshot.Buckets.Count + " rows";
                BuildTable(snapshot);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildTable(StatsSnapshot snapshot)
        {
            TablePanel.Children.Clear();
            TablePanel.Children.Add(BuildRow("Period", "Focus", "Tasks", "Habits", "Medication", header: true));

            foreach (StatsBucket bucket in snapshot.Buckets)
            {
                string focus = bucket.FocusSeconds > 0 ? ModuleUtil.FormatMinutes(bucket.FocusSeconds) : "—";
                string tasks = bucket.TasksCompleted.ToString();
                string habits = bucket.HabitPossible > 0 ? Percent(bucket.HabitRate) : "—";
                string medication = bucket.MedicationDue > 0 ? Percent(bucket.MedicationRate) : "—";

                TablePanel.Children.Add(BuildRow(bucket.Detail, focus, tasks, habits, medication, header: false));
            }
        }

        private FrameworkElement BuildRow(string period, string focus, string tasks,
            string habits, string medication, bool header)
        {
            var grid = new Grid();
            double[] widths = { 1.7, 1, 0.8, 1, 1.1 };

            foreach (double width in widths)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(width, GridUnitType.Star),
                });
            }

            AddCell(grid, 0, period, header, false);
            AddCell(grid, 1, focus, header, true);
            AddCell(grid, 2, tasks, header, true);
            AddCell(grid, 3, habits, header, true);
            AddCell(grid, 4, medication, header, true);

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, header ? 0 : 1),
                Padding = new Thickness(1, header ? 3 : 4, 1, header ? 5 : 4),
                Child = grid,
            };
        }

        private static void AddCell(Grid grid, int column, string text, bool header, bool numeric)
        {
            var cell = new TextBlock
            {
                Text = text,
                Foreground = header ? HeaderBrush : numeric ? ValueBrush : PeriodBrush,
                FontSize = header ? 9.5 : 11,
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                TextAlignment = numeric ? TextAlignment.Right : TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        private static string CaptionFor(StatsMetric metric)
        {
            switch (metric)
            {
                case StatsMetric.Tasks:
                    return "Tasks completed in each period.";
                case StatsMetric.Habits:
                    return "Completed check-ins divided by active habits × days in the period.";
                case StatsMetric.Medication:
                    return "Doses taken divided by logged doses (taken, missed or skipped).";
                default:
                    return "Minutes from ended focus sessions in each period.";
            }
        }

        private static string Percent(double rate)
        {
            return Math.Round(rate * 100).ToString("0") + "%";
        }

        private void Granularity_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            string tag = (sender as FrameworkElement)?.Tag as string;
            StatsGranularity granularity = tag == "weekly"
                ? StatsGranularity.Weekly
                : tag == "monthly" ? StatsGranularity.Monthly : StatsGranularity.Daily;

            _module.SetGranularity(granularity);
        }

        private void Metric_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            string tag = (sender as FrameworkElement)?.Tag as string;
            StatsMetric metric = tag == "tasks"
                ? StatsMetric.Tasks
                : tag == "habits"
                    ? StatsMetric.Habits
                    : tag == "medication" ? StatsMetric.Medication : StatsMetric.Focus;

            _module.SetMetric(metric);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.Refresh();
        }
    }
}
