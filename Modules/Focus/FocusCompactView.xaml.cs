using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RightDock.Data;

namespace RightDock.Modules.Focus
{
    public partial class FocusCompactView : UserControl
    {
        private static readonly System.Windows.Media.Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly System.Windows.Media.Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");

        private readonly FocusModule _module;
        private bool _syncing;
        private string _historyKey;

        internal FocusCompactView(FocusModule module)
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
                    : _module.TaskTitle;

                Progress.Value = _module.Progress;

                TodayValue.Text = ModuleUtil.FormatMinutes(_module.Stats.TodaySeconds);
                SessionsValue.Text = _module.Stats.TodaySessions.ToString();

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

        private void BuildHistory()
        {
            HistoryPanel.Children.Clear();

            List<FocusSessionItem> items = _module.History.Take(4).ToList();
            EmptyHistory.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (FocusSessionItem session in items)
            {
                var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(session.TaskTitle) ? "Focus session" : session.TaskTitle,
                    Foreground = RowTitleBrush,
                    FontSize = 11.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var meta = new TextBlock
                {
                    Text = ModuleUtil.FormatHistoryTime(session.StartedUtc) + "  ·  "
                        + ModuleUtil.FormatMinutes(session.ActualSeconds),
                    Foreground = RowMetaBrush,
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                Grid.SetColumn(meta, 1);

                grid.Children.Add(title);
                grid.Children.Add(meta);
                HistoryPanel.Children.Add(grid);
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

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenDashboard();
        }
    }
}
