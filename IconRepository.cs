using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace RightDock
{
    internal sealed class IconItem
    {
        internal IconItem(string key, string name, string category)
        {
            Key = key;
            Name = name;
            Category = category;
        }

        internal string Key { get; }
        internal string Name { get; }
        internal string Category { get; }
    }

    internal static class IconRepository
    {
        internal static readonly string UserIconsFolder = Path.Combine(PinStore.DataFolder, "icons");

        private static readonly Dictionary<string, IconCatalog.Entry> BuiltIns =
            new Dictionary<string, IconCatalog.Entry>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Geometry> GeometryCache =
            new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<string> CategoryOrder = new List<string>();

        private static List<IconItem> _userIcons = new List<IconItem>();
        private static bool _userLoaded;

        static IconRepository()
        {
            foreach (IconCatalog.Entry entry in IconCatalog.Icons)
            {
                if (BuiltIns.ContainsKey(entry.Key)) continue;
                BuiltIns.Add(entry.Key, entry);
                if (!CategoryOrder.Contains(entry.Category)) CategoryOrder.Add(entry.Category);
            }
        }

        internal static string ResolveKey(string stored)
        {
            return string.IsNullOrWhiteSpace(stored) ? IconCatalog.DefaultKey : stored;
        }

        internal static FrameworkElement CreateIconElement(string key, double size, Brush stroke)
        {
            Geometry geometry = GetGeometry(key);
            if (geometry == null) return null;

            return new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stroke = stroke,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        internal static List<IconItem> GetItems()
        {
            LoadUserIcons(force: false);

            var items = IconCatalog.Icons
                .Select(e => new IconItem(e.Key, e.Name, e.Category))
                .ToList();

            if (_userIcons.Count > 0)
            {
                items.AddRange(_userIcons);
            }

            return items;
        }

        internal static List<string> GetCategoryOrder(List<IconItem> items)
        {
            var order = new List<string>(CategoryOrder);
            foreach (IconItem item in items)
            {
                if (!order.Contains(item.Category)) order.Add(item.Category);
            }
            return order;
        }

        internal static void RefreshUserIcons()
        {
            LoadUserIcons(force: true);
        }

        private static Geometry GetGeometry(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (GeometryCache.TryGetValue(key, out Geometry cached)) return cached;

            if (BuiltIns.TryGetValue(key, out IconCatalog.Entry entry))
            {
                Geometry geometry = TryParse(entry.Path);
                if (geometry != null) GeometryCache[key] = geometry;
                return geometry;
            }

            LoadUserIcons(force: false);
            return GeometryCache.TryGetValue(key, out Geometry userGeometry) ? userGeometry : null;
        }

        private static void LoadUserIcons(bool force)
        {
            if (_userLoaded && !force) return;
            _userLoaded = true;

            _userIcons = new List<IconItem>();

            try
            {
                if (!Directory.Exists(UserIconsFolder)) return;

                foreach (string file in Directory.EnumerateFiles(UserIconsFolder, "*.svg").OrderBy(Path.GetFileName))
                {
                    string key = "user:" + Path.GetFileNameWithoutExtension(file);

                    if (!GeometryCache.ContainsKey(key) || force)
                    {
                        Geometry geometry = TryParseSvg(File.ReadAllText(file));
                        if (geometry == null) continue;
                        GeometryCache[key] = geometry;
                    }

                    _userIcons.Add(new IconItem(key, Path.GetFileNameWithoutExtension(file), "My icons"));
                }
            }
            catch
            {
                // A malformed icons folder should not take the dock down.
            }
        }

        private static Geometry TryParse(string pathData)
        {
            try
            {
                Geometry geometry = Geometry.Parse(pathData);
                geometry.Freeze();
                return geometry;
            }
            catch
            {
                return null;
            }
        }

        private static Geometry TryParseSvg(string svg)
        {
            try
            {
                var group = new GeometryGroup();
                bool any = false;

                foreach (Match m in Regex.Matches(svg, @"<path\b[^>]*?\bd\s*=\s*""([^""]*)""",
                             RegexOptions.IgnoreCase | RegexOptions.Singleline))
                {
                    Geometry path = TryParse(m.Groups[1].Value);
                    if (path != null)
                    {
                        group.Children.Add(path);
                        any = true;
                    }
                }

                foreach (Match m in Regex.Matches(svg, @"<(circle|ellipse|rect|line|polyline|polygon)\b[^>]*>",
                             RegexOptions.IgnoreCase | RegexOptions.Singleline))
                {
                    Geometry shape = ShapeToGeometry(m.Groups[1].Value.ToLowerInvariant(), m.Value);
                    if (shape != null)
                    {
                        group.Children.Add(shape);
                        any = true;
                    }
                }

                if (!any) return null;

                group.Freeze();
                return group;
            }
            catch
            {
                return null;
            }
        }

        private static Geometry ShapeToGeometry(string tag, string text)
        {
            try
            {
                switch (tag)
                {
                    case "circle":
                        return new EllipseGeometry(
                            new Point(Attr(text, "cx", 0), Attr(text, "cy", 0)),
                            Attr(text, "r", 0), Attr(text, "r", 0));

                    case "ellipse":
                        return new EllipseGeometry(
                            new Point(Attr(text, "cx", 0), Attr(text, "cy", 0)),
                            Attr(text, "rx", 0), Attr(text, "ry", 0));

                    case "rect":
                        return new RectangleGeometry(new Rect(
                            Attr(text, "x", 0), Attr(text, "y", 0),
                            Attr(text, "width", 0), Attr(text, "height", 0)));

                    case "line":
                        return new LineGeometry(
                            new Point(Attr(text, "x1", 0), Attr(text, "y1", 0)),
                            new Point(Attr(text, "x2", 0), Attr(text, "y2", 0)));

                    case "polyline":
                    case "polygon":
                    {
                        string points = AttrText(text, "points");
                        if (string.IsNullOrWhiteSpace(points)) return null;

                        double[] numbers = points.Split(new[] { ' ', ',', '\t', '\n', '\r' },
                                StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN)
                            .ToArray();

                        if (numbers.Length < 4 || numbers.Any(double.IsNaN)) return null;

                        var figure = new PathFigure { StartPoint = new Point(numbers[0], numbers[1]), IsClosed = tag == "polygon" };
                        var segment = new PolyLineSegment();
                        for (int i = 2; i + 1 < numbers.Length; i += 2)
                        {
                            segment.Points.Add(new Point(numbers[i], numbers[i + 1]));
                        }
                        figure.Segments.Add(segment);

                        var geometry = new PathGeometry();
                        geometry.Figures.Add(figure);
                        return geometry;
                    }

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static double Attr(string tag, string name, double fallback)
        {
            string value = AttrText(tag, name);
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : fallback;
        }

        private static string AttrText(string tag, string name)
        {
            Match m = Regex.Match(tag, @"\b" + name + @"\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
