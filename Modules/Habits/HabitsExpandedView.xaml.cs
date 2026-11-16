using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Habits
{
    public partial class HabitsExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush SelectedRowBrush = ModuleUtil.Brush("#16FFFFFF");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");
        private static readonly Brush DotDoneBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush DotEmptyBrush = ModuleUtil.Brush("#26FFFFFF");

        private readonly HabitsModule _module;
        private string _editingId;
        private string _type = "boolean";
        private bool _deleteArmed;
        private bool _syncing;

        internal HabitsExpandedView(HabitsModule module)
        {
            _module = module;
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _module.Changed += Refresh;
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
                List<HabitItem> items = _module.Items.ToList();
                ListCountText.Text = items.Count == 0
                    ? string.Empty
                    : items.Count + (items.Count == 1 ? " habit" : " habits")
                        + "  ·  " + _module.CompletedToday + " done today";
                EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                HabitPanel.Children.Clear();
                foreach (HabitItem item in items)
                {
                    HabitPanel.Children.Add(BuildRow(item));
                }

                BuildWeek(items);

                if (_editingId != null && items.All(item => item.Id != _editingId))
                {
                    ClearEditor();
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(HabitItem item)
        {
            bool selected = string.Equals(item.Id, _editingId, StringComparison.Ordinal);

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
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = BuildMeta(item),
                Foreground = item.TodayCompleted ? DotDoneBrush : RowMetaBrush,
                FontSize = 10.5,
                Margin = new Thickness(0, 2, 0, 0),
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

            var row = new Button
            {
                Style = (Style)FindResource("MdRowButton"),
                Content = grid,
                Background = selected ? SelectedRowBrush : Brushes.Transparent,
            };
            AutomationProperties.SetName(row, "Edit habit " + item.Name);
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

        private void BuildWeek(List<HabitItem> items)
        {
            WeekPanel.Children.Clear();

            if (items.Count == 0)
            {
                WeekSummary.Text = string.Empty;
                return;
            }

            int doneDays = items.Sum(item => item.WeekCompleted.Count(done => done));
            WeekSummary.Text = doneDays + " of " + (items.Count * 7) + " days done";

            var header = CreateWeekGrid();
            var dayLetters = new string[7];
            for (int i = 0; i < 7; i++)
            {
                dayLetters[i] = DateTime.Today.AddDays(i - 6).ToString("ddd");
            }

            header.Children.Add(new TextBlock
            {
                Text = "HABIT",
                Style = (Style)FindResource("MdSectionTitle"),
                Margin = new Thickness(1, 0, 0, 4),
            });

            for (int i = 0; i < 7; i++)
            {
                var label = new TextBlock
                {
                    Text = dayLetters[i],
                    Foreground = RowMetaBrush,
                    FontSize = 9.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                Grid.SetColumn(label, i + 1);
                header.Children.Add(label);
            }

            var streakHeader = new TextBlock
            {
                Text = "STREAK",
                Foreground = RowMetaBrush,
                FontSize = 9.5,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 2, 4),
            };
            Grid.SetColumn(streakHeader, 8);
            header.Children.Add(streakHeader);

            WeekPanel.Children.Add(header);

            foreach (HabitItem item in items)
            {
                var row = CreateWeekGrid();

                row.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    Foreground = RowTitleBrush,
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(1, 0, 6, 0),
                });

                for (int i = 0; i < 7; i++)
                {
                    DateTime date = DateTime.Today.AddDays(i - 6);
                    var dot = new Border
                    {
                        Width = 12,
                        Height = 12,
                        CornerRadius = new CornerRadius(6),
                        Background = item.WeekCompleted[i] ? DotDoneBrush : DotEmptyBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = date.ToString("MMM d") + (item.WeekCompleted[i] ? " · done" : " · not done"),
                    };
                    AutomationProperties.SetName(dot, "Habit " + item.Name + " " + date.ToString("yyyy-MM-dd"));
                    Grid.SetColumn(dot, i + 1);
                    row.Children.Add(dot);
                }

                var streak = new TextBlock
                {
                    Text = item.CurrentStreak + "d",
                    Foreground = item.CurrentStreak > 0 ? DotDoneBrush : RowMetaBrush,
                    FontSize = 10.5,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 2, 0),
                };
                Grid.SetColumn(streak, 8);
                row.Children.Add(streak);

                WeekPanel.Children.Add(new Border
                {
                    BorderBrush = DividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(0, 5, 0, 5),
                    Child = row,
                });
            }
        }

        private static Grid CreateWeekGrid()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            for (int i = 0; i < 7; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            return grid;
        }

        private void LoadEditor(string habitId)
        {
            HabitItem item = _module.Get(habitId);
            if (item == null)
            {
                ClearEditor();
                return;
            }

            _editingId = item.Id;
            EditorTitle.Text = "Edit habit";
            NameInput.Text = item.Name ?? string.Empty;
            DescriptionInput.Text = item.Description ?? string.Empty;
            TargetInput.Text = HabitItem.FormatValue(item.TargetValue);
            UnitInput.Text = item.Unit ?? string.Empty;
            _type = item.TrackingType;

            UpdateTypeChips();
            DisarmDelete();
            DeleteButton.IsEnabled = true;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void ClearEditor()
        {
            _editingId = null;
            EditorTitle.Text = "New habit";
            NameInput.Text = string.Empty;
            DescriptionInput.Text = string.Empty;
            TargetInput.Text = "1";
            UnitInput.Text = string.Empty;
            _type = "boolean";

            UpdateTypeChips();
            DisarmDelete();
            DeleteButton.IsEnabled = false;
            StatusText.Text = string.Empty;
            HideError();
        }

        private void UpdateTypeChips()
        {
            TypeBoolean.IsChecked = _type == "boolean";
            TypeCounter.IsChecked = _type == "counter";
            TypeDuration.IsChecked = _type == "duration";

            bool measured = _type != "boolean";
            TargetInput.IsEnabled = measured;
            UnitInput.IsEnabled = measured;
            if (!measured)
            {
                TargetInput.Text = "1";
                UnitInput.Text = string.Empty;
                TargetHint.Text = "Yes / no habits are simply done or not done.";
            }
            else
            {
                TargetHint.Text = "Counter habits count up; duration habits add 5 minutes per tap.";
            }
        }

        private void Type_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string type)
            {
                _type = type;
                UpdateTypeChips();
                StatusText.Text = string.Empty;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            string name = NameInput.Text.Trim();
            if (name.Length == 0)
            {
                ShowError("Give the habit a name.");
                return;
            }

            double target = 1;
            if (_type != "boolean")
            {
                string targetText = TargetInput.Text.Trim();
                bool parsed = double.TryParse(targetText, NumberStyles.Float,
                    CultureInfo.CurrentCulture, out target)
                    || double.TryParse(targetText, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out target);

                if (!parsed || target <= 0 || target > 1_000_000)
                {
                    ShowError("Target must be a positive number.");
                    return;
                }
            }

            var habit = new HabitItem
            {
                Id = _editingId,
                Name = name,
                Description = DescriptionInput.Text,
                TrackingType = _type,
                TargetValue = target,
                Unit = UnitInput.Text.Trim(),
            };

            string id = _module.Save(habit);
            if (id == null)
            {
                ShowError("Could not save the habit.");
                return;
            }

            _editingId = id;
            EditorTitle.Text = "Edit habit";
            DeleteButton.IsEnabled = true;
            DisarmDelete();
            StatusText.Text = "Saved";
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            NameInput.Focus();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
            NameInput.Focus();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingId)) return;

            if (!_deleteArmed)
            {
                _deleteArmed = true;
                DeleteButton.Content = "Confirm delete";
                StatusText.Text = "Click delete again to remove this habit and its history.";
                return;
            }

            _module.Delete(_editingId);
            ClearEditor();
            StatusText.Text = "Habit deleted";
        }

        private void DisarmDelete()
        {
            _deleteArmed = false;
            DeleteButton.Content = "Delete";
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
