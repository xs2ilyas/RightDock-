using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Reminders
{
    public partial class RemindersExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush OverdueBrush = ModuleUtil.Brush("#EF8080");
        private static readonly Brush SelectedRowBrush = ModuleUtil.Brush("#16FFFFFF");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly RemindersModule _module;
        private string _editingId;
        private string _recurrence = "none";
        private int _customDays = 3;
        private string _view = "active";
        private bool _deleteArmed;
        private bool _syncing;

        internal RemindersExpandedView(RemindersModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;

            string pending = _module.TakePendingEdit();
            if (!string.IsNullOrWhiteSpace(pending))
            {
                LoadEditor(pending);
            }
            else
            {
                ClearEditor();
            }

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
                ViewActive.IsChecked = _view == "active";
                ViewHistory.IsChecked = _view == "history";

                ReminderPanel.Children.Clear();

                if (_view == "history")
                {
                    List<ReminderItem> history = _module.History();
                    ListCountText.Text = history.Count == 0 ? string.Empty : history.Count + " completed";
                    EmptyText.Text = "Completed reminders appear here.";
                    EmptyText.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                    foreach (ReminderItem item in history)
                    {
                        ReminderPanel.Children.Add(BuildHistoryRow(item));
                    }
                }
                else
                {
                    List<ReminderItem> active = _module.Active();
                    ListCountText.Text = active.Count == 0 ? string.Empty : active.Count + " active";
                    EmptyText.Text = "No active reminders.";
                    EmptyText.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                    foreach (ReminderItem item in active)
                    {
                        ReminderPanel.Children.Add(BuildActiveRow(item));
                    }
                }

                if (!string.IsNullOrEmpty(_editingId))
                {
                    ReminderItem editing = _module.Get(_editingId);
                    if (editing == null || editing.IsCompleted) ClearEditor();
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildActiveRow(ReminderItem item)
        {
            bool selected = string.Equals(item.Id, _editingId, StringComparison.Ordinal);

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

            var text = new StackPanel { Margin = new Thickness(9, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = item.Title,
                Foreground = RowTitleBrush,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = BuildActiveMeta(item),
                Foreground = item.IsOverdue ? OverdueBrush : RowMetaBrush,
                FontSize = 10.5,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 1);

            var snooze = new Button
            {
                Style = (Style)FindResource("MdGhostButton"),
                Content = "Snooze",
                FontSize = 10.5,
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
                Background = selected ? SelectedRowBrush : Brushes.Transparent,
                Tag = item.Id,
            };
            AutomationProperties.SetName(row, "Edit reminder " + item.Title);
            row.Click += (_, __) =>
            {
                if (_syncing) return;
                LoadEditor(item.Id);
                Refresh();
            };

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = row,
            };
        }

        private FrameworkElement BuildHistoryRow(ReminderItem item)
        {
            var text = new StackPanel();
            text.Children.Add(new TextBlock
            {
                Text = item.Title,
                Foreground = RowTitleBrush,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            var parts = new List<string>();
            if (item.CompletedUtc.HasValue)
            {
                parts.Add("Completed " + ModuleUtil.FormatHistoryTime(item.CompletedUtc.Value));
            }
            if (!string.IsNullOrWhiteSpace(item.Recurrence) && item.Recurrence != "none")
            {
                parts.Add(ModuleUtil.RecurrenceLabel(item.Recurrence));
            }

            text.Children.Add(new TextBlock
            {
                Text = string.Join("  ·  ", parts),
                Foreground = RowMetaBrush,
                FontSize = 10.5,
                Margin = new Thickness(0, 2, 0, 0),
            });

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(9, 7, 9, 7),
                Child = text,
            };
        }

        private static string BuildActiveMeta(ReminderItem item)
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
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void LoadEditor(string reminderId)
        {
            ReminderItem item = _module.Get(reminderId);
            if (item == null || item.IsCompleted)
            {
                ClearEditor();
                return;
            }

            _editingId = item.Id;
            EditorTitle.Text = "Edit reminder";
            TitleInput.Text = item.Title ?? string.Empty;
            NotesInput.Text = item.Notes ?? string.Empty;
            DueInput.Text = item.EffectiveDueUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            string rule = string.IsNullOrWhiteSpace(item.Recurrence) ? "none" : item.Recurrence;
            if (rule.StartsWith("custom:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(rule.Substring("custom:".Length), out int days))
            {
                _recurrence = "custom";
                _customDays = Math.Max(1, days);
            }
            else
            {
                _recurrence = rule;
                _customDays = 3;
            }

            CustomDaysInput.Text = _customDays.ToString();
            UpdateRepeatChips();
            DisarmDelete();
            DeleteButton.IsEnabled = true;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void ClearEditor()
        {
            _editingId = null;
            EditorTitle.Text = "New reminder";
            TitleInput.Text = string.Empty;
            NotesInput.Text = string.Empty;
            DueInput.Text = string.Empty;
            _recurrence = "none";
            _customDays = 3;
            CustomDaysInput.Text = "3";

            UpdateRepeatChips();
            DisarmDelete();
            DeleteButton.IsEnabled = false;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void UpdateRepeatChips()
        {
            RepeatNone.IsChecked = _recurrence == "none";
            RepeatDaily.IsChecked = _recurrence == "daily";
            RepeatWeekly.IsChecked = _recurrence == "weekly";
            RepeatCustom.IsChecked = _recurrence == "custom";
            CustomDaysPanel.Visibility = _recurrence == "custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string title = TitleInput.Text.Trim();
            if (title.Length == 0)
            {
                ShowError("Give the reminder a title.");
                return;
            }

            string dueText = DueInput.Text.Trim();
            if (dueText.Length == 0)
            {
                ShowError("A reminder needs a due date.");
                return;
            }

            DateTime? parsed = ModuleUtil.ParseLocalDateTime(dueText);
            if (!parsed.HasValue)
            {
                ShowError("Could not read the due date. Try 14:30, 2026-11-20 09:00 or 11/20 18:00.");
                return;
            }

            string rule = RepeatDaily.IsChecked == true ? "daily"
                : RepeatWeekly.IsChecked == true ? "weekly"
                : RepeatCustom.IsChecked == true ? "custom"
                : "none";
            _recurrence = rule;

            if (rule == "custom")
            {
                if (!int.TryParse(CustomDaysInput.Text.Trim(), out int days) || days < 1 || days > 365)
                {
                    ShowError("Custom repetition needs a number of days between 1 and 365.");
                    return;
                }
                rule = "custom:" + days;
                _customDays = days;
            }

            var item = new ReminderItem
            {
                Id = _editingId,
                Title = title,
                Notes = NotesInput.Text,
                DueUtc = parsed.Value.ToUniversalTime(),
                Recurrence = rule,
            };

            string id = _module.Save(item);
            if (id == null)
            {
                ShowError("Could not save the reminder.");
                return;
            }

            _editingId = id;
            EditorTitle.Text = "Edit reminder";
            DeleteButton.IsEnabled = true;
            DisarmDelete();
            StatusText.Text = "Saved";
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            TitleInput.Focus();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            TitleInput.Focus();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            if (!_deleteArmed)
            {
                _deleteArmed = true;
                DeleteButton.Content = "Confirm delete";
                StatusText.Text = "Click delete again to remove this reminder.";
                return;
            }

            _module.Delete(_editingId);
            ClearEditor();
            StatusText.Text = "Reminder deleted";
        }

        private void DisarmDelete()
        {
            _deleteArmed = false;
            DeleteButton.Content = "Delete";
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            if (sender is FrameworkElement element && element.Tag is string view)
            {
                _view = view;
                Refresh();
            }
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string rule)
            {
                _recurrence = rule;
                UpdateRepeatChips();
                StatusText.Text = string.Empty;
            }
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
