using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Habits
{
    public partial class HabitsCompactView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush DoneMetaBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly HabitsModule _module;
        private bool _syncing;

        internal HabitsCompactView(HabitsModule module)
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
                DoneValue.Text = _module.CompletedToday + " of " + _module.TotalCount;

                int streak = _module.BestStreak;
                StreakValue.Text = streak == 1 ? "1 day" : streak + " days";

                HabitPanel.Children.Clear();
                EmptyText.Visibility = _module.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                List<HabitItem> items = _module.Items.Take(6).ToList();
                for (int i = 0; i < items.Count; i++)
                {
                    HabitPanel.Children.Add(BuildRow(items[i], i == items.Count - 1));
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(HabitItem item, bool last)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new ToggleButton
            {
                Style = (Style)FindResource("MdRoundCheck"),
                IsChecked = item.TodayCompleted,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(check, "Toggle habit " + item.Name);
            check.Click += (_, __) =>
            {
                if (!_syncing) _module.Toggle(item.Id);
            };

            var text = new StackPanel
            {
                Margin = new Thickness(9, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            text.Children.Add(new TextBlock
            {
                Text = item.Name,
                Foreground = RowTitleBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = BuildMeta(item),
                Foreground = item.TodayCompleted ? DoneMetaBrush : RowMetaBrush,
                FontSize = 10.5,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 1);

            var plus = new Button
            {
                Style = (Style)FindResource("MdGhostButton"),
                Content = "+",
                FontSize = 15,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = item.IsBoolean ? Visibility.Collapsed : Visibility.Visible,
                IsEnabled = !item.TodayCompleted,
            };
            AutomationProperties.SetName(plus, "Increment habit " + item.Name);
            plus.Click += (_, __) =>
            {
                if (!_syncing) _module.Increment(item.Id);
            };
            Grid.SetColumn(plus, 2);

            grid.Children.Add(check);
            grid.Children.Add(text);
            grid.Children.Add(plus);

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = last ? new Thickness(0) : new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 5, 2, 5),
                Child = grid,
            };
        }

        private static string BuildMeta(HabitItem item)
        {
            string meta = item.IsBoolean
                ? (item.TodayCompleted ? "Done today" : "Not done yet")
                : item.TargetText;

            if (item.CurrentStreak > 0)
            {
                meta += "  ·  " + item.CurrentStreak + "-day streak";
            }

            return meta;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenDashboard();
        }
    }
}
