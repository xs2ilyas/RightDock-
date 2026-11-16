using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace RightDock
{
    public partial class SettingsWindow : Window
    {
        public event Action<bool> AutoHideChanged;
        public event Action<Color> IconColorChanged;

        private static readonly Color[] Presets =
        {
            Color.FromRgb(0xA9, 0xA9, 0xA9),
            Color.FromRgb(0xF5, 0xF5, 0xF5),
            Color.FromRgb(0x5A, 0xB0, 0xF2),
            Color.FromRgb(0x39, 0xC2, 0xB6),
            Color.FromRgb(0x63, 0xC1, 0x74),
            Color.FromRgb(0xE6, 0xC8, 0x4F),
            Color.FromRgb(0xF2, 0x99, 0x4A),
            Color.FromRgb(0xEF, 0x6B, 0x6B),
            Color.FromRgb(0xE5, 0x6B, 0xA8),
            Color.FromRgb(0xA8, 0x8B, 0xEB),
        };

        private readonly AppSettings _settings;
        private readonly List<Button> _swatchButtons = new List<Button>();
        private Button _customSwatch;
        private Color _currentColor;
        private bool _loading = true;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();

            _settings = settings;
            _currentColor = ParseColor(settings.IconColor, Presets[0]);

            BuildSwatches();
            SelectTab(0);

            AutoHideToggle.IsChecked = settings.AutoHide;
            StartupToggle.IsChecked = StartupRegistration.IsEnabled();
            StartupPathRun.Text = StartupRegistration.ExecutablePath;

            AutoHideToggle.Checked += AutoHideToggled;
            AutoHideToggle.Unchecked += AutoHideToggled;
            StartupToggle.Checked += StartupToggled;
            StartupToggle.Unchecked += StartupToggled;

            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };

            HeaderBar.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            _loading = false;
        }

        private void BuildSwatches()
        {
            foreach (Color color in Presets)
            {
                Color captured = color;
                var button = new Button
                {
                    Style = (Style)FindResource("Swatch"),
                    Background = new SolidColorBrush(captured),
                };
                button.Click += (_, __) => ApplyColor(captured);

                SwatchPanel.Children.Add(button);
                _swatchButtons.Add(button);
            }

            _customSwatch = new Button
            {
                Style = (Style)FindResource("Swatch"),
                Background = (Brush)FindResource("Rainbow"),
                ToolTip = "Custom color",
            };
            _customSwatch.Click += (_, __) => PickCustomColor();
            SwatchPanel.Children.Add(_customSwatch);

            UpdateSwatches();
        }

        private void UpdateSwatches()
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                SetSwatchSelected(_swatchButtons[i], Presets[i] == _currentColor,
                    IsLight(Presets[i]) ? Brushes.Black : Brushes.White);
            }

            bool custom = Array.IndexOf(Presets, _currentColor) < 0;
            SetSwatchSelected(_customSwatch, custom, custom ? Brushes.Black : Brushes.White);

            ColorValueText.Text = "Current color   " + ToHex(_currentColor);
        }

        private static void SetSwatchSelected(Button button, bool selected, Brush checkBrush)
        {
            button.Tag = selected ? "1" : null;
            button.BorderBrush = selected ? Brushes.White : Brushes.Transparent;
            button.Foreground = checkBrush;
        }

        private void ApplyColor(Color color)
        {
            _currentColor = Color.FromRgb(color.R, color.G, color.B);
            _settings.IconColor = ToHex(_currentColor);
            SettingsStore.Save(_settings);

            UpdateSwatches();
            IconColorChanged?.Invoke(_currentColor);
        }

        private void PickCustom_Click(object sender, RoutedEventArgs e)
        {
            PickCustomColor();
        }

        private void PickCustomColor()
        {
            using (var dialog = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(_currentColor.R, _currentColor.G, _currentColor.B),
            })
            {
                var owner = new Win32Window(new WindowInteropHelper(this).Handle);
                if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
                {
                    ApplyColor(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
                }
            }
        }

        private void AutoHideToggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            bool enabled = AutoHideToggle.IsChecked == true;
            _settings.AutoHide = enabled;
            SettingsStore.Save(_settings);
            AutoHideChanged?.Invoke(enabled);
        }

        private void StartupToggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            bool enabled = StartupToggle.IsChecked == true;

            try
            {
                StartupRegistration.SetEnabled(enabled);
                StartupError.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StartupError.Text = "Could not update Windows startup: " + ex.Message;
                StartupError.Visibility = Visibility.Visible;

                _loading = true;
                StartupToggle.IsChecked = !enabled;
                _loading = false;
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender == TabGeneral) SelectTab(0);
            else if (sender == TabAppearance) SelectTab(1);
            else SelectTab(2);
        }

        private void SelectTab(int index)
        {
            PageGeneral.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            PageAppearance.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            PageStartup.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

            TabGeneral.Tag = index == 0 ? "1" : null;
            TabAppearance.Tag = index == 1 ? "1" : null;
            TabStartup.Tag = index == 2 ? "1" : null;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static bool IsLight(Color color)
        {
            return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B > 150;
        }

        private static Color ParseColor(string value, Color fallback)
        {
            try
            {
                if (ColorConverter.ConvertFromString(value) is Color parsed)
                {
                    return Color.FromRgb(parsed.R, parsed.G, parsed.B);
                }
            }
            catch
            {
                // Fall through to the fallback color.
            }

            return fallback;
        }

        private sealed class Win32Window : System.Windows.Forms.IWin32Window
        {
            public Win32Window(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }
    }
}
