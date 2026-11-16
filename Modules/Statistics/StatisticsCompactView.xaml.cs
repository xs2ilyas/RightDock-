using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RightDock.Modules.Statistics
{
    public partial class StatisticsCompactView : UserControl
    {
        private readonly StatisticsModule _module;
        private readonly DispatcherTimer _timer;
        private bool _syncing;

        internal StatisticsCompactView(StatisticsModule module)
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

                FocusValue.Text = ModuleUtil.FormatMinutes(snapshot.TotalFocusSeconds);
                FocusMeta.Text = snapshot.TotalFocusSessions == 1
                    ? "1 session"
                    : snapshot.TotalFocusSessions + " sessions";

                TaskValue.Text = snapshot.TotalTasksCompleted.ToString();
                TaskMeta.Text = "completed";

                HabitValue.Text = Percent(snapshot.OverallHabitRate);
                HabitMeta.Text = snapshot.TotalHabitCheckins + " check-ins";

                MedValue.Text = Percent(snapshot.OverallMedicationRate);
                MedMeta.Text = snapshot.TotalMedicationTaken + " of "
                    + snapshot.TotalMedicationDue + " doses";

                ChartTitle.Text = StatsChart.MetricTitle(snapshot.Metric).ToUpperInvariant()
                    + " · " + snapshot.RangeLabel.ToUpperInvariant();

                StatsChart.Build(MiniChart, snapshot.Buckets, snapshot.Metric, 34, 8.5, 8.5);
            }
            finally
            {
                _syncing = false;
            }
        }

        private static string Percent(double rate)
        {
            return Math.Round(rate * 100).ToString("0") + "%";
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.OpenStatistics();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.Refresh();
        }
    }
}
