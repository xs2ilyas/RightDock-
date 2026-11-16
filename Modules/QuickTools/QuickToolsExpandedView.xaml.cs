using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RightDock.Data;

namespace RightDock.Modules.QuickTools
{
    public partial class QuickToolsExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly QuickToolsModule _module;
        private readonly List<ToggleButton> _patternChips = new List<ToggleButton>();
        private int _cycles = 5;
        private double _circleScale = 0.74;
        private bool _syncing;

        internal QuickToolsExpandedView(QuickToolsModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;
            _module.BreathingTick += UpdateBreathUi;
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _module.Changed -= Refresh;
            _module.BreathingTick -= UpdateBreathUi;
        }

        private void Refresh()
        {
            _syncing = true;
            try
            {
                BuildNotes();
                BuildPatterns();
                BuildSessions();
                UpdateBreathStats();
                UpdateBreathUi();
                UpdateScreenBreakUi();
                UpdateCycleChips();
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildNotes()
        {
            List<NoteItem> notes = _module.RecentNotes(8);
            NoteCountText.Text = notes.Count == 0 ? string.Empty : notes.Count + " recent";
            EmptyNotesText.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            NotePanel.Children.Clear();
            foreach (NoteItem note in notes)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock
                {
                    Text = note.DisplayTitle,
                    Foreground = RowTitleBrush,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });

                var parts = new List<string> { ModuleUtil.FormatHistoryTime(note.UpdatedUtc) };
                if (!string.IsNullOrWhiteSpace(note.Category)) parts.Add(note.Category);
                text.Children.Add(new TextBlock
                {
                    Text = string.Join("  ·  ", parts),
                    Foreground = RowMetaBrush,
                    FontSize = 10,
                    Margin = new Thickness(0, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });

                Grid.SetColumn(text, 0);
                grid.Children.Add(text);

                var row = new Button
                {
                    Style = (Style)FindResource("MdRowButton"),
                    Content = grid,
                    Margin = new Thickness(-4, 0, -4, 0),
                };
                AutomationProperties.SetName(row, "Open note " + note.DisplayTitle);
                row.Click += (_, __) => _module.OpenNotes();

                NotePanel.Children.Add(new Border
                {
                    BorderBrush = DividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = row,
                });
            }
        }

        private void BuildPatterns()
        {
            PatternPanel.Children.Clear();
            _patternChips.Clear();

            foreach (BreathingPattern pattern in _module.Patterns)
            {
                string key = pattern.Key;
                var chip = new ToggleButton
                {
                    Style = (Style)FindResource("MdChip"),
                    Content = pattern.Name,
                    ToolTip = pattern.Detail,
                    IsChecked = string.Equals(key, _module.SelectedPattern.Key, StringComparison.Ordinal),
                    Tag = key,
                };
                AutomationProperties.SetName(chip, "Breathing pattern " + pattern.Name);
                chip.Click += (_, __) =>
                {
                    if (!_syncing) _module.SelectPattern(key);
                };

                _patternChips.Add(chip);
                PatternPanel.Children.Add(chip);
            }
        }

        private void BuildSessions()
        {
            List<BreathingSessionItem> sessions = _module.RecentBreathingSessions(4);
            EmptySessionsText.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            SessionPanel.Children.Clear();
            foreach (BreathingSessionItem session in sessions)
            {
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                grid.Children.Add(new TextBlock
                {
                    Text = session.PatternName + "  ·  " + session.Cycles
                        + (session.Cycles == 1 ? " cycle" : " cycles"),
                    Foreground = RowTitleBrush,
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });

                var meta = new TextBlock
                {
                    Text = ModuleUtil.FormatHistoryTime(session.StartedUtc) + "  ·  "
                        + ModuleUtil.FormatDuration(session.DurationSeconds),
                    Foreground = RowMetaBrush,
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                Grid.SetColumn(meta, 1);
                grid.Children.Add(meta);

                SessionPanel.Children.Add(grid);
            }
        }

        private void UpdateBreathStats()
        {
            BreathingStats stats = _module.BreathingStatistics();
            BreathStatsText.Text = stats.TodaySessions == 0
                ? "No sessions today"
                : stats.TodaySessions + (stats.TodaySessions == 1 ? " session · " : " sessions · ")
                    + ModuleUtil.FormatDuration(stats.TodaySeconds) + " today";

            SessionSummaryText.Text = stats.TotalSessions == 0
                ? string.Empty
                : stats.TotalSessions + " total · " + ModuleUtil.FormatDuration(stats.TotalSeconds);
        }

        private void UpdateBreathUi()
        {
            bool active = _module.IsBreathing;

            if (active)
            {
                PhaseTitleText.Text = _module.BreathPhaseName;
                PhaseDetailText.Text = _module.SelectedPattern.Name + " · " + _module.SelectedPattern.Detail;
                CirclePhaseText.Text = _module.BreathSecondsLeft + "s";
                CountdownText.Text = ModuleUtil.FormatClock(_module.BreathElapsedSeconds);
                CycleText.Text = "Cycle " + _module.BreathCycle + " of " + _module.BreathTotalCycles;
                PhaseProgress.Maximum = Math.Max(1, _module.BreathPhaseSeconds);
                PhaseProgress.Value = Math.Max(0, _module.BreathPhaseSeconds - _module.BreathSecondsLeft);
                AnimateCircle();
            }
            else
            {
                PhaseTitleText.Text = "Guided breathing";
                PhaseDetailText.Text = string.IsNullOrWhiteSpace(_module.BreathStatus)
                    ? "Pick a pattern and press start."
                    : _module.BreathStatus;
                CirclePhaseText.Text = "Ready";
                CountdownText.Text = "00:00";
                CycleText.Text = string.Empty;
                PhaseProgress.Value = 0;
                ResetCircle();
            }

            StartButton.IsEnabled = !active;
            PauseButton.IsEnabled = active;
            PauseButton.Content = _module.IsPaused ? "Resume" : "Pause";
            StopButton.IsEnabled = active;

            foreach (ToggleButton chip in _patternChips)
            {
                chip.IsEnabled = !active;
            }

            UpdateCycleChips();
        }

        private void UpdateCycleChips()
        {
            bool idle = !_module.IsBreathing;

            Cycles3.IsChecked = _cycles == 3;
            Cycles5.IsChecked = _cycles == 5;
            Cycles8.IsChecked = _cycles == 8;

            Cycles3.IsEnabled = idle;
            Cycles5.IsEnabled = idle;
            Cycles8.IsEnabled = idle;
        }

        private void AnimateCircle()
        {
            if (_module.IsPaused) return;

            int phaseIndex = _module.BreathPhaseIndex;
            int phaseSeconds = Math.Max(1, _module.BreathPhaseSeconds);
            int remaining = Math.Max(0, Math.Min(phaseSeconds, _module.BreathSecondsLeft));
            double progress = 1.0 - (double)remaining / phaseSeconds;

            double target;
            if (phaseIndex == 0) target = 0.74 + 0.26 * progress;
            else if (phaseIndex == 2) target = 1.0 - 0.26 * progress;
            else target = phaseIndex == 1 ? 1.0 : 0.74;

            var animation = new DoubleAnimation(_circleScale, target, TimeSpan.FromSeconds(0.9));
            BreathScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            BreathScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            _circleScale = target;
        }

        private void ResetCircle()
        {
            BreathScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            BreathScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            BreathScale.ScaleX = 0.74;
            BreathScale.ScaleY = 0.74;
            _circleScale = 0.74;
        }

        private void UpdateScreenBreakUi()
        {
            ScreenBreakToggle.IsChecked = _module.ScreenBreakEnabled;
            ScreenBreakToggle.Content = _module.ScreenBreakEnabled ? "On" : "Off";
            NextBreakText.Text = _module.NextBreakText;
            ScreenBreakHint.Text = "Look 20 feet away for 20 seconds, then stand up and stretch.";

            int breaks = _module.BreaksToday;
            BreaksTodayText.Text = breaks == 0
                ? string.Empty
                : breaks + (breaks == 1 ? " break today" : " breaks today");

            int minutes = _module.ScreenBreakMinutes;
            Minutes15.IsChecked = minutes == 15;
            Minutes30.IsChecked = minutes == 30;
            Minutes45.IsChecked = minutes == 45;
            Minutes60.IsChecked = minutes == 60;

            if (!CustomMinutesInput.IsKeyboardFocused)
            {
                CustomMinutesInput.Text = minutes.ToString();
            }
        }

        private void SaveQuickNote_Click(object sender, RoutedEventArgs e)
        {
            SubmitQuickNote();
        }

        private void QuickNote_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SubmitQuickNote();
            }
        }

