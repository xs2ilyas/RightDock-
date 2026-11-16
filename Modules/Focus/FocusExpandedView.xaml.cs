using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Focus
{
    public partial class FocusExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush CompletedBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush PartialBrush = ModuleUtil.Brush("#C9A36B");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");
        private static readonly Brush BarBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush BarEmptyBrush = ModuleUtil.Brush("#26FFFFFF");

        private readonly FocusModule _module;
        private bool _syncing;
        private string _taskKey;
        private string _statsKey;
        private string _historyKey;

        internal FocusExpandedView(FocusModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _module.Changed -= Refresh;
        }

        private void Refresh()
        {
            _syncing = true;
            try
            {
                ClockText.Text = ModuleUtil.FormatClock(_module.RemainingSeconds);
                PhaseText.Text = _module.IsRunning ? "FOCUSING" : _module.IsPaused ? "PAUSED" : "READY";
                TaskText.Text = string.IsNullOrWhiteSpace(_module.TaskTitle)
                    ? "No task linked"
                    : "Working on: " + _module.TaskTitle;

                Progress.Value = _module.Progress;

                PrimaryButton.Content = _module.IsRunning ? "Pause" : _module.IsPaused ? "Resume" : "Start focus";
                ResetButton.IsEnabled = _module.IsActive;

                bool idle = !_module.IsActive;
                Preset15.IsChecked = _module.PresetMinutes == 15;
                Preset25.IsChecked = _module.PresetMinutes == 25;
                Preset45.IsChecked = _module.PresetMinutes == 45;
                Preset60.IsChecked = _module.PresetMinutes == 60;
                Preset15.IsEnabled = idle;
                Preset25.IsEnabled = idle;
                Preset45.IsEnabled = idle;
                Preset60.IsEnabled = idle;

                string taskKey = (_module.IsActive ? "1" : "0") + "|" + (_module.SelectedTaskId ?? string.Empty);
                if (taskKey != _taskKey)
                {
                    _taskKey = taskKey;
                    BuildTasks();
                }

                string statsKey = string.Join(",", _module.Stats.DailySecondsOldestFirst);
                if (statsKey != _statsKey)
                {
                    _statsKey = statsKey;
                    BuildStats();
                }

                string historyKey = _module.History.Count == 0
                    ? "0"
                    : _module.History.Count + ":" + _module.History[0].Id + ":" + _module.History[0].EndedUtc;
                if (historyKey != _historyKey)
                {
                    _historyKey = historyKey;
                    BuildHistory();
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildTasks()
        {
            TaskPanel.Children.Clear();

            List<TaskItem> tasks = _module.GetActiveTasks();
            bool locked = _module.IsActive;

            TaskHint.Text = locked
                ? "The linked task is locked while the timer is running."
                : tasks.Count == 0
                    ? "No open tasks yet. Add tasks in the Tasks module."
                    : "Pick a task to record it with the session.";

            TaskPanel.Children.Add(CreateTaskChip(null, "No task", _module.SelectedTaskId == null, locked));
            foreach (TaskItem task in tasks)
            {
                TaskPanel.Children.Add(CreateTaskChip(task.Id, task.Title, _module.SelectedTaskId == task.Id, locked));
            }
        }

        private FrameworkElement CreateTaskChip(string taskId, string title, bool selected, bool locked)
        {
            var chip = new ToggleButton
            {
                Style = (Style)FindResource("MdChip"),
                Content = title.Length > 30 ? title.Substring(0, 29) + "…" : title,
                IsChecked = selected,
                IsEnabled = !locked,
                Tag = taskId,
                ToolTip = title,
            };
            AutomationProperties.SetName(chip, "Focus task " + title);

            chip.Click += (_, __) =>
            {
                if (_syncing) return;
                _module.SetTask(taskId, taskId == null ? null : title);
            };

            return chip;
        }

        private void BuildStats()
        {
            StatsGrid.Children.Clear();

            int[] values = _module.Stats.DailySecondsOldestFirst;
            int max = values.Length == 0 ? 0 : values.Max();
            int total = values.Sum();

            WeekTotal.Text = total > 0 ? ModuleUtil.FormatMinutes(total) + " total" : "No time yet";

            for (int i = 0; i < values.Length; i++)
            {
                int value = values[i];
                double height = max <= 0 ? 3 : Math.Max(3, 52.0 * value / max);

                var barHost = new Grid { Height = 60, VerticalAlignment = VerticalAlignment.Bottom };
                barHost.Children.Add(new Border
                {
                    Height = height,
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Background = value > 0 ? BarBrush : BarEmptyBrush,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    ToolTip = value > 0 ? ModuleUtil.FormatMinutes(value) : "No focus",
                });
                barHost.Children.Add(new TextBlock
                {
                    Text = value > 0 ? (value / 60) + "m" : string.Empty,
                    FontSize = 9.5,
                    Foreground = RowMetaBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                });

                var column = new StackPanel { Margin = new Thickness(3, 0, 3, 0) };
                column.Children.Add(barHost);
                column.Children.Add(new TextBlock
                {
                    Text = ModuleUtil.FormatDay(DateTime.Today.AddDays(i - 6)),
                    FontSize = 10,
                    Foreground = RowMetaBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0),
                });

                StatsGrid.Children.Add(column);
            }
        }

        private void BuildHistory()
        {
            HistoryPanel.Children.Clear();

            List<FocusSessionItem> items = _module.History.ToList();
            EmptyHistory.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistorySummary.Text = items.Count == 0 ? string.Empty : items.Count + " shown";

            foreach (FocusSessionItem session in items)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var text = new StackPanel();
                text.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(session.TaskTitle) ? "Focus session" : session.TaskTitle,
                    Foreground = RowTitleBrush,
                    FontSize = 12.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                text.Children.Add(new TextBlock
                {
                    Text = ModuleUtil.FormatHistoryTime(session.StartedUtc) + "  ·  "
                        + ModuleUtil.FormatMinutes(session.ActualSeconds) + " of "
                        + ModuleUtil.FormatMinutes(session.PlannedSeconds),
                    Foreground = RowMetaBrush,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 2, 0, 0),
                });

                var status = new TextBlock
                {
                    Text = session.IsCompleted ? "Completed" : "Ended early",
                    Foreground = session.IsCompleted ? CompletedBrush : PartialBrush,
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0),
                };
                Grid.SetColumn(status, 1);

                grid.Children.Add(text);
                grid.Children.Add(status);

                HistoryPanel.Children.Add(new Border
                {
                    BorderBrush = DividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(1, 8, 1, 8),
                    Child = grid,
                });
            }
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (_module.IsRunning) _module.Pause();
            else if (_module.IsPaused) _module.Resume();
            else _module.Start();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.Reset();
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            if (sender is FrameworkElement element && element.Tag is string tag
                && int.TryParse(tag, out int minutes))
            {
                _module.SetPreset(minutes);
            }
        }
    }
}
