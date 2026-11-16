using System;
using System.Collections.Generic;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Reminders
{
    internal sealed class RemindersModule : ProductivityModule
    {
        private const string KeyPrefix = "reminder:";

        private ModuleContext _context;
        private ReminderStore _store;
        private readonly Dictionary<string, string> _keys = new Dictionary<string, string>(StringComparer.Ordinal);

        internal event Action Changed;

        public override string Id => "reminders";

        public override string Name => "Reminders";

        public override string Description => "One-time and recurring reminders with snooze and completion history.";

        public override string IconKey => "lucide:bell";

        internal string PendingEditId { get; set; }

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new ReminderStore(context.Database);
        }

        public override void Activate()
        {
            RescheduleAll();
            Changed?.Invoke();
        }

        public override void Deactivate()
        {
            CancelAll();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new RemindersCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new RemindersExpandedView(this);
        }

        internal List<ReminderItem> Active()
        {
            return _store?.Active() ?? new List<ReminderItem>();
        }

        internal List<ReminderItem> History()
        {
            return _store?.History() ?? new List<ReminderItem>();
        }

        internal ReminderItem Get(string reminderId)
        {
            return _store?.Get(reminderId);
        }

        internal string Save(ReminderItem reminder)
        {
            if (reminder == null || string.IsNullOrWhiteSpace(reminder.Title) || _store == null) return null;

            string id;
            if (string.IsNullOrWhiteSpace(reminder.Id))
            {
                id = _store.Add(reminder);
            }
            else
            {
                id = _store.Update(reminder) ? reminder.Id : null;
            }

            RescheduleAll();
            Changed?.Invoke();
            return id;
        }

        internal void AddQuick(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || _store == null) return;

            _store.Add(new ReminderItem
            {
                Title = title.Trim(),
                DueUtc = DateTime.UtcNow.AddHours(1),
                Recurrence = "none",
            });

            RescheduleAll();
            Changed?.Invoke();
        }

        internal void Complete(string reminderId)
        {
            if (string.IsNullOrWhiteSpace(reminderId)) return;

            _store?.SetCompleted(reminderId);
            RescheduleAll();
            Changed?.Invoke();
        }

        internal void Snooze(string reminderId, int minutes)
        {
            if (string.IsNullOrWhiteSpace(reminderId)) return;

            _store?.Snooze(reminderId, minutes);
            RescheduleAll();
            Changed?.Invoke();
        }

        internal void Delete(string reminderId)
        {
            if (string.IsNullOrWhiteSpace(reminderId)) return;

            _store?.Delete(reminderId);
            RescheduleAll();
            Changed?.Invoke();
        }

        internal void EditFromCompact(string reminderId)
        {
            PendingEditId = reminderId;
            _context?.OpenExpanded?.Invoke(Id);
        }

        internal string TakePendingEdit()
        {
            string id = PendingEditId;
            PendingEditId = null;
            return id;
        }

        internal void OpenDashboard()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }

        private void RescheduleAll()
        {
            CancelAll();
            if (_store == null) return;

            foreach (ReminderItem item in _store.Active())
            {
                Schedule(item);
            }
        }

        private void Schedule(ReminderItem item)
        {
            if (_context?.Scheduler == null) return;

            DateTime fireAt = item.EffectiveDueUtc;
            if (fireAt <= DateTime.UtcNow) fireAt = DateTime.UtcNow.AddSeconds(1);

            string key = KeyPrefix + item.Id + ":" + item.EffectiveDueUtc.Ticks.ToString();
            _keys[item.Id] = key;

            string reminderId = item.Id;
            _context.Scheduler.Schedule(key, fireAt, () => Fire(reminderId));
        }

        private void Fire(string reminderId)
        {
            _keys.Remove(reminderId);

            ReminderItem item = _store?.Get(reminderId);
            if (item == null || item.IsCompleted)
            {
                Changed?.Invoke();
                return;
            }

            if (item.EffectiveDueUtc > DateTime.UtcNow.AddSeconds(2))
            {
                Schedule(item);
                return;
            }

            string message = string.IsNullOrWhiteSpace(item.Notes)
                ? "Due " + ModuleUtil.FormatDue(item.EffectiveDueUtc)
                : item.Notes.Trim();

            _context?.Notifications?.Show("Reminder · " + item.Title, message,
                () => _context.OpenExpanded?.Invoke(Id));

            Changed?.Invoke();
        }

        private void CancelAll()
        {
            if (_context?.Scheduler == null)
            {
                _keys.Clear();
                return;
            }

            foreach (string key in _keys.Values)
            {
                _context.Scheduler.Cancel(key);
            }

            _keys.Clear();
        }
    }
}
