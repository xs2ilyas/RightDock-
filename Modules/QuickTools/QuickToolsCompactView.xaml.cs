using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace RightDock.Modules.QuickTools
{
    public partial class QuickToolsCompactView : UserControl
    {
        private readonly QuickToolsModule _module;
        private readonly List<ToggleButton> _patternChips = new List<ToggleButton>();
        private int _cycles = 5;
        private bool _syncing;

        internal QuickToolsCompactView(QuickToolsModule module)
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
                int count = _module.NoteCount();
                NoteCountText.Text = count == 0 ? string.Empty : count + " saved";

                BuildPatterns();
                UpdateBreathUi();
                UpdateScreenBreakUi();
                UpdateCycleChips();
            }
            finally
            {
                _syncing = false;
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

        private void UpdateBreathUi()
        {
            bool active = _module.IsBreathing;

            if (active)
            {
                PhaseText.Text = _module.BreathPhaseName;
                BreathMetaText.Text = _module.BreathSecondsLeft + " s left · " + _module.SelectedPattern.Name;
                CycleText.Text = "Cycle " + _module.BreathCycle + " / " + _module.BreathTotalCycles;
                PhaseProgress.Maximum = Math.Max(1, _module.BreathPhaseSeconds);
                PhaseProgress.Value = Math.Max(0, _module.BreathPhaseSeconds - _module.BreathSecondsLeft);
            }
            else
            {
                PhaseText.Text = "Ready";
                BreathMetaText.Text = string.IsNullOrWhiteSpace(_module.BreathStatus)
                    ? "Pick a pattern and start."
                    : _module.BreathStatus;
                CycleText.Text = string.Empty;
                PhaseProgress.Value = 0;
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

        private void UpdateScreenBreakUi()
        {
            ScreenBreakToggle.IsChecked = _module.ScreenBreakEnabled;
            ScreenBreakToggle.Content = _module.ScreenBreakEnabled ? "On" : "Off";
            NextBreakText.Text = _module.NextBreakText;

            int breaks = _module.BreaksToday;
            BreaksTodayText.Text = breaks == 0
                ? string.Empty
                : breaks + (breaks == 1 ? " break today" : " breaks today");

            int minutes = _module.ScreenBreakMinutes;
            Minutes15.IsChecked = minutes == 15;
            Minutes30.IsChecked = minutes == 30;
            Minutes45.IsChecked = minutes == 45;
            Minutes60.IsChecked = minutes == 60;
        }

        private void Capture_Click(object sender, RoutedEventArgs e)
        {
            SubmitCapture();
        }

        private void Capture_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SubmitCapture();
            }
        }

        private void SubmitCapture()
        {
            string body = CaptureInput.Text;
            if (string.IsNullOrWhiteSpace(body)) return;

            if (_module.CaptureNote(body) == null) return;

            CaptureInput.Text = string.Empty;
            CaptureInput.Focus();
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

        private void RemindNow_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.FireBreakNow();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenExpandedView();
        }
    }
}
