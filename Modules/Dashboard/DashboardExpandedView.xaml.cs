using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RightDock.Data;

namespace RightDock.Modules.Dashboard
{
    public partial class DashboardExpandedView : UserControl
    {
        private static readonly Brush RowLabelBrush = ModuleUtil.Brush("#B8B8B8");
        private static readonly Brush RowValueBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush BarBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush BarEmptyBrush = ModuleUtil.Brush("#26FFFFFF");

        private readonly DashboardModule _module;
        private readonly DispatcherTimer _timer;
        private bool _syncing;

        internal DashboardExpandedView(DashboardModule module)
        {
            _module = module;
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
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
                DateText.Text = DateTime.Today.ToString("dddd, MMMM d");
                UpdatedText.Text = "Updated " + snapshot.UpdatedLocal.ToString("HH:mm");

                string focusTime = ModuleUtil.FormatMinutes(snapshot.FocusSecondsToday);
                string sessionText = snapshot.FocusSessionsToday == 1
                    ? "1 session"
                    : snapshot.FocusSessionsToday + " sessions";

                OverviewFocusValue.Text = focusTime;
                OverviewFocusMeta.Text = snapshot.FocusSessionsToday == 0
                    ? "No sessions yet"
                    : sessionText + " today";

                OverviewTaskValue.Text = snapshot.Tasks.DueToday + " due";
                OverviewTaskMeta.Text = snapshot.Tasks.Overdue + " overdue · "
                    + snapshot.Tasks.CompletedToday + " completed";

                OverviewHabitValue.Text = snapshot.HabitsDone + " / " + snapshot.HabitsTotal;
                OverviewHabitProgress.Maximum = Math.Max(1, snapshot.HabitsTotal);
                OverviewHabitProgress.Value = snapshot.HabitsDone;
                OverviewHabitMeta.Text = snapshot.HabitsTotal == 0
                    ? "No habits yet"
                    : (snapshot.HabitsTotal - snapshot.HabitsDone) + " left today";

                OverviewMedValue.Text = snapshot.MedicationTaken + " / " + snapshot.MedicationPlanned;
                OverviewMedMeta.Text = snapshot.NextDoseText;

                OverviewReminderValue.Text = snapshot.RemindersActive + " active";
                OverviewReminderMeta.Text = snapshot.RemindersOverdue > 0
                    ? snapshot.RemindersOverdue + " overdue · " + snapshot.NextReminderText
                    : snapshot.NextReminderText;

                OverviewNoteValue.Text = snapshot.NotesActive + (snapshot.NotesActive == 1 ? " note" : " notes");
                OverviewNoteMeta.Text = snapshot.NotesPinned + " pinned · "
                    + snapshot.NotesArchived + " archived";

                FocusCardTime.Text = focusTime;
                FocusCardSessions.Text = sessionText;
                FocusCardMeta.Text = snapshot.FocusSessionsToday == 0
                    ? "Start a session from the focus timer"
                    : "Tracked today across " + sessionText;
                BuildFocusChart(snapshot.FocusWeekSeconds);

                TaskCardDone.Text = snapshot.Tasks.CompletedToday + " done today";
                TaskCardDue.Text = snapshot.Tasks.DueToday.ToString();
                TaskCardOverdue.Text = snapshot.Tasks.Overdue.ToString();
                TaskCardCompleted.Text = snapshot.Tasks.CompletedToday.ToString();

                HabitCardValue.Text = snapshot.HabitsDone + " of " + snapshot.HabitsTotal;
                HabitCardProgress.Maximum = Math.Max(1, snapshot.HabitsTotal);
                HabitCardProgress.Value = snapshot.HabitsDone;
                HabitCardStreak.Text = snapshot.BestHabitStreak > 0
                    ? "Best " + snapshot.BestHabitStreak + (snapshot.BestHabitStreak == 1 ? " day" : " days")
                    : string.Empty;
                HabitCardMeta.Text = snapshot.HabitsTotal == 0
                    ? "Add habits in the Habits module."
                    : (snapshot.HabitsTotal - snapshot.HabitsDone) + " remaining today";

                MedCardTotal.Text = snapshot.MedicationPlanned + " planned";
                MedCardValue.Text = snapshot.MedicationTaken + " of " + snapshot.MedicationPlanned;
                MedCardMeta.Text = snapshot.MedicationPlanned == 0
                    ? "No medication scheduled for today."
                    : "doses taken today";
                MedCardNext.Text = snapshot.MedicationPlanned == 0
                    ? string.Empty
                    : "Next: " + snapshot.NextDoseText;

                ReminderCardValue.Text = snapshot.RemindersActive == 0
                    ? "None"
                    : snapshot.RemindersActive + " active";
                ReminderCardMeta.Text = snapshot.RemindersActive == 0
                    ? "No active reminders"
                    : "Next: " + snapshot.NextReminderText
                        + (snapshot.RemindersOverdue > 0 ? Environment.NewLine + snapshot.RemindersOverdue + " overdue" : string.Empty);

                NoteCardValue.Text = snapshot.NotesActive + (snapshot.NotesActive == 1 ? " note" : " notes");
                NoteCardMeta.Text = snapshot.NotesPinned + " pinned" + Environment.NewLine
                    + snapshot.NotesArchived + " archived";

                BuildHealth(snapshot);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildFocusChart(int[] values)
        {
            FocusChart.Children.Clear();

            int max = values.Length == 0 ? 0 : values.Max();

            for (int i = 0; i < values.Length; i++)
            {
                int value = values[i];
                double height = max <= 0 ? 3 : Math.Max(3, 34.0 * value / max);

                var barHost = new Grid { Height = 40, VerticalAlignment = VerticalAlignment.Bottom };
                barHost.Children.Add(new Border
                {
                    Height = height,
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Background = value > 0 ? BarBrush : BarEmptyBrush,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    ToolTip = value > 0 ? ModuleUtil.FormatMinutes(value) : "No focus",
                });

                var column = new StackPanel { Margin = new Thickness(2, 0, 2, 0) };
                column.Children.Add(barHost);
                column.Children.Add(new TextBlock
                {
                    Text = ModuleUtil.FormatDay(DateTime.Today.AddDays(i - 6)),
                    FontSize = 9.5,
                    Foreground = RowLabelBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0),
                });

                FocusChart.Children.Add(column);
            }
        }

