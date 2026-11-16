using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Tasks
{
    public partial class TasksExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush OverdueBrush = ModuleUtil.Brush("#EF8080");
        private static readonly Brush SelectedRowBrush = ModuleUtil.Brush("#16FFFFFF");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly TasksModule _module;
        private string _editingId;
        private int _priority = 2;
        private string _recurrence = "none";
        private bool _deleteArmed;
        private bool _syncing;

        internal TasksExpandedView(TasksModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;

            SearchBox.Text = _module.SearchText;
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
                ViewToday.IsChecked = _module.CurrentView == "today";
                ViewUpcoming.IsChecked = _module.CurrentView == "upcoming";
                ViewCompleted.IsChecked = _module.CurrentView == "completed";
                ViewAll.IsChecked = _module.CurrentView == "all";

                List<TaskItem> tasks = _module.QueryCurrent();
                ListCountText.Text = tasks.Count == 0 ? string.Empty : tasks.Count + " task" + (tasks.Count == 1 ? string.Empty : "s");
                EmptyText.Visibility = tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                TaskPanel.Children.Clear();
                foreach (TaskItem task in tasks)
                {
                    TaskPanel.Children.Add(BuildRow(task));
                }

                if (!string.IsNullOrEmpty(_editingId) && _module.Get(_editingId) == null)
                {
                    ClearEditor();
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(TaskItem task)
        {
            bool selected = string.Equals(task.Id, _editingId, StringComparison.Ordinal);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new ToggleButton
            {
                Style = (Style)FindResource("MdRoundCheck"),
                IsChecked = task.IsCompleted,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(check, "Complete " + task.Title);
            check.Checked += (_, __) =>
            {
                if (!_syncing) _module.ToggleCompleted(task.Id, true);
            };
            check.Unchecked += (_, __) =>
            {
                if (!_syncing) _module.ToggleCompleted(task.Id, false);
            };

            var text = new StackPanel { Margin = new Thickness(9, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

            var title = new TextBlock
            {
                Text = task.Title,
                Foreground = RowTitleBrush,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            if (task.IsCompleted) title.TextDecorations = TextDecorations.Strikethrough;
            text.Children.Add(title);

            string meta = BuildMeta(task);
            if (!string.IsNullOrEmpty(meta))
            {
                text.Children.Add(new TextBlock
                {
                    Text = meta,
                    Foreground = task.IsOverdue ? OverdueBrush : RowMetaBrush,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
            Grid.SetColumn(text, 1);

            var dot = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = ModuleUtil.PriorityBrush(task.Priority),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = ModuleUtil.PriorityName(task.Priority) + " priority",
            };
            Grid.SetColumn(dot, 2);

            grid.Children.Add(check);
            grid.Children.Add(text);
            grid.Children.Add(dot);

            var button = new Button
            {
                Style = (Style)FindResource("MdRowButton"),
                Content = grid,
                Background = selected ? SelectedRowBrush : Brushes.Transparent,
                Tag = task.Id,
            };
            AutomationProperties.SetName(button, "Edit task " + task.Title);
            button.Click += (_, __) =>
            {
                if (_syncing) return;
                LoadEditor(task.Id);
                Refresh();
            };

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = button,
            };
        }

        private static string BuildMeta(TaskItem task)
        {
            var parts = new List<string>();

            if (task.IsCompleted)
            {
                if (task.CompletedUtc.HasValue)
                {
                    parts.Add("Completed " + ModuleUtil.FormatHistoryTime(task.CompletedUtc.Value));
                }
            }
            else if (task.DueUtc.HasValue)
            {
                parts.Add((task.IsOverdue ? "Overdue · " : string.Empty) + ModuleUtil.FormatDue(task.DueUtc));
            }
            else
            {
                parts.Add("No due date");
            }

            if (!string.IsNullOrWhiteSpace(task.CategoryName)) parts.Add(task.CategoryName);
            if (!string.IsNullOrWhiteSpace(task.ProjectName)) parts.Add(task.ProjectName);
            if (!string.IsNullOrWhiteSpace(task.Recurrence) && task.Recurrence != "none")
            {
                parts.Add(ModuleUtil.RecurrenceLabel(task.Recurrence));
            }

            return string.Join("  ·  ", parts);
        }

        private void LoadEditor(string taskId)
        {
            TaskItem task = _module.Get(taskId);
            if (task == null)
            {
                ClearEditor();
                return;
            }

            _editingId = task.Id;
            EditorTitle.Text = "Edit task";
            TitleInput.Text = task.Title ?? string.Empty;
            NotesInput.Text = task.Notes ?? string.Empty;
            DueInput.Text = task.DueUtc.HasValue
                ? task.DueUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : string.Empty;
            CategoryInput.Text = task.CategoryName ?? string.Empty;
            ProjectInput.Text = task.ProjectName ?? string.Empty;
            _priority = task.Priority;
            _recurrence = string.IsNullOrWhiteSpace(task.Recurrence) ? "none" : task.Recurrence;

            UpdateChips();
            DisarmDelete();
            DeleteButton.IsEnabled = true;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void ClearEditor()
        {
            _editingId = null;
            EditorTitle.Text = "New task";
            TitleInput.Text = string.Empty;
            NotesInput.Text = string.Empty;
            DueInput.Text = string.Empty;
            CategoryInput.Text = string.Empty;
            ProjectInput.Text = string.Empty;
            _priority = 2;
            _recurrence = "none";

            UpdateChips();
            DisarmDelete();
            DeleteButton.IsEnabled = false;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void UpdateChips()
        {
            PriorityLow.IsChecked = _priority == 1;
            PriorityNormal.IsChecked = _priority == 2;
            PriorityHigh.IsChecked = _priority == 3;

            RepeatNone.IsChecked = _recurrence == "none";
            RepeatDaily.IsChecked = _recurrence == "daily";
            RepeatWeekly.IsChecked = _recurrence == "weekly";
            RepeatMonthly.IsChecked = _recurrence == "monthly";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string title = TitleInput.Text.Trim();
            if (title.Length == 0)
            {
                ShowError("Give the task a title before saving.");
                return;
            }

            _priority = PriorityHigh.IsChecked == true ? 3 : PriorityLow.IsChecked == true ? 1 : 2;
            _recurrence = RepeatDaily.IsChecked == true ? "daily"
                : RepeatWeekly.IsChecked == true ? "weekly"
                : RepeatMonthly.IsChecked == true ? "monthly"
                : "none";
            UpdateChips();

            DateTime? due = null;
            string dueText = DueInput.Text.Trim();
            if (dueText.Length > 0)
            {
                DateTime? parsed = ModuleUtil.ParseLocalDateTime(dueText);
                if (!parsed.HasValue)
                {
                    ShowError("Could not read the due date. Try 14:30, 2026-11-20 or 11/20 09:00.");
                    return;
                }
                due = parsed.Value.ToUniversalTime();
            }

            var task = new TaskItem
            {
                Id = _editingId,
                Title = title,
                Notes = NotesInput.Text,
                Priority = _priority,
                DueUtc = due,
                CategoryName = CategoryInput.Text.Trim(),
                ProjectName = ProjectInput.Text.Trim(),
                Recurrence = _recurrence,
            };

            string id = _module.Save(task);
            if (id == null)
            {
                ShowError("Could not save the task.");
                return;
            }

            _editingId = id;
            EditorTitle.Text = "Edit task";
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
                StatusText.Text = "Click delete again to remove this task.";
                return;
            }

            _module.DeleteTask(_editingId);
            ClearEditor();
            StatusText.Text = "Task deleted";
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
                _module.SetView(view);
            }
        }

        private void Priority_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string tag
                && int.TryParse(tag, out int priority))
            {
                _priority = priority;
                UpdateChips();
                StatusText.Text = string.Empty;
            }
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string rule)
            {
                _recurrence = rule;
                UpdateChips();
                StatusText.Text = string.Empty;
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            _module.SetSearch(SearchBox.Text);
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
