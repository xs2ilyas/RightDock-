using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Reminders
{
    public partial class RemindersCompactView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush OverdueBrush = ModuleUtil.Brush("#EF8080");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly RemindersModule _module;
        private bool _syncing;

        internal RemindersCompactView(RemindersModule module)
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
                List<ReminderItem> items = _module.Active();
                CountText.Text = items.Count == 0 ? string.Empty : items.Count + " active";
                EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                ReminderPanel.Children.Clear();
                foreach (ReminderItem item in items)
                {
                    ReminderPanel.Children.Add(BuildRow(item));
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(ReminderItem item)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new ToggleButton
            {
                Style = (Style)FindResource("MdRoundCheck"),
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(check, "Complete reminder " + item.Title);
            check.Click += (_, __) =>
            {
                if (!_syncing) _module.Complete(item.Id);
            };

            var text = new StackPanel { Margin = new Thickness(8, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

            text.Children.Add(new TextBlock
            {
                Text = item.Title,
                Foreground = RowTitleBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = BuildMeta(item),
                Foreground = item.IsOverdue ? OverdueBrush : RowMetaBrush,
                FontSize = 10,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 1);

            var snooze = new Button
            {
                Style = (Style)FindResource("MdGhostButton"),
                Content = "Snooze",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(snooze, "Snooze " + item.Title);
            snooze.Click += (_, __) => OpenSnoozeMenu(snooze, item);
            Grid.SetColumn(snooze, 2);

            grid.Children.Add(check);
            grid.Children.Add(text);
            grid.Children.Add(snooze);

            var row = new Button
            {
                Style = (Style)FindResource("MdRowButton"),
                Content = grid,
                Tag = item.Id,
                Margin = new Thickness(-4, 0, -4, 0),
            };
            AutomationProperties.SetName(row, "Edit reminder " + item.Title);
            row.Click += (_, __) =>
            {
                if (!_syncing) _module.EditFromCompact(item.Id);
            };

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = row,
            };
        }

        private static string BuildMeta(ReminderItem item)
        {
            var parts = new List<string>();

            if (item.SnoozeUntilUtc.HasValue)
            {
                parts.Add("Snoozed · " + ModuleUtil.FormatDue(item.SnoozeUntilUtc));
            }
            else
            {
                parts.Add((item.IsOverdue ? "Overdue · " : string.Empty) + ModuleUtil.FormatDue(item.DueUtc));
            }

            if (!string.IsNullOrWhiteSpace(item.Recurrence) && item.Recurrence != "none")
            {
                parts.Add(ModuleUtil.RecurrenceLabel(item.Recurrence));
            }

            return string.Join("  ·  ", parts);
        }

        private void OpenSnoozeMenu(FrameworkElement anchor, ReminderItem item)
        {
            var menu = new ContextMenu();
            foreach ((string label, int minutes) in new[] { ("5 minutes", 5), ("10 minutes", 10), ("30 minutes", 30), ("1 hour", 60) })
            {
                var entry = new MenuItem { Header = label };
                entry.Click += (_, __) => _module.Snooze(item.Id, minutes);
                menu.Items.Add(entry);
            }

            menu.PlacementTarget = anchor;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            SubmitQuickAdd();
        }

        private void QuickAdd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitQuickAdd();
            }
        }

        private void SubmitQuickAdd()
        {
            string title = QuickAdd.Text;
            if (string.IsNullOrWhiteSpace(title)) return;

            _module.AddQuick(title);
            QuickAdd.Text = string.Empty;
            QuickAdd.Focus();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenDashboard();
        }
    }
}