        private void BuildHealth(DashboardSnapshot snapshot)
        {
            HealthCardPanel.Children.Clear();

            foreach (string key in HealthMetrics.RingKeys)
            {
                HealthMetricState state = snapshot.Health.FirstOrDefault(item => item.Metric.Key == key);
                if (state == null) continue;

                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 7) };

                var header = new Grid();
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                header.Children.Add(new TextBlock
                {
                    Text = state.Metric.Name,
                    Foreground = RowLabelBrush,
                    FontSize = 11,
                });

                string value = state.HasToday
                    ? state.Metric.FormatValue(state.Today) + " of "
                        + state.Metric.FormatValue(state.Metric.Target)
                    : "Not logged";

                var valueText = new TextBlock
                {
                    Text = value,
                    Foreground = RowValueBrush,
                    FontSize = 11,
                };
                Grid.SetColumn(valueText, 1);
                header.Children.Add(valueText);

                row.Children.Add(header);

                var bar = new ProgressBar
                {
                    Style = (Style)FindResource("MdProgress"),
                    Minimum = 0,
                    Maximum = Math.Max(1, state.Metric.Target),
                    Value = state.HasToday ? state.Today : 0,
                    Foreground = ModuleUtil.Brush(state.Metric.ColorHex),
                    Margin = new Thickness(0, 3, 0, 0),
                };
                row.Children.Add(bar);

                HealthCardPanel.Children.Add(row);
            }

            HealthMetricState weight = snapshot.Health.FirstOrDefault(item => item.Metric.Key == "weight");
            HealthCardWeight.Text = weight != null && weight.Latest.HasValue
                ? "Latest " + weight.Metric.FormatValue(weight.Latest.Value)
                : "No weight logged";
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.Refresh();
        }

        private void OpenFocus_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("focus");
        }

        private void OpenTasks_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("tasks");
        }

        private void OpenCalendar_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("calendar");
        }

        private void OpenStatistics_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("statistics");
        }

        private void OpenHabits_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("habits");
        }

        private void OpenMedication_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("medication");
        }

        private void OpenHealth_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("health");
        }

        private void OpenReminders_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("reminders");
        }

        private void OpenNotes_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncing) _module.OpenModule("notes");
        }
    }
}
