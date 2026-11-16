using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Learning
{
    public partial class LearningExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush SelectedRowBrush = ModuleUtil.Brush("#16FFFFFF");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly LearningModule _module;
        private string _editingId;
        private bool _editingCompleted;
        private string _selectedCategory = string.Empty;
        private string _selectedUnit = "minutes";
        private bool _deleteArmed;
        private bool _syncing;

        internal LearningExpandedView(LearningModule module)
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
            ClearEditor();
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
                BuildStatusChips();
                BuildCategoryFilters();

                List<LearningGoalItem> goals = _module.Query();
                ListCountText.Text = goals.Count == 0
                    ? string.Empty
                    : goals.Count + (goals.Count == 1 ? " goal" : " goals");

                EmptyText.Visibility = goals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                EmptyText.Text = BuildEmptyText();

                GoalPanel.Children.Clear();
                foreach (LearningGoalItem goal in goals)
                {
                    GoalPanel.Children.Add(BuildRow(goal));
                }

                if (!string.IsNullOrEmpty(_editingId))
                {
                    LearningGoalItem editing = _module.Get(_editingId);
                    if (editing == null)
                    {
                        ClearEditor();
                    }
                    else
                    {
                        _editingCompleted = editing.IsCompleted;
                        CompleteButton.IsEnabled = true;
                        CompleteButton.Content = editing.IsCompleted ? "Reopen goal" : "Mark complete";
                        DeleteButton.IsEnabled = true;
                    }
                }
                else
                {
                    CompleteButton.IsEnabled = false;
                    CompleteButton.Content = "Mark complete";
                    DeleteButton.IsEnabled = false;
                }

                BuildSessions();
            }
            finally
            {
                _syncing = false;
            }
        }

        private string BuildEmptyText()
        {
            if (!string.IsNullOrWhiteSpace(_module.SearchText)
                || !string.IsNullOrWhiteSpace(_module.CategoryFilter))
            {
                return "No goals match these filters.";
            }

            switch (_module.StatusFilter)
            {
                case "completed": return "No completed goals yet.";
                case "all": return "No learning goals yet.";
                default: return "No active goals yet. Create one to start tracking.";
            }
        }

        private void BuildStatusChips()
        {
            string status = _module.StatusFilter ?? "active";
            StatusAll.IsChecked = status == "all";
            StatusActive.IsChecked = status == "active";
            StatusCompleted.IsChecked = status == "completed";
        }

        private void BuildCategoryFilters()
        {
            FilterCategoryPanel.Children.Clear();
            FilterCategoryPanel.Children.Add(BuildCategoryChip(string.Empty, "All",
                string.IsNullOrWhiteSpace(_module.CategoryFilter)));

            foreach (string category in _module.Categories())
            {
                FilterCategoryPanel.Children.Add(BuildCategoryChip(category, category,
                    string.Equals(category, _module.CategoryFilter, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private FrameworkElement BuildCategoryChip(string value, string label, bool selected)
        {
            var chip = new ToggleButton
            {
                Style = (Style)FindResource("MdChip"),
                Content = label,
                IsChecked = selected,
                Tag = value,
            };
            AutomationProperties.SetName(chip, value.Length == 0
                ? "All goal categories"
                : "Goal category " + value);

            chip.Click += (_, __) =>
            {
                if (!_syncing) _module.SetCategory(value);
            };

            return chip;
        }

        private FrameworkElement BuildRow(LearningGoalItem goal)
        {
            bool selected = string.Equals(goal.Id, _editingId, StringComparison.Ordinal);

            var stack = new StackPanel();

            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            top.Children.Add(new TextBlock
            {
                Text = (goal.IsCompleted ? "✓  " : string.Empty) + goal.Title,
                Foreground = goal.IsCompleted ? RowMetaBrush : RowTitleBrush,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var progress = new TextBlock
            {
                Text = goal.ProgressText,
                Foreground = RowMetaBrush,
                FontSize = 10.5,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(progress, 1);
            top.Children.Add(progress);
            stack.Children.Add(top);

            stack.Children.Add(new ProgressBar
            {
                Style = (Style)FindResource("MdProgress"),
                Minimum = 0,
                Maximum = 1,
                Value = goal.ProgressFraction,
                Margin = new Thickness(0, 4, 0, 0),
            });

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(goal.Category)) parts.Add(goal.Category);
            if (!string.IsNullOrWhiteSpace(goal.DueText)) parts.Add(goal.DueText);
            if (goal.IsCompleted) parts.Add("Completed");

            if (parts.Count > 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = string.Join("  ·  ", parts),
                    Foreground = RowMetaBrush,
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }

            var row = new Button
            {
                Style = (Style)FindResource("MdRowButton"),
                Content = stack,
                Background = selected ? SelectedRowBrush : Brushes.Transparent,
                Tag = goal.Id,
            };
            AutomationProperties.SetName(row, "Edit learning goal " + goal.Title);
            row.Click += (_, __) =>
            {
                if (_syncing) return;
                LoadEditor(goal.Id);
                Refresh();
            };

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = row,
            };
        }

        private void BuildSessions()
        {
            List<LearningSessionItem> sessions = _module.RecentSessions(3);
            EmptySessionsText.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            SessionPanel.Children.Clear();
            foreach (LearningSessionItem session in sessions)
            {
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string title = string.IsNullOrWhiteSpace(session.GoalTitle) ? "General" : session.GoalTitle;
                string notes = Snippet(session.Notes);
                grid.Children.Add(new TextBlock
                {
                    Text = notes == null ? title : title + "  ·  " + notes,
                    Foreground = RowTitleBrush,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });

                var meta = new TextBlock
                {
                    Text = ModuleUtil.FormatHistoryTime(session.StartedUtc) + "  ·  "
                        + ModuleUtil.FormatMinutes(session.DurationSeconds),
                    Foreground = RowMetaBrush,
                    FontSize = 10,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(meta, 1);
                grid.Children.Add(meta);

                SessionPanel.Children.Add(grid);
            }
        }

        private static string Snippet(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string value = text.Trim().Replace("\r", " ").Replace("\n", " ");
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            return value.Length <= 40 ? value : value.Substring(0, 39) + "…";
        }

        private void LoadEditor(string goalId)
        {
            LearningGoalItem goal = _module.Get(goalId);
            if (goal == null)
            {
                ClearEditor();
                return;
            }

            _editingId = goal.Id;
            _editingCompleted = goal.IsCompleted;
            _selectedCategory = goal.Category ?? string.Empty;
            _selectedUnit = string.IsNullOrWhiteSpace(goal.Unit) ? "minutes" : goal.Unit;

            EditorTitle.Text = "Edit goal";
            TitleInput.Text = goal.Title ?? string.Empty;
            TargetInput.Text = goal.TargetValue > 0 ? LearningGoalItem.FormatNumber(goal.TargetValue) : string.Empty;
            ProgressInput.Text = LearningGoalItem.FormatNumber(goal.ProgressValue);
            DueInput.Text = goal.DueDate.HasValue
                ? goal.DueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Empty;
            DescriptionInput.Text = goal.Description ?? string.Empty;

            SyncCategoryChips();
            SyncUnitChips();

            CompleteButton.IsEnabled = true;
            CompleteButton.Content = goal.IsCompleted ? "Reopen goal" : "Mark complete";
            DeleteButton.IsEnabled = true;
            _deleteArmed = false;
            DeleteButton.Content = "Delete";
            EditorStatus.Text = goal.IsCompleted ? "Completed goal" : string.Empty;
            SessionStatus.Text = string.Empty;
            HideError();
        }

        private void ClearEditor()
        {
            _editingId = null;
            _editingCompleted = false;
            _selectedCategory = string.Empty;
            _selectedUnit = "minutes";

            EditorTitle.Text = "New goal";
            TitleInput.Text = string.Empty;
            TargetInput.Text = string.Empty;
            ProgressInput.Text = string.Empty;
            DueInput.Text = string.Empty;
            DescriptionInput.Text = string.Empty;

            SyncCategoryChips();
            SyncUnitChips();

            CompleteButton.IsEnabled = false;
            CompleteButton.Content = "Mark complete";
            DeleteButton.IsEnabled = false;
            _deleteArmed = false;
            DeleteButton.Content = "Delete";

            EditorStatus.Text = string.Empty;
            SessionStatus.Text = string.Empty;
            HideError();
        }

        private void SyncCategoryChips()
        {
            CategoryCourse.IsChecked = _selectedCategory == "Course";
            CategoryReading.IsChecked = _selectedCategory == "Reading";
            CategorySkill.IsChecked = _selectedCategory == "Skill";
            CategoryOther.IsChecked = _selectedCategory == "Other";
        }

        private void SyncUnitChips()
        {
            UnitMinutes.IsChecked = _selectedUnit == "minutes";
            UnitHours.IsChecked = _selectedUnit == "hours";
            UnitPages.IsChecked = _selectedUnit == "pages";
            UnitChapters.IsChecked = _selectedUnit == "chapters";
            UnitLessons.IsChecked = _selectedUnit == "lessons";
            UnitSessions.IsChecked = _selectedUnit == "sessions";
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            _module.SetSearch(SearchBox.Text);
        }

        private void Status_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (ReferenceEquals(sender, StatusAll)) _module.SetStatus("all");
            else if (ReferenceEquals(sender, StatusCompleted)) _module.SetStatus("completed");
            else _module.SetStatus("active");
        }

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (sender is ToggleButton chip && chip.Tag is string value)
            {
                _selectedCategory = chip.IsChecked == true ? value : string.Empty;
            }

            SyncCategoryChips();
            EditorStatus.Text = string.Empty;
        }

        private void Unit_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (sender is ToggleButton chip && chip.Tag is string value)
            {
                _selectedUnit = value;
            }

            SyncUnitChips();
            EditorStatus.Text = string.Empty;
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();

            if (Array.IndexOf(LearningModule.CategoryOptions, _module.CategoryFilter) >= 0)
            {
                _selectedCategory = _module.CategoryFilter;
                SyncCategoryChips();
            }

            TitleInput.Focus();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            TitleInput.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string title = TitleInput.Text.Trim();
            if (title.Length == 0)
            {
                ShowError("Give the goal a title before saving.");
                return;
            }

            if (!TryParseNumber(TargetInput.Text, out double target) || target < 0)
            {
                ShowError("Target must be a number of 0 or more.");
                return;
            }

            if (!TryParseNumber(ProgressInput.Text, out double progress) || progress < 0)
            {
                ShowError("Progress must be a number of 0 or more.");
                return;
            }

            DateTime? due = null;
            string dueText = DueInput.Text.Trim();
            if (dueText.Length > 0)
            {
                DateTime? parsed = ModuleUtil.ParseLocalDateTime(dueText);
                if (!parsed.HasValue)
                {
                    ShowError("Use a due date like 2026-12-31.");
                    return;
                }

                due = parsed.Value.Date;
            }

            var goal = new LearningGoalItem
            {
                Id = _editingId,
                Title = title,
                Description = DescriptionInput.Text.Trim(),
                Category = _selectedCategory,
                TargetValue = target,
                Unit = _selectedUnit,
                ProgressValue = progress,
                DueDate = due,
                IsCompleted = _editingCompleted,
            };

            string id = _module.Save(goal);
            if (id == null)
            {
                ShowError("Could not save the goal.");
                return;
            }

            _editingId = id;
            EditorTitle.Text = "Edit goal";
            CompleteButton.IsEnabled = true;
            DeleteButton.IsEnabled = true;
            EditorStatus.Text = "Saved";
        }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            bool completed = !_editingCompleted;
            _module.SetCompleted(_editingId, completed);

            _editingCompleted = completed;
            CompleteButton.Content = completed ? "Reopen goal" : "Mark complete";
            EditorStatus.Text = completed ? "Goal completed" : "Goal reopened";
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            if (!_deleteArmed)
            {
                _deleteArmed = true;
                DeleteButton.Content = "Confirm delete";
                EditorStatus.Text = "Click delete again to remove this goal. Its sessions are kept.";
                return;
            }

            _module.Delete(_editingId);
            ClearEditor();
            EditorStatus.Text = "Goal deleted";
        }

        private void LogSession_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId))
            {
                SessionStatus.Text = "Select or save a goal before logging a session.";
                return;
            }

            if (!int.TryParse(SessionMinutesInput.Text.Trim(), out int minutes)
                || minutes < 1 || minutes > 1440)
            {
                SessionStatus.Text = "Enter minutes between 1 and 1440.";
                return;
            }

            string result = _module.LogSession(_editingId, minutes, SessionNotesInput.Text);
            if (result == null)
            {
                SessionStatus.Text = "Could not log the session.";
                return;
            }

            SessionMinutesInput.Text = string.Empty;
            SessionNotesInput.Text = string.Empty;
            SessionStatus.Text = result;

            LearningGoalItem goal = _module.Get(_editingId);
            if (goal != null)
            {
                ProgressInput.Text = LearningGoalItem.FormatNumber(goal.ProgressValue);
            }
        }

        private static bool TryParseNumber(string text, out double value)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                value = 0;
                return true;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, ModuleUtil.Invariant, out value);
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
