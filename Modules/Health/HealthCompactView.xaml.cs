using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Health
{
    public partial class HealthCompactView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");

        private static readonly string[] QuickKeys = { "water", "meals", "activity", "sleep" };

        private readonly HealthModule _module;
        private bool _syncing;

        internal HealthCompactView(HealthModule module)
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
                UpdateRing(_module.State("water"), WaterRing, WaterMeta);
                UpdateRing(_module.State("activity"), ActivityRing, ActivityMeta);
                UpdateRing(_module.State("sleep"), SleepRing, SleepMeta);

                QuickPanel.Children.Clear();
                for (int i = 0; i < QuickKeys.Length; i++)
                {
                    HealthMetricState state = _module.State(QuickKeys[i]);
                    if (state == null) continue;
                    QuickPanel.Children.Add(BuildQuickRow(state, i == QuickKeys.Length - 1));
                }

                HealthMetricState weight = _module.State("weight");
                WeightText.Text = weight?.Latest.HasValue == true
                    ? "Latest weight: " + weight.Metric.FormatValue(weight.Latest.Value)
                    : "No weight logged yet.";
            }
            finally
            {
                _syncing = false;
            }
        }

        private static void UpdateRing(HealthMetricState state, RingView ring, TextBlock meta)
        {
            if (state == null) return;

            ring.Update(state.Today, state.Metric.Target, state.Metric.Name,
                state.Metric.Unit, state.Metric.ColorHex);
            meta.Text = ProgressText(state);
        }

        private FrameworkElement BuildQuickRow(HealthMetricState state, bool last)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = state.Metric.Name,
                Foreground = RowTitleBrush,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var value = new TextBlock
            {
                Text = ProgressText(state),
                Foreground = RowMetaBrush,
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0),
            };
            Grid.SetColumn(value, 1);

            var minus = new Button
            {
                Style = (Style)FindResource("MdGhostButton"),
                Content = "−",
                FontSize = 14,
                Padding = new Thickness(5, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(minus, "Remove " + state.Metric.Name);
            minus.Click += (_, __) =>
            {
                if (!_syncing) _module.Subtract(state.Metric.Key);
            };
            Grid.SetColumn(minus, 2);

            var plus = new Button
            {
                Style = (Style)FindResource("MdGhostButton"),
                Content = "+",
                FontSize = 14,
                Padding = new Thickness(5, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(plus, "Add " + state.Metric.Name);
            plus.Click += (_, __) =>
            {
                if (!_syncing) _module.Add(state.Metric.Key);
            };
            Grid.SetColumn(plus, 3);

            grid.Children.Add(name);
            grid.Children.Add(value);
            grid.Children.Add(minus);
            grid.Children.Add(plus);

            return new Border
            {
                BorderBrush = DividerBrush,
                BorderThickness = last ? new Thickness(0) : new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 4, 2, 4),
                Child = grid,
            };
        }

        private static string ProgressText(HealthMetricState state)
        {
            return HealthMetric.FormatNumber(state.Today) + " / "
                + HealthMetric.FormatNumber(state.Metric.Target) + " "
                + state.Metric.Unit;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _module.OpenDashboard();
        }
    }
}
