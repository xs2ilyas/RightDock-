using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace RightDock.Modules.Statistics
{
    internal static class StatsChart
    {
        private static readonly Brush MetaBrush = ModuleUtil.Brush("#8C8C8C");
        private static readonly Brush EmptyBrush = ModuleUtil.Brush("#26FFFFFF");
        private static readonly Brush FocusBrush = ModuleUtil.Brush("#9CC7E9");
        private static readonly Brush TaskBrush = ModuleUtil.Brush("#8FD3A8");
        private static readonly Brush HabitBrush = ModuleUtil.Brush("#C7B3E8");
        private static readonly Brush MedicationBrush = ModuleUtil.Brush("#D6B67A");

        internal static void Build(UniformGrid host, IList<StatsBucket> buckets, StatsMetric metric,
            double barHeight, double valueFontSize, double labelFontSize)
        {
            host.Children.Clear();
            if (buckets == null || buckets.Count == 0) return;

            host.Columns = buckets.Count;

            double max = metric == StatsMetric.Habits || metric == StatsMetric.Medication
                ? 100
                : buckets.Max(bucket => MetricValue(bucket, metric));

            Brush brush = MetricBrush(metric);

            foreach (StatsBucket bucket in buckets)
            {
                double value = MetricValue(bucket, metric);
                double height = max <= 0 ? 3 : Math.Max(3, barHeight * value / max);

                var column = new StackPanel { Margin = new Thickness(2, 0, 2, 0) };

                column.Children.Add(new TextBlock
                {
                    Text = value > 0 ? ChartValue(value, metric) : string.Empty,
                    FontSize = valueFontSize,
                    Foreground = MetaBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });

                var barHost = new Grid { Height = barHeight, VerticalAlignment = VerticalAlignment.Bottom };
                barHost.Children.Add(new Border
                {
                    Height = height,
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Background = value > 0 ? brush : EmptyBrush,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    ToolTip = value > 0 ? TooltipValue(value, metric) : "No data",
                });
                column.Children.Add(barHost);

                column.Children.Add(new TextBlock
                {
                    Text = bucket.Label,
                    FontSize = labelFontSize,
                    Foreground = MetaBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0),
                });

                host.Children.Add(column);
            }
        }

        internal static double MetricValue(StatsBucket bucket, StatsMetric metric)
        {
            switch (metric)
            {
                case StatsMetric.Tasks: return bucket.TasksCompleted;
                case StatsMetric.Habits: return bucket.HabitRate * 100;
                case StatsMetric.Medication: return bucket.MedicationRate * 100;
                default: return bucket.FocusSeconds / 60.0;
            }
        }

        internal static string MetricTitle(StatsMetric metric)
        {
            switch (metric)
            {
                case StatsMetric.Tasks: return "Tasks completed";
                case StatsMetric.Habits: return "Habit check-in rate";
                case StatsMetric.Medication: return "Medication adherence";
                default: return "Focus minutes";
            }
        }

        internal static Brush MetricBrush(StatsMetric metric)
        {
            switch (metric)
            {
                case StatsMetric.Tasks: return TaskBrush;
                case StatsMetric.Habits: return HabitBrush;
                case StatsMetric.Medication: return MedicationBrush;
                default: return FocusBrush;
            }
        }

        private static string ChartValue(double value, StatsMetric metric)
        {
            switch (metric)
            {
                case StatsMetric.Habits:
                case StatsMetric.Medication:
                    return Math.Round(value) + "%";
                default:
                    return Math.Round(value) + (metric == StatsMetric.Focus ? "m" : string.Empty);
            }
        }

        private static string TooltipValue(double value, StatsMetric metric)
        {
            switch (metric)
            {
                case StatsMetric.Tasks:
                    return Math.Round(value) + (value == 1 ? " task completed" : " tasks completed");
                case StatsMetric.Habits:
                    return Math.Round(value) + "% of check-ins";
                case StatsMetric.Medication:
                    return Math.Round(value) + "% of doses taken";
                default:
                    return Math.Round(value) + " minutes of focus";
            }
        }
    }
}