        private void SubmitQuickNote()
        {
            string body = QuickNoteInput.Text;
            if (string.IsNullOrWhiteSpace(body)) return;

            if (_module.CaptureNote(body) == null)
            {
                QuickNoteStatus.Text = "Could not save the note.";
                return;
            }

            QuickNoteStatus.Text = "Saved to Notes.";
            QuickNoteInput.Text = string.Empty;
            QuickNoteInput.Focus();
        }

        private void OpenNotes_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenNotes();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.StartBreathing(_cycles);
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.TogglePauseBreathing();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.StopBreathing(true);
        }

        private void Cycles_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (sender is ContentControl control
                && int.TryParse(control.Content?.ToString(), out int parsed))
            {
                _cycles = parsed;
            }

            UpdateCycleChips();
        }

        private void ScreenBreak_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.SetScreenBreakEnabled(ScreenBreakToggle.IsChecked == true);
        }

        private void Minutes_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (sender is FrameworkElement element && element.Tag is string tag
                && int.TryParse(tag, out int minutes))
            {
                _module.SetScreenBreakMinutes(minutes);
            }
        }

        private void ApplyMinutes_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (!int.TryParse(CustomMinutesInput.Text.Trim(), out int minutes)
                || minutes < 1 || minutes > 240)
            {
                ScreenBreakHint.Text = "Enter a whole number between 1 and 240 minutes.";
                return;
            }

            _module.SetScreenBreakMinutes(minutes);
        }

        private void RemindNow_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.FireBreakNow();
        }

        private void ResetTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.ResetScreenBreakTimer();
        }
    }
}
