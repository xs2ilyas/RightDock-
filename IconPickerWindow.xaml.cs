using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RightDock
{
    public partial class IconPickerWindow : Window
    {
        private static readonly Brush SelectedCellBrush = CreateFrozenBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));

        private readonly string _currentKey;
        private readonly string _appName;
        private readonly ImageSource _appIcon;
        private readonly Brush _iconStroke;

        public string SelectedKey { get; private set; }

        public IconPickerWindow(string currentKey, string appName, ImageSource appIcon, Color iconColor)
        {
            InitializeComponent();

            _currentKey = IconRepository.ResolveKey(currentKey);
            _appName = appName;
            _appIcon = appIcon;
            _iconStroke = CreateFrozenBrush(iconColor);

            IconRepository.RefreshUserIcons();
            Rebuild();

            SearchBox.TextChanged += (_, __) =>
            {
                SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                Rebuild();
            };

            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };

            HeaderBar.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
        }

        private void Rebuild()
        {
            SectionsHost.Children.Clear();

            string filter = (SearchBox.Text ?? string.Empty).Trim();
            List<IconItem> items = IconRepository.GetItems();

            if (filter.Length == 0)
            {
                AddSection("This app", BuildAppIconRow());
            }

            List<IconItem> matches = items
                .Where(i => filter.Length == 0
                            || i.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                            || i.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            foreach (string category in IconRepository.GetCategoryOrder(matches))
            {
                List<IconItem> group = matches.Where(i => i.Category == category).ToList();
                if (group.Count == 0) continue;

                var panel = new WrapPanel();
                foreach (IconItem item in group)
                {
                    panel.Children.Add(BuildIconCell(item));
                }

                AddSection(category, panel);
            }

            if (matches.Count == 0)
            {
                SectionsHost.Children.Add(new TextBlock
                {
                    Text = "No icons match \"" + filter + "\"",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8C, 0x8C, 0x8C)),
                    FontSize = 12,
                    Margin = new Thickness(4, 16, 0, 0),
                });
            }
        }

        private void AddSection(string title, UIElement content)
        {
            SectionsHost.Children.Add(new TextBlock
            {
                Text = title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 12, 0, 4),
            });

            SectionsHost.Children.Add(content);
        }

        private UIElement BuildAppIconRow()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            if (_appIcon != null)
            {
                row.Children.Add(new Image
                {
                    Source = _appIcon,
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(0, 0, 10, 0),
                });
            }

            row.Children.Add(new TextBlock
            {
                Text = "Use " + _appName + "'s own icon",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var button = new Button
            {
                Style = (Style)FindResource("AppIconRow"),
                Content = row,
                Background = _currentKey == IconCatalog.AppIconKey ? SelectedCellBrush : Brushes.Transparent,
            };
            button.Click += (_, __) => Select(IconCatalog.AppIconKey);

            return button;
        }

        private UIElement BuildIconCell(IconItem item)
        {
            FrameworkElement glyph = IconRepository.CreateIconElement(item.Key, 26, _iconStroke);

            var button = new Button
            {
                Style = (Style)FindResource("IconCell"),
                ToolTip = item.Name,
                Content = glyph ?? (object)new TextBlock
                {
                    Text = "?",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                    FontSize = 14,
                },
                Background = item.Key == _currentKey ? SelectedCellBrush : Brushes.Transparent,
            };

            button.Click += (_, __) => Select(item.Key);
            return button;
        }

        private void Select(string key)
        {
            SelectedKey = key;
            DialogResult = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(IconRepository.UserIconsFolder);
                Process.Start(new ProcessStartInfo(IconRepository.UserIconsFolder) { UseShellExecute = true });
            }
            catch
            {
                // Opening the folder is a convenience; ignore failures.
            }
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
