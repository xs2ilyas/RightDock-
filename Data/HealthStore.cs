using System;
using System.Collections.Generic;
using System.Globalization;
using RightDock.Modules;

namespace RightDock.Data
{
    internal sealed class HealthMetric
    {
        internal HealthMetric(string key, string name, string unit, double target, double step,
            bool isMeasured, string colorHex)
        {
            Key = key;
            Name = name;
            Unit = unit;
            Target = target;
            Step = step;
            IsMeasured = isMeasured;
            ColorHex = colorHex;
        }

        internal string Key { get; }

        internal string Name { get; }

        internal string Unit { get; }

        internal double Target { get; }

        internal double Step { get; }

        internal bool IsMeasured { get; }

        internal string ColorHex { get; }

        internal static string FormatNumber(double value)
        {
            return value == Math.Floor(value)
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        internal string FormatValue(double value)
        {
            string text = FormatNumber(value);
            return string.IsNullOrWhiteSpace(Unit) ? text : text + " " + Unit;
        }

        internal string TargetText => IsMeasured
            ? "Measured"
            : "of " + FormatValue(Target);
    }

    internal sealed class HealthMetricState
    {
        internal HealthMetric Metric { get; set; }

        internal double Today { get; set; }

        internal bool HasToday { get; set; }

        internal double[] Week { get; } = new double[7];

        internal double Average { get; set; }

        internal int DaysLogged { get; set; }

        internal double? Latest { get; set; }
    }

    internal static class HealthMetrics
    {
        internal static readonly HealthMetric[] All =
        {
            new HealthMetric("water", "Water", "glasses", 8, 1, false, "#9CC7E9"),
            new HealthMetric("meals", "Meals", "meals", 3, 1, false, "#D6B67A"),
            new HealthMetric("sleep", "Sleep", "h", 8, 0.5, false, "#A8A6E8"),
            new HealthMetric("activity", "Activity", "min", 30, 5, false, "#8FD3A8"),
            new HealthMetric("vegetables", "Vegetables", "servings", 3, 1, false, "#A8D08D"),
            new HealthMetric("fruits", "Fruits", "servings", 2, 1, false, "#E8A0A0"),
            new HealthMetric("relaxation", "Relaxation", "min", 15, 5, false, "#C7B3E8"),
            new HealthMetric("weight", "Weight", "kg", 0, 0.5, true, "#C9A36B"),
        };

        internal static HealthMetric Find(string key)
        {
            foreach (HealthMetric metric in All)
            {
                if (metric.Key == key) return metric;
            }

            return null;
        }

        internal static readonly string[] RingKeys = { "water", "activity", "sleep" };
    }

    internal sealed class HealthStore
    {
        private readonly Database _database;

        internal HealthStore(Database database)
        {
            _database = database;
        }

        internal List<HealthMetricState> Load()
        {
            var states = new List<HealthMetricState>();
            foreach (HealthMetric metric in HealthMetrics.All)
            {
                states.Add(new HealthMetricState { Metric = metric });
            }

            if (_database == null || !_database.IsAvailable) return states;

            try
            {
                DateTime today = DateTime.Today;
                var values = new Dictionary<string, Dictionary<DateTime, double>>(StringComparer.Ordinal);

                foreach (Dictionary<string, object> row in _database.Query(
                    @"SELECT LogDate, MetricType, Value FROM HealthLogs
                      WHERE LogDate >= @start AND LogDate <= @end",
                    Database.Param("@start", DayKey(today.AddDays(-6))),
                    Database.Param("@end", DayKey(today))))
                {
                    string metric = row["MetricType"] as string;
                    DateTime? date = ParseDayKey(row["LogDate"]);
                    if (metric == null || !date.HasValue) continue;

                    if (!values.TryGetValue(metric, out Dictionary<DateTime, double> byDate))
                    {
                        byDate = new Dictionary<DateTime, double>();
                        values[metric] = byDate;
                    }

                    byDate[date.Value.Date] = Convert.ToDouble(row["Value"], CultureInfo.InvariantCulture);
                }

                foreach (HealthMetricState state in states)
                {
                    if (!values.TryGetValue(state.Metric.Key, out Dictionary<DateTime, double> byDate))
                    {
                        continue;
                    }

                    if (byDate.TryGetValue(today, out double todayValue))
                    {
                        state.Today = todayValue;
                        state.HasToday = true;
                    }

                    double sum = 0;
                    int count = 0;
                    for (int i = 0; i < 7; i++)
                    {
                        DateTime date = today.AddDays(i - 6);
                        if (byDate.TryGetValue(date, out double value))
                        {
                            state.Week[i] = value;
                            sum += value;
                            count++;
                        }
                    }

                    state.Average = count == 0 ? 0 : sum / count;
                    state.DaysLogged = count;
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not load health logs.", ex);
            }

            HealthMetricState weight = states.Find(state => state.Metric.Key == "weight");
            weight.Latest = LatestWeight();

            return states;
        }

        internal bool AddDelta(string metricKey, double direction)
        {
            HealthMetric metric = HealthMetrics.Find(metricKey);
            if (metric == null || metric.IsMeasured || direction == 0) return false;

            double next = TodayValue(metricKey) + metric.Step * Math.Sign(direction);
            return SetToday(metricKey, next);
        }

        internal bool SetToday(string metricKey, double value)
        {
            HealthMetric metric = HealthMetrics.Find(metricKey);
            if (metric == null || _database == null || !_database.IsAvailable) return false;

            value = Math.Round(Math.Max(0, Math.Min(1_000_000, value)), 2);
            DateTime now = DateTime.UtcNow;

            try
            {
                if (value <= 0)
                {
                    _database.Execute(
                        "DELETE FROM HealthLogs WHERE LogDate = @date AND MetricType = @metric;",
                        Database.Param("@date", DayKey(DateTime.Today)),
                        Database.Param("@metric", metricKey));
                    return true;
                }

                return _database.Execute(
                    @"INSERT INTO HealthLogs (LogId, LogDate, MetricType, Value, Unit, Notes, CreatedUtc)
                      VALUES (@id, @date, @metric, @value, @unit, NULL, @now)
                      ON CONFLICT(LogDate, MetricType) DO UPDATE SET Value = excluded.Value;",
                    Database.Param("@id", ModuleUtil.NewId()),
                    Database.Param("@date", DayKey(DateTime.Today)),
                    Database.Param("@metric", metricKey),
                    Database.Param("@value", value),
                    Database.Param("@unit", metric.Unit),
                    Database.Param("@now", ModuleUtil.Iso(now))) > 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not save a health entry.", ex);
                return false;
            }
        }

        private double TodayValue(string metricKey)
        {
            if (_database == null || !_database.IsAvailable) return 0;

            try
            {
                object value = _database.Scalar(
                    "SELECT Value FROM HealthLogs WHERE LogDate = @date AND MetricType = @metric;",
                    Database.Param("@date", DayKey(DateTime.Today)),
                    Database.Param("@metric", metricKey));

                return value == null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not read a health value.", ex);
                return 0;
            }
        }

        private double? LatestWeight()
        {
            try
            {
                object value = _database.Scalar(
                    @"SELECT Value FROM HealthLogs
                      WHERE MetricType = 'weight'
                      ORDER BY LogDate DESC LIMIT 1;");

                return value == null ? (double?)null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Could not read the latest weight.", ex);
                return null;
            }
        }

        private static string DayKey(DateTime localDate)
        {
            return localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static DateTime? ParseDayKey(object value)
        {
            string text = value as string;
            if (string.IsNullOrWhiteSpace(text)) return null;

            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : (DateTime?)null;
        }
    }
}
