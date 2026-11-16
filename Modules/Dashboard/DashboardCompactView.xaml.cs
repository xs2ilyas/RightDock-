using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RightDock.Data;

namespace RightDock.Modules.Dashboard
{
    public partial class DashboardCompactView : UserControl
    {
        private static readonly Brush RowLabelBrush = ModuleUtil.Brush("#B8B8B8");
        private static readonly Brush RowValueBrush = ModuleUtil.Brush("#EDEDED");

        private readonly DashboardModule _module;
        private readonly DispatcherTimer _timer;
        private bool _syncing;

        internal DashboardCompactView(DashboardModule module)
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
            DashboardSnapshot snapshot = _module.Snapshot;
            if (snapshot == null) return;

            _syncing = true;
            try
            {
                UpdatedText.Text = "Updated " + snapshot.UpdatedLocal.ToString("HH:mm");

                FocusValue.Text = ModuleUtil.FormatMinutes(snapshot.FocusSecondsToday);
                FocusMeta.Text = snapshot.FocusSessionsToday == 0
                    ? "No sessions yet"
                    : snapshot.FocusSessionsToday + (snapshot.FocusSessionsToday == 1 ? " session today" : " sessions today");

                TaskValue.Text = snapshot.Tasks.DueToday + " due";
                TaskMeta.Text = snapshot.Tasks.Overdue + " overdue · " + snapshot.Tasks.CompletedToday + " done";

                HabitValue.Text = snapshot.HabitsDone + " of " + snapshot.HabitsTotal;
                HabitProgress.Maximum = Math.Max(1, snapshot.HabitsTotal);
                HabitProgress.Value = snapshot.HabitsDone;
                HabitMeta.Text = snapshot.HabitsTotal == 0
                    ? "No habits yet"
                    : snapshot.BestHabitStreak == 1
                        ? "Best streak 1 day"
                        : "Best streak " + snapshot.BestHabitStreak + " days";

                MedValue.Text = snapshot.MedicationPlanned == 0
                    ? "No doses"
                    : snapshot.MedicationTaken + " of " + snapshot.MedicationPlanned;
                MedMeta.Text = snapshot.NextDoseText;

                if (snapshot.RemindersActive > 0)
                {
                    NextText.Text = snapshot.NextReminderText;
                    NextMeta.Text = snapshot.RemindersOverdue > 0
                        ? snapshot.RemindersOverdue + " overdue · " + snapshot.RemindersActive + " active"
                        : snapshot.RemindersActive + (snapshot.RemindersActive == 1 ? " active reminder" : " active reminders");
                }
                else if (snapshot.MedicationPlanned > 0 && snapshot.MedicationTaken < snapshot.MedicationPlanned)
                {
                    NextText.Text = snapshot.NextDoseText;
                    NextMeta.Text = "Medication";
                }
                else
                {
                    NextText.Text = "Nothing scheduled for today.";
                    NextMeta.Text = snapshot.MedicationPlanned > 0 ? "All doses handled" : string.Empty;
                }

                BuildHealth(snapshot);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildHealth(DashboardSnapshot snapshot)
        {
            HealthPanel.Children.Clear();

            foreach (string key in HealthMetrics.RingKeys)
            {
                HealthMetricState state = snapshot.Health.FirstOrDefault(item => item.Metric.Key == key);
                if (state == null) continue;

                string value = state.HasToday
                    ? state.Metric.FormatValue(state.Today)
                    : "Not logged";

                AddHealthRow(state.Metric.Name, value + " · " + state.Metric.TargetText);
            }

            HealthMetricState weight = snapshot.Health.FirstOrDefault(item => item.Metric.Key == "weight");
            if (weight != null)
            {
                AddHealthRow("Weight", weight.Latest.HasValue
                    ? weight.Metric.FormatValue(weight.Latest.Value)
                    : "Not logged");
            }
        }

        private void AddHealthRow(string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = RowLabelBrush,
                FontSize = 11.5,
            });

            var valueText = new TextBlock
            {
                Text = value,
                Foreground = RowValueBrush,
                FontSize = 11,
            };
            Grid.SetColumn(valueText, 1);
            grid.Children.Add(valueText);

            HealthPanel.Children.Add(grid);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.OpenDashboard();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.Refresh();
        }
    }
}
