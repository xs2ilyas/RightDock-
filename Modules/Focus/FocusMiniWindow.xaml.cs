using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RightDock.Modules.Focus
{
    public partial class FocusMiniWindow : Window
    {
        private static readonly Brush TimeRunningBrush = Frozen("#F5F7FA");
        private static readonly Brush TimePausedBrush = Frozen("#F0B36B");
        private static readonly Brush PinIdleBrush = Frozen("#8B93A3");
        private static readonly Brush PinActiveBrush = Frozen("#8AB4F8");
        private static readonly Brush PinActiveBackground = Frozen("#2E8AB4F8");

        private readonly FocusModule _module;
        private readonly AppSettings _settings;

        internal FocusMiniWindow(FocusModule module, AppSettings settings)
        {
            InitializeComponent();

            _module = module;
            _settings = settings;

            ApplyPinState();
            Refresh();
            _module.Changed += Refresh;
            Closed += OnClosed;
        }

        internal void Present()
        {
            PlaceWindow();
            if (!IsVisible) Show();
            ApplyPinState();
            Refresh();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _module.Changed -= Refresh;
        }

        private void PlaceWindow()
        {
            Rect work = SystemParameters.WorkArea;

            double left = _settings.MiniTimerLeft ?? (work.Right - Width - 18);
            double top = _settings.MiniTimerTop ?? (work.Bottom - Height - 18);

            double minLeft = SystemParameters.VirtualScreenLeft + 4;
            double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width - 4;
            double minTop = SystemParameters.VirtualScreenTop + 4;
            double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height - 4;

            if (maxLeft < minLeft) maxLeft = minLeft;
            if (maxTop < minTop) maxTop = minTop;

            Left = Math.Min(Math.Max(left, minLeft), maxLeft);
            Top = Math.Min(Math.Max(top, minTop), maxTop);
        }

        private void Shell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            e.Handled = true;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            _settings.MiniTimerLeft = Left;
            _settings.MiniTimerTop = Top;
            SettingsStore.Save(_settings);
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            _settings.MiniTimerPinned = !_settings.MiniTimerPinned;
            SettingsStore.Save(_settings);
            ApplyPinState();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ApplyPinState()
        {
            bool pinned = _settings.MiniTimerPinned;

            Topmost = pinned;
            PinButton.Background = pinned ? PinActiveBackground : Brushes.Transparent;
            PinGlyph.Foreground = pinned ? PinActiveBrush : PinIdleBrush;
            PinButton.ToolTip = pinned ? "Unpin from top" : "Keep always on top";
        }

        private void Refresh()
        {
            TimeText.Text = ModuleUtil.FormatClock(_module.RemainingSeconds);

            if (_module.IsRunning)
            {
                TimeText.Foreground = TimeRunningBrush;
                TimeText.ToolTip = "Focusing";
            }
            else if (_module.IsPaused)
            {
                TimeText.Foreground = TimePausedBrush;
                TimeText.ToolTip = "Paused";
            }
            else
            {
                TimeText.Foreground = TimePausedBrush;
                TimeText.ToolTip = "Ready";
            }
        }

        private static Brush Frozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
