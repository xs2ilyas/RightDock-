using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using RightDock.Services;

namespace RightDock.Services
{
    internal sealed class Scheduler : IDisposable
    {
        private sealed class Item
        {
            internal string Key;
            internal DateTime DueUtc;
            internal TimeSpan? Interval;
            internal Action Callback;
        }

        private static readonly TimeSpan MaxDelay = TimeSpan.FromHours(1);
        private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(50);

        private readonly object _gate = new object();
        private readonly Dictionary<string, Item> _items = new Dictionary<string, Item>(StringComparer.Ordinal);
        private readonly DispatcherTimer _timer;

        private bool _disposed;

        internal Scheduler()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Normal);
            _timer.Tick += OnTick;
        }

        internal event Action<string, Exception> CallbackFailed;

        internal int Count
        {
            get
            {
                lock (_gate)
                {
                    return _items.Count;
                }
            }
        }

        internal void Schedule(string key, DateTime dueUtc, Action callback, TimeSpan? interval = null)
        {
            if (string.IsNullOrWhiteSpace(key) || callback == null) return;

            lock (_gate)
            {
                if (_disposed) return;

                _items[key] = new Item
                {
                    Key = key,
                    DueUtc = dueUtc.Kind == DateTimeKind.Utc ? dueUtc : dueUtc.ToUniversalTime(),
                    Interval = interval,
                    Callback = callback,
                };
            }

            Rearm();
        }

        internal void Cancel(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            lock (_gate)
            {
                _items.Remove(key);
            }

            Rearm();
        }

        internal bool IsScheduled(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            lock (_gate)
            {
                return _items.ContainsKey(key);
            }
        }

        internal void Reconcile()
        {
            if (_disposed) return;
            OnTick(null, EventArgs.Empty);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _items.Clear();
            }

            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        private void Rearm()
        {
            _timer.Stop();

            TimeSpan delay;

            lock (_gate)
            {
                if (_disposed || _items.Count == 0) return;

                DateTime next = _items.Values.Min(item => item.DueUtc);
                delay = next - DateTime.UtcNow;

                if (delay < MinDelay) delay = MinDelay;
                if (delay > MaxDelay) delay = MaxDelay;
            }

            _timer.Interval = delay;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            _timer.Stop();

            List<Item> due;
            DateTime now = DateTime.UtcNow;

            lock (_gate)
            {
                if (_disposed) return;
                due = _items.Values.Where(item => item.DueUtc <= now).ToList();
            }

            foreach (Item item in due)
            {
                lock (_gate)
                {
                    if (!_items.TryGetValue(item.Key, out Item current) || !ReferenceEquals(current, item)) continue;

                    if (item.Interval.HasValue)
                    {
                        DateTime next = item.DueUtc;
                        int guard = 0;
                        do
                        {
                            next = next.Add(item.Interval.Value);
                            guard++;
                        }
                        while (next <= now && guard < 1000);

                        if (next <= now) next = now.Add(item.Interval.Value);
                        item.DueUtc = next;
                    }
                    else
                    {
                        _items.Remove(item.Key);
                    }
                }

                try
                {
                    item.Callback();
                }
                catch (Exception ex)
                {
                    Log.Error("Scheduled callback failed: " + item.Key, ex);
                    CallbackFailed?.Invoke(item.Key, ex);
                }
            }

            Rearm();
        }
    }
}
