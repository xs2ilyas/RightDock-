using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Health
{
    public partial class HealthExpandedView : UserControl
    {
        private static readonly Brush RowTitleBrush = ModuleUtil.Brush("#EDEDED");
        private static readonly Brush RowMetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush DividerBrush = ModuleUtil.Brush("#1AFFFFFF");
        private static readonly Brush BarEmptyBrush = ModuleUtil.Brush("#1FFFFFFF");

        private static readonly string[] QuickKeys =
        {
            "water", "meals", "sleep", "activity", "vegetables", "fruits", "relaxation",
        };

        private readonly HealthModule _module;
        private bool _syncing;

        internal HealthExpandedView(HealthModule module)
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

                BuildWeight();
                BuildWeek();
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

        private void BuildWeight()
        {
            HealthMetricState weight = _module.State("weight");
            if (weight == null) return;

            WeightValue.Text = weight.Latest.HasValue
                ? weight.Metric.FormatValue(weight.Latest.Value)
                : "—";
            WeightMeta.Text = weight.HasToday ? "Updated today" : "Latest";
            WeightDetail.Text = weight.DaysLogged == 0
                ? "Enter today's weight to start a 7-day trend."
                : "7-day average " + weight.Metric.FormatValue(weight.Average)
                    + "  ·  " + weight.DaysLogged + " of 7 days logged";
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
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var value = new TextBlock
            {
                Text = ProgressText(state),
                Foreground = RowMetaBrush,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 6, 0),
            };
            Grid.SetColumn(value, 1);

            var minus = new Button
            {
                Style = (Style)FindResource("MdGhostButton"),
                Content = "−",
                FontSize = 15,
                Padding = new Thickness(6, 0, 6, 0),
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
                FontSize = 15,
                Padding = new Thickness(6, 0, 6, 0),
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
                Padding = new Thickness(2, 5, 2, 5),
                Child = grid,
            };
        }

        private void BuildWeek()
        {
            WeekPanel.Children.Clear();

            WeekSummary.Text = _module.States.Sum(state => state.DaysLogged) + " entries logged";

            var header = CreateWeekGrid();
            header.Children.Add(new TextBlock
            {
                Text = "METRIC",
                Style = (Style)FindResource("MdSectionTitle"),
                Margin = new Thickness(1, 0, 0, 4),
            });

            for (int i = 0; i < 7; i++)
            {
                var label = new TextBlock
                {
                    Text = DateTime.Today.AddDays(i - 6).ToString("ddd"),
                    Foreground = RowMetaBrush,
                    FontSize = 9.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                Grid.SetColumn(label, i + 1);
                header.Children.Add(label);
            }

            var averageHeader = new TextBlock
            {
                Text = "AVG",
                Foreground = RowMetaBrush,
                FontSize = 9.5,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 2, 4),
            };
            Grid.SetColumn(averageHeader, 8);
            header.Children.Add(averageHeader);

            WeekPanel.Children.Add(header);

            foreach (HealthMetricState state in _module.States)
            {
                var row = CreateWeekGrid();

                row.Children.Add(new TextBlock
                {
                    Text = state.Metric.Name,
                    Foreground = RowTitleBrush,
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(1, 0, 6, 0),
                });

                double max = state.Week.Length == 0 ? 0 : state.Week.Max();
                for (int i = 0; i < 7; i++)
                {
                    double value = state.Week[i];
                    double height = max <= 0 ? 0 : Math.Max(3, 18 * value / max);

                    var host = new Grid { Height = 20, VerticalAlignment = VerticalAlignment.Center };
                    var bar = new Border
                    {
                        Height = value > 0 ? height : 2,
                        Width = 14,
                        CornerRadius = new CornerRadius(2),
                        Background = value > 0 ? ModuleUtil.Brush(state.Metric.ColorHex) : BarEmptyBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        ToolTip = value > 0
                            ? state.Metric.FormatValue(value)
                            : "No entry",
                    };
                    AutomationProperties.SetName(bar,
                        state.Metric.Name + " " + DateTime.Today.AddDays(i - 6).ToString("yyyy-MM-dd"));
                    host.Children.Add(bar);
                    Grid.SetColumn(host, i + 1);
                    row.Children.Add(host);
                }

                var average = new TextBlock
                {
                    Text = state.DaysLogged == 0 ? "—" : HealthMetric.FormatNumber(state.Average),
                    Foreground = state.DaysLogged == 0 ? RowMetaBrush : RowTitleBrush,
                    FontSize = 10.5,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 2, 0),
                    ToolTip = state.DaysLogged == 0
                        ? "No entries in the last 7 days"
                        : state.Metric.FormatValue(state.Average),
                };
                Grid.SetColumn(average, 8);
                row.Children.Add(average);

                WeekPanel.Children.Add(new Border
                {
                    BorderBrush = DividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(0, 4, 0, 4),
                    Child = row,
                });
            }
        }

        private static Grid CreateWeekGrid()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            for (int i = 0; i < 7; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            return grid;
        }

        private static string ProgressText(HealthMetricState state)
        {
            return HealthMetric.FormatNumber(state.Today) + " / "
                + HealthMetric.FormatNumber(state.Metric.Target) + " "
                + state.Metric.Unit;
        }

        private void SaveWeight_Click(object sender, RoutedEventArgs e)
        {
            WeightError.Visibility = Visibility.Collapsed;

            string text = WeightInput.Text.Trim();
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            if (!parsed || value <= 0 || value > 1_000)
            {
                WeightError.Text = "Enter a weight in kilograms, for example 72.4.";
                WeightError.Visibility = Visibility.Visible;
                return;
            }

            if (_module.SaveWeight(value))
            {
                WeightInput.Text = string.Empty;
            }
            else
            {
                WeightError.Text = "Could not save the weight.";
                WeightError.Visibility = Visibility.Visible;
            }
        }
    }
}
