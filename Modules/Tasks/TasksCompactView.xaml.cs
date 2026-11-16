using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Tasks
{
    public partial class TasksCompactView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush OverdueBrush = ModuleUtil.Brush("#EF8080");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly TasksModule _module;
        private bool _syncing;

        internal TasksCompactView(TasksModule module)
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
                DateText.Text = DateTime.Today.ToString("ddd, MMM d");

                List<TaskItem> tasks = _module.QueryToday();
                CountText.Text = tasks.Count == 0 ? string.Empty : tasks.Count + " open";
                EmptyText.Visibility = tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                TaskPanel.Children.Clear();
                foreach (TaskItem task in tasks)
                {
                    TaskPanel.Children.Add(BuildRow(task));
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(TaskItem task)
        {
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

            var text = new StackPanel { Margin = new Thickness(8, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

            var title = new TextBlock
            {
                Text = task.Title,
                Foreground = RowTitleBrush,
                FontSize = 12,
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
                    FontSize = 10,
                    Margin = new Thickness(0, 1, 0, 0),
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
                Tag = task.Id,
                Margin = new Thickness(-4, 0, -4, 0),
            };
            AutomationProperties.SetName(button, "Edit task " + task.Title);
            button.Click += (_, __) =>
            {
                if (!_syncing) _module.EditFromCompact(task.Id);
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

            if (!string.IsNullOrWhiteSpace(task.CategoryName)) parts.Add(task.CategoryName);
            if (!string.IsNullOrWhiteSpace(task.ProjectName)) parts.Add(task.ProjectName);
            if (!string.IsNullOrWhiteSpace(task.Recurrence) && task.Recurrence != "none")
            {
                parts.Add(ModuleUtil.RecurrenceLabel(task.Recurrence));
            }

            return string.Join("  ·  ", parts);
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
