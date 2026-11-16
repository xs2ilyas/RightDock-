using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RightDock.Modules.Calendar
{
    public partial class CalendarCompactView : UserControl
    {
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush RowDoneBrush = ModuleUtil.Brush("#6F6F6F");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");
        private static readonly Brush TaskBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush ReminderBrush = ModuleUtil.Brush("#D6B67A");
        private static readonly Brush MedicationBrush = ModuleUtil.Brush("#8FD3A8");
        private static readonly Brush FocusBrush = ModuleUtil.Brush("#C7B3E8");

        private readonly CalendarModule _module;
        private readonly DispatcherTimer _timer;
        private bool _syncing;

        internal CalendarCompactView(CalendarModule module)
        {
            _module = module;
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _timer.Tick += (_, __) => Refresh();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;
            _timer.Start();
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _module.Changed -= Refresh;
            _timer.Stop();
        }

        private void Refresh()
        {
            _syncing = true;
            try
            {
                DateTime today = DateTime.Today;
                List<CalendarEntry> entries = _module.LoadEntries(today, today.AddDays(7));

                List<CalendarEntry> todayEntries = entries
                    .Where(entry => entry.LocalTime.Date == today)
                    .ToList();

                TodayTitle.Text = today.ToString("dddd, MMMM d");
                TodayCount.Text = todayEntries.Count == 0
                    ? "No items"
                    : todayEntries.Count + (todayEntries.Count == 1 ? " item" : " items");

                CalendarEntry next = todayEntries.FirstOrDefault(entry => entry.LocalTime >= DateTime.Now);
                TodayHint.Text = next != null
                    ? "Next " + next.LocalTime.ToString("HH:mm")
                    : todayEntries.Count > 0 ? "All done" : string.Empty;

                TodayPanel.Children.Clear();
                TodayEmpty.Visibility = todayEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                foreach (CalendarEntry entry in todayEntries.Take(5))
                {
                    TodayPanel.Children.Add(BuildRow(entry, showDate: false));
                }

                List<CalendarEntry> upcoming = entries
                    .Where(entry => entry.LocalTime.Date > today)
                    .Take(4)
                    .ToList();

                UpcomingPanel.Children.Clear();
                UpcomingPanel.Visibility = upcoming.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                UpcomingEmpty.Visibility = upcoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                foreach (CalendarEntry entry in upcoming)
                {
                    UpcomingPanel.Children.Add(BuildRow(entry, showDate: true));
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(CalendarEntry entry, bool showDate)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var time = new TextBlock
            {
                Text = showDate
                    ? entry.LocalTime.ToString("ddd HH:mm")
                    : entry.LocalTime.ToString("HH:mm"),
                Foreground = RowMetaBrush,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            grid.Children.Add(time);

            var stripe = new Border
            {
                Background = BrushFor(entry.Kind),
                Width = 3,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(2, 2, 7, 2),
            };
            Grid.SetColumn(stripe, 1);
            grid.Children.Add(stripe);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock
            {
                Text = entry.Title,
                Foreground = entry.IsDone ? RowDoneBrush : BrushFor(entry.Kind),
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            if (entry.IsDone) title.TextDecorations = TextDecorations.Strikethrough;
            text.Children.Add(title);
            text.Children.Add(new TextBlock
            {
                Text = entry.Detail,
                Foreground = entry.IsOverdue && !entry.IsDone
                    ? ModuleUtil.Brush("#EF8080")
                    : RowMetaBrush,
                FontSize = 9.5,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 2);
            grid.Children.Add(text);

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(1, 4, 1, 4),
                Child = grid,
            };
        }

        internal static Brush BrushFor(CalendarEntryKind kind)
        {
            switch (kind)
            {
                case CalendarEntryKind.Task: return TaskBrush;
                case CalendarEntryKind.Reminder: return ReminderBrush;
                case CalendarEntryKind.Medication: return MedicationBrush;
                default: return FocusBrush;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.OpenPlanner();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.ShowToday();
            _module.OpenPlanner();
        }
    }
}
