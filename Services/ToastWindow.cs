using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace RightDock.Services
{
    internal sealed class ToastWindow : Window
    {
        private const double ToastWidth = 336;

        private readonly Action _onClick;
        private readonly DispatcherTimer _dismissTimer;
        private bool _closing;

        internal ToastWindow(string title, string message, Action onClick)
        {
            _onClick = onClick;

            Width = ToastWidth;
            SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Opacity = 0;

            Content = BuildCard(title, message);

            _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _dismissTimer.Tick += (_, __) => BeginClose();

            Loaded += OnLoaded;
        }

        internal void ShowToast()
        {
            Show();
            _dismissTimer.Start();
        }

        internal void BeginClose()
        {
            if (_closing) return;
            _closing = true;
            _dismissTimer.Stop();

            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, __) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            long exStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE,
                new IntPtr(exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE));

            UpdateLayout();

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            BeginAnimation(OpacityProperty, fadeIn);
        }

        private FrameworkElement BuildCard(string title, string message)
        {
            var icon = IconRepository.CreateIconElement("lucide:bell", 18,
                CreateFrozenBrush(Color.FromRgb(0xD6, 0xD6, 0xD6)));

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            text.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(title) ? "RightDock" : title,
                Foreground = CreateFrozenBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            if (!string.IsNullOrWhiteSpace(message))
            {
                text.Children.Add(new TextBlock
                {
                    Text = message,
                    Foreground = CreateFrozenBrush(Color.FromRgb(0xB9, 0xB9, 0xB9)),
                    FontSize = 11.5,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 16,
                    Margin = new Thickness(0, 3, 0, 0),
                });
            }

            var close = new Button
            {
                Content = "\u2715",
                Width = 22,
                Height = 22,
                FontSize = 10,
                Foreground = CreateFrozenBrush(Color.FromRgb(0x8E, 0x8E, 0x8E)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Focusable = false,
            };
            close.Click += (_, __) => BeginClose();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (icon != null)
            {
                icon.Margin = new Thickness(0, 2, 11, 0);
                icon.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(icon, 0);
                grid.Children.Add(icon);
            }

            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            Grid.SetColumn(close, 2);
            grid.Children.Add(close);

            var card = new Border
            {
                Margin = new Thickness(14),
                Padding = new Thickness(14, 12, 10, 12),
                CornerRadius = new CornerRadius(12),
                Background = CreateFrozenBrush(Color.FromArgb(0xF5, 0x24, 0x24, 0x26)),
                BorderBrush = CreateFrozenBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = grid,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 3,
                    Opacity = 0.45,
                    Color = Colors.Black,
                },
            };

            card.MouseLeftButtonUp += (_, __) =>
            {
                try
                {
                    _onClick?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error("Notification click handler failed.", ex);
                }

                BeginClose();
            };

            return card;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
