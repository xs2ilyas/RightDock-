using System;
using System.Globalization;
using System.Windows.Media;

namespace RightDock.Modules
{
    internal static class ModuleUtil
    {
        internal static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        internal static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static string Iso(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("O", Invariant);
        }

        internal static DateTime? FromIso(object value)
        {
            string text = value as string;
            if (string.IsNullOrWhiteSpace(text)) return null;

            if (DateTime.TryParse(text, Invariant, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : parsed.ToUniversalTime();
            }

            return null;
        }

        internal static DateTime? ParseLocalDateTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            text = text.Trim();
            bool timeOnly = text.Contains(":") && !text.Contains("-") && !text.Contains("/");
            string[] exactFormats =
            {
                "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-dd",
                "M/d/yyyy HH:mm", "M/d/yyyy h:mm tt", "M/d/yyyy",
                "MM/dd/yyyy HH:mm", "MM/dd/yyyy h:mm tt", "MM/dd/yyyy",
                "M/d HH:mm", "M/d H:mm", "M/d",
                "MM/dd HH:mm", "MM/dd H:mm", "MM/dd",
                "HH:mm", "H:mm",
            };

            if (DateTime.TryParseExact(text, exactFormats, CultureInfo.CurrentCulture,
                    DateTimeStyles.AllowWhiteSpaces, out DateTime exact))
            {
                if (timeOnly)
                {
                    DateTime now = DateTime.Now;
                    exact = new DateTime(now.Year, now.Month, now.Day,
                        exact.Hour, exact.Minute, 0);
                    if (exact < now) exact = exact.AddDays(1);
                }

                if (exact.Hour == 0 && exact.Minute == 0 && exact.Second == 0
                    && !text.Contains(":"))
                {
                    exact = exact.AddHours(17);
                }

                return exact;
            }

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture,
                    DateTimeStyles.AssumeLocal, out DateTime loose))
            {
                return loose;
            }

            return null;
        }

        internal static string FormatDue(DateTime? dueUtc)
        {
            if (!dueUtc.HasValue) return string.Empty;

            DateTime local = dueUtc.Value.ToLocalTime();
            DateTime now = DateTime.Now;
            DateTime today = now.Date;

            if (local.Date == today) return "Today " + local.ToString("HH:mm");
            if (local.Date == today.AddDays(1)) return "Tomorrow " + local.ToString("HH:mm");
            if (local.Date > today && local.Date < today.AddDays(7))
            {
                return local.ToString("ddd HH:mm");
            }
            if (local.Year == now.Year) return local.ToString("MMM d HH:mm");
            return local.ToString("MMM d, yyyy");
        }

        internal static string FormatHistoryTime(DateTime utc)
        {
            DateTime local = utc.ToLocalTime();
            return local.Date == DateTime.Today
                ? "Today " + local.ToString("HH:mm")
                : local.ToString("MMM d HH:mm");
        }

        internal static string FormatDay(DateTime dayLocal)
        {
            return dayLocal.Date == DateTime.Today ? "Today" : dayLocal.ToString("ddd");
        }

        internal static string FormatMinutes(int seconds)
        {
            int minutes = (int)Math.Round(seconds / 60.0);
            if (minutes < 60) return minutes + " min";
            return (minutes / 60) + " h " + (minutes % 60).ToString("00") + " min";
        }

        internal static string FormatClock(int seconds)
        {
            if (seconds < 0) seconds = 0;
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        internal static string RecurrenceLabel(string rule)
        {
            if (string.IsNullOrWhiteSpace(rule)) return "Once";
            switch (rule)
            {
                case "daily": return "Daily";
                case "weekly": return "Weekly";
                case "monthly": return "Monthly";
            }
            if (rule.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                string days = rule.Substring("custom:".Length);
                return "Every " + days + " days";
            }
            return "Once";
        }

        internal static DateTime? NextOccurrence(string rule, DateTime fromUtc)
        {
            if (string.IsNullOrWhiteSpace(rule) || rule == "none") return null;

            switch (rule)
            {
                case "daily": return fromUtc.AddDays(1);
                case "weekly": return fromUtc.AddDays(7);
                case "monthly": return fromUtc.AddMonths(1);
            }

            if (rule.StartsWith("custom:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(rule.Substring("custom:".Length), out int days))
            {
                return fromUtc.AddDays(Math.Max(1, days));
            }

            return null;
        }

        internal static string PriorityName(int priority)
        {
            switch (priority)
            {
                case 3: return "High";
                case 1: return "Low";
                default: return "Normal";
            }
        }

        internal static Brush PriorityBrush(int priority)
        {
            switch (priority)
            {
                case 3: return Brush("#EF8080");
                case 1: return Brush("#9A9A9A");
                default: return Brush("#9CC7E9");
            }
        }

        internal static Brush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        internal static string EscapeLike(string text)
        {
            return text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }
    }
}
