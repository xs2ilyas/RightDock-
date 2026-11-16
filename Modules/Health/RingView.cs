using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RightDock.Data;

namespace RightDock.Modules.Health
{
    public sealed class RingView : Grid
    {
        private const double Diameter = 74;
        private const double Thickness = 6;

        private readonly Path _arc;
        private readonly TextBlock _value;
        private readonly TextBlock _label;

        public RingView()
        {
            Width = Diameter;
            Height = Diameter;

            var track = new Ellipse
            {
                Width = Diameter - Thickness,
                Height = Diameter - Thickness,
                Stroke = ModuleUtil.Brush("#26FFFFFF"),
                StrokeThickness = Thickness,
            };

            _arc = new Path
            {
                StrokeThickness = Thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _value = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = ModuleUtil.Brush("#F5F5F5"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };

            _label = new TextBlock
            {
                FontSize = 9,
                Foreground = ModuleUtil.Brush("#8C8C8C"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0),
            };

            stack.Children.Add(_value);
            stack.Children.Add(_label);

            Children.Add(track);
            Children.Add(_arc);
            Children.Add(stack);
        }

        internal void Update(double value, double target, string label, string unit, string colorHex)
        {
            double fraction = target <= 0 ? 0 : Math.Min(1, Math.Max(0, value / target));

            _arc.Data = BuildArc(fraction);
            _arc.Stroke = ModuleUtil.Brush(colorHex);
            _value.Text = HealthMetric.FormatNumber(value);
            _label.Text = label;

            string valueText = HealthMetric.FormatNumber(value);
            string targetText = HealthMetric.FormatNumber(target);
            ToolTip = valueText + " / " + targetText
                + (string.IsNullOrWhiteSpace(unit) ? string.Empty : " " + unit);
        }

        private static Geometry BuildArc(double fraction)
        {
            if (fraction <= 0) return null;

            double radius = (Diameter - Thickness) / 2;
            double center = Diameter / 2;

            if (fraction >= 0.999)
            {
                var full = new EllipseGeometry(new Point(center, center), radius, radius);
                full.Freeze();
                return full;
            }

            double sweep = 359.99 * fraction;
            Point start = PointOn(center, radius, -90);
            Point end = PointOn(center, radius, -90 + sweep);

            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                context.ArcTo(end, new Size(radius, radius), 0, sweep > 180,
                    SweepDirection.Clockwise, true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static Point PointOn(double center, double radius, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return new Point(
                center + radius * Math.Cos(radians),
                center + radius * Math.Sin(radians));
        }
    }
}
