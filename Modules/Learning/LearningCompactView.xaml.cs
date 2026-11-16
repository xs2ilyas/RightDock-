using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Learning
{
    public partial class LearningCompactView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private readonly LearningModule _module;
        private bool _syncing;

        internal LearningCompactView(LearningModule module)
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
                LearningStats stats = _module.Stats();
                ActiveValue.Text = stats.ActiveGoals.ToString();
                WeekValue.Text = ModuleUtil.FormatMinutes(stats.WeekSeconds);

                List<LearningGoalItem> goals = _module.ActiveGoals(4);
                EmptyText.Visibility = goals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                GoalPanel.Children.Clear();
                foreach (LearningGoalItem goal in goals)
                {
                    GoalPanel.Children.Add(BuildRow(goal));
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private FrameworkElement BuildRow(LearningGoalItem goal)
        {
            var stack = new StackPanel();

            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            top.Children.Add(new TextBlock
            {
                Text = goal.Title,
                Foreground = RowTitleBrush,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var progressText = new TextBlock
            {
                Text = goal.ProgressText,
                Foreground = RowMetaBrush,
                FontSize = 10.5,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(progressText, 1);
            top.Children.Add(progressText);
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
                Margin = new Thickness(-4, 2, -4, 2),
            };
            AutomationProperties.SetName(row, "Open learning goal " + goal.Title);
            row.Click += (_, __) =>
            {
                if (!_syncing) _module.OpenExpandedView();
            };

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = row,
            };
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenExpandedView();
        }
    }
}
