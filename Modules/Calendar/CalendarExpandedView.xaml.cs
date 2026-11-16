using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace RightDock.Modules.Calendar
{
    public partial class CalendarExpandedView : UserControl
    {
        private static readonly Brush TitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush MetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush DoneBrush = ModuleUtil.Brush("#6F6F6F");
        private static readonly Brush AccentBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");
        private static readonly Brush SelectedBackground = ModuleUtil.Brush("#1F9CC7E9");
        private static readonly Brush SelectedBorder = ModuleUtil.Brush("#669CC7E9");
        private static readonly Brush TodayBorder = ModuleUtil.Brush("#4D9CC7E9");

        private readonly CalendarModule _module;
        private bool _syncing;
        private bool _quickIsTask = true;

        internal CalendarExpandedView(CalendarModule module)
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
                QuickError.Visibility = Visibility.Collapsed;
                QuickStatus.Visibility = Visibility.Collapsed;

                DateTime gridStart = _module.MonthGridStart();
                List<CalendarEntry> entries = _module.LoadEntries(gridStart, gridStart.AddDays(41));

                MonthTitle.Text = _module.VisibleMonth.ToString("MMMM yyyy");
                int monthCount = entries.Count(entry =>
                    entry.LocalTime.Year == _module.VisibleMonth.Year
                    && entry.LocalTime.Month == _module.VisibleMonth.Month);
                MonthMeta.Text = monthCount == 0
                    ? "No entries"
                    : monthCount + (monthCount == 1 ? " entry" : " entries");

                BuildMonthGrid(gridStart, entries);
                BuildAgenda(entries);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BuildMonthGrid(DateTime gridStart, List<CalendarEntry> entries)
        {
            MonthGrid.Children.Clear();

            DateTime today = DateTime.Today;
            DateTime selected = _module.SelectedDate.Date;

            for (int i = 0; i < 42; i++)
            {
                DateTime date = gridStart.AddDays(i);
                List<CalendarEntry> dayEntries = entries
                    .Where(entry => entry.LocalTime.Date == date)
                    .ToList();

                var button = new Button
                {
                    Style = (Style)FindResource("CalendarDayButton"),
                    Tag = date,
                    Margin = new Thickness(1),
                };

                AutomationProperties.SetName(button, date.ToString("dddd, MMMM d") + " · "
                    + (dayEntries.Count == 0 ? "no entries" : dayEntries.Count + " entries"));
                AutomationProperties.SetAutomationId(button, "CalendarDay" + date.ToString("yyyyMMdd"));

                var content = new StackPanel();
                var header = new Grid();
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                header.Children.Add(new TextBlock
                {
                    Text = date.Day.ToString(),
                    FontSize = 11.5,
                    FontWeight = date == today ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = date == today
                        ? AccentBrush
                        : date.Month == _module.VisibleMonth.Month ? TitleBrush : MetaBrush,
                });

                if (dayEntries.Count > 0)
                {
                    var count = new TextBlock
                    {
                        Text = dayEntries.Count.ToString(),
                        FontSize = 9,
                        Foreground = MetaBrush,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(count, 1);
                    header.Children.Add(count);
                }

                content.Children.Add(header);

                foreach (CalendarEntry entry in dayEntries.Take(2))
                {
                    var line = new TextBlock
                    {
                        Text = entry.Title,
                        FontSize = 9,
                        Foreground = entry.IsDone ? DoneBrush : CalendarCompactView.BrushFor(entry.Kind),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(0, 2, 0, 0),
                    };
                    if (entry.IsDone) line.TextDecorations = TextDecorations.Strikethrough;
                    content.Children.Add(line);
                }

                if (dayEntries.Count > 2)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = "+" + (dayEntries.Count - 2) + " more",
                        FontSize = 8.5,
                        Foreground = MetaBrush,
                        Margin = new Thickness(0, 1, 0, 0),
                    });
                }

                button.Content = content;

                if (date.Month != _module.VisibleMonth.Month)
                {
                    button.Opacity = 0.45;
                }

                if (date == selected)
                {
                    button.Background = SelectedBackground;
                    button.BorderBrush = SelectedBorder;
                }
                else if (date == today)
                {
                    button.BorderBrush = TodayBorder;
                }

                button.Click += (_, __) =>
                {
                    if (!_syncing) _module.SelectDate(date);
                };

                MonthGrid.Children.Add(button);
            }
        }

        private void BuildAgenda(List<CalendarEntry> entries)
        {
            DateTime selected = _module.SelectedDate.Date;
            List<CalendarEntry> dayEntries = entries
                .Where(entry => entry.LocalTime.Date == selected)
                .ToList();

            string prefix = selected == DateTime.Today
                ? "Today"
                : selected == DateTime.Today.AddDays(1)
                    ? "Tomorrow"
                    : selected == DateTime.Today.AddDays(-1)
                        ? "Yesterday"
                        : string.Empty;

            SelectedTitle.Text = (string.IsNullOrEmpty(prefix) ? string.Empty : prefix + " · ")
                + selected.ToString("dddd, MMMM d");
            QuickDateText.Text = "Add to " + selected.ToString("ddd, MMM d");

            int tasks = dayEntries.Count(entry => entry.Kind == CalendarEntryKind.Task);
            int reminders = dayEntries.Count(entry => entry.Kind == CalendarEntryKind.Reminder);
            int doses = dayEntries.Count(entry => entry.Kind == CalendarEntryKind.Medication);
            int focus = dayEntries.Count(entry => entry.Kind == CalendarEntryKind.Focus);
            int done = dayEntries.Count(entry => entry.IsDone);

            var parts = new List<string>();
            if (tasks > 0) parts.Add(tasks == 1 ? "1 task" : tasks + " tasks");
            if (reminders > 0) parts.Add(reminders == 1 ? "1 reminder" : reminders + " reminders");
            if (doses > 0) parts.Add(doses == 1 ? "1 dose" : doses + " doses");
            if (focus > 0) parts.Add(focus == 1 ? "1 focus session" : focus + " focus sessions");
            if (done > 0) parts.Add(done + " done");

            SelectedMeta.Text = parts.Count == 0 ? "No entries" : string.Join(" · ", parts);

            AgendaPanel.Children.Clear();
            EmptyAgenda.Visibility = dayEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (CalendarEntry entry in dayEntries)
            {
                AgendaPanel.Children.Add(BuildAgendaRow(entry));
            }
        }

        private FrameworkElement BuildAgendaRow(CalendarEntry entry)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(new TextBlock
            {
                Text = entry.LocalTime.ToString("HH:mm"),
                Foreground = MetaBrush,
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var stripe = new Border
            {
                Background = CalendarCompactView.BrushFor(entry.Kind),
                Width = 3,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(2, 3, 8, 3),
            };
            Grid.SetColumn(stripe, 1);
            grid.Children.Add(stripe);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock
            {
                Text = entry.Title,
                Foreground = entry.IsDone ? DoneBrush : TitleBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            if (entry.IsDone) title.TextDecorations = TextDecorations.Strikethrough;
            text.Children.Add(title);

            text.Children.Add(new TextBlock
            {
                Text = entry.Detail,
                Foreground = entry.IsOverdue && !entry.IsDone ? ModuleUtil.Brush("#EF8080") : MetaBrush,
                FontSize = 10,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 2);
            grid.Children.Add(text);

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(1, 6, 1, 6),
                Opacity = entry.IsDone ? 0.75 : 1,
                Child = grid,
            };
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.MoveMonth(-1);
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.MoveMonth(1);
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            _module.ShowToday();
        }

        private void QuickKind_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            _quickIsTask = ReferenceEquals(sender, QuickTaskChip);
            QuickTaskChip.IsChecked = _quickIsTask;
            QuickReminderChip.IsChecked = !_quickIsTask;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            QuickError.Visibility = Visibility.Collapsed;
            QuickStatus.Visibility = Visibility.Collapsed;

            string message = _module.AddQuickEntry(_module.SelectedDate, QuickTitle.Text,
                _quickIsTask, QuickTime.Text, out string error);

            if (error != null)
            {
                QuickError.Text = error;
                QuickError.Visibility = Visibility.Visible;
                QuickTitle.Focus();
                return;
            }

            QuickTitle.Text = string.Empty;
            QuickStatus.Text = message;
            QuickStatus.Visibility = Visibility.Visible;
        }
    }
}
