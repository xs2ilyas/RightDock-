using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RightDock.Modules
{
    public partial class ModulePopupWindow : Window
    {
        private readonly bool _expanded;
        private FrameworkElement _anchor;
        private bool _closing;

        internal ModulePopupWindow(string title, string iconKey, Brush iconBrush, bool expanded)
        {
            InitializeComponent();

            _expanded = expanded;
            Title = title ?? "RightDock";
            TitleText.Text = title ?? "RightDock";

            FrameworkElement icon = IconRepository.CreateIconElement(iconKey, 18, iconBrush);
            if (icon != null) IconHost.Content = icon;

            if (expanded)
            {
                Width = 980;
                Height = 680;
                SizeToContent = SizeToContent.Manual;
                ContentScroll.MaxHeight = double.PositiveInfinity;
            }
            else
            {
                Width = 320;
                SizeToContent = SizeToContent.Height;
                MaxHeight = 640;
                ContentScroll.MaxHeight = 520;
            }

            Deactivated += OnDeactivated;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _closing = true;
            base.OnClosing(e);
        }

        internal void SetContent(FrameworkElement content)
        {
            ContentView.Content = content;
        }

        internal void SetAnchor(FrameworkElement anchor)
        {
            _anchor = anchor;
        }

        internal void PlaceBeside(FrameworkElement anchor)
        {
            Rect work = SystemParameters.WorkArea;

            Point point = anchor.PointToScreen(new Point(0, 0));
            DpiScale dpi = VisualTreeHelper.GetDpi(anchor);
            double anchorX = point.X / dpi.DpiScaleX;
            double anchorY = point.Y / dpi.DpiScaleY;

            UpdateLayout();

            double height = ActualHeight > 0 ? ActualHeight : 240;
            double top = anchorY - 12;

            if (top + height > work.Bottom - 8) top = work.Bottom - height - 8;
            top = Math.Max(work.Top + 8, top);

            Left = Math.Max(work.Left + 8, anchorX - Width - 10);
            Top = top;
        }

        internal void CenterOnWorkArea()
        {
            Rect work = SystemParameters.WorkArea;
            Left = work.Left + (work.Width - Width) / 2;
            Top = work.Top + (work.Height - Height) / 2;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_expanded || _closing) return;
            if (OwnedWindows.Cast<Window>().Any(window => window.IsVisible)) return;

            // Clicking the dock button that opened this popup deactivates it first;
            // keep it alive so the click can toggle it closed instead of reopening.
            if (CursorOverAnchor())
            {
                return;
            }

            // Deactivation can arrive while the window is already closing, which would
            // make a nested Close() throw; defer and re-check the closing state.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closing) Close();
            }));
        }

        private bool CursorOverAnchor()
        {
            if (_anchor == null || !_anchor.IsVisible) return false;

            try
            {
                if (!NativeMethods.GetCursorPos(out NativeMethods.POINT pt)) return false;

                Point topLeft = _anchor.PointToScreen(new Point(0, 0));
                Point bottomRight = _anchor.PointToScreen(new Point(_anchor.ActualWidth, _anchor.ActualHeight));

                return pt.X >= topLeft.X && pt.X <= bottomRight.X
                    && pt.Y >= topLeft.Y && pt.Y <= bottomRight.Y;
            }
            catch
            {
                return false;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
