using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Medication
{
    internal sealed class MedicationModule : ProductivityModule
    {
        private const string DoseKeyPrefix = "med:dose:";
        private const string RolloverKey = "med:rollover";

        private ModuleContext _context;
        private MedicationStore _store;
        private readonly Dictionary<string, string> _keys = new Dictionary<string, string>(StringComparer.Ordinal);

        private List<MedicationDose> _today = new List<MedicationDose>();
        private List<MedicationDose> _week = new List<MedicationDose>();
        private List<MedicationItem> _medications = new List<MedicationItem>();

        internal event Action Changed;

        public override string Id => "medication";

        public override string Name => "Medication";

        public override string Description => "Medication schedules with daily intake logs, snooze and adherence history.";

        public override string IconKey => "lucide:pill";

        internal IReadOnlyList<MedicationDose> TodayDoses => _today;

        internal IReadOnlyList<MedicationDose> WeekDoses => _week;

        internal IReadOnlyList<MedicationItem> Medications => _medications;

        internal int TakenToday => _today.Count(dose => dose.Status == "taken");

        internal int PlannedToday => _today.Count;

        internal MedicationDose NextDose => _today
            .Where(dose => dose.IsActionable)
            .OrderBy(dose => dose.EffectiveUtc)
            .FirstOrDefault();

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new MedicationStore(context.Database);
        }

        public override void Activate()
        {
            Refresh();
        }

        public override void Deactivate()
        {
            CancelAll();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new MedicationCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new MedicationExpandedView(this);
        }

        internal void Refresh()
        {
            if (_store == null) return;

            _store.EnsureDoses();
            _today = _store.Today();
            _week = _store.LastWeek();
            _medications = _store.Load();

            Reschedule();
            Changed?.Invoke();
        }

        internal MedicationItem Get(string medicationId)
        {
            return _store?.Get(medicationId);
        }

        internal string Save(MedicationItem medication, IList<string> times, string daysOfWeek)
        {
            if (_store == null) return null;

            string id = _store.Save(medication, times, daysOfWeek);
            if (id != null) Refresh();
            return id;
        }

        internal void Delete(string medicationId)
        {
            if (string.IsNullOrWhiteSpace(medicationId)) return;

            _store?.Delete(medicationId);
            Refresh();
        }

        internal void Take(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId)) return;

            _store?.Take(logId);
            Refresh();
        }

        internal void Skip(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId)) return;

            _store?.Skip(logId);
            Refresh();
        }

        internal void Snooze(string logId, int minutes)
        {
            if (string.IsNullOrWhiteSpace(logId)) return;

            _store?.Snooze(logId, minutes);
            Refresh();
        }

        internal void OpenDashboard()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }

        private void Reschedule()
        {
            CancelAll();
            if (_context?.Scheduler == null) return;

            foreach (MedicationDose dose in _today.Where(dose => dose.IsActionable))
            {
                DateTime target = dose.Status == "snoozed"
                    ? dose.SnoozeUntilUtc ?? dose.ScheduledUtc
                    : dose.ScheduledUtc;

                if (target <= DateTime.UtcNow.AddSeconds(1)) continue;

                string key = DoseKeyPrefix + dose.LogId + ":" + target.Ticks.ToString();
                _keys[dose.LogId] = key;

                string logId = dose.LogId;
                _context.Scheduler.Schedule(key, target, () => Fire(logId));
            }

            _context.Scheduler.Schedule(RolloverKey, DateTime.Today.AddDays(1).AddSeconds(2), OnRollover);
        }

        private void Fire(string logId)
        {
            _keys.Remove(logId);

            MedicationDose dose = _store?.GetDose(logId);
            if (dose == null || !dose.IsActionable)
            {
                Changed?.Invoke();
                return;
            }

            string message = string.IsNullOrWhiteSpace(dose.Dosage)
                ? "Scheduled for " + dose.TimeLabel + "."
                : dose.Dosage + "  ·  scheduled for " + dose.TimeLabel + ".";

            _context?.Notifications?.Show("Medication · " + dose.MedicationName, message,
                () => _context.OpenExpanded?.Invoke(Id));

            Changed?.Invoke();
        }

        private void OnRollover()
        {
            Refresh();
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

            _context.Scheduler.Cancel(RolloverKey);
            _keys.Clear();
        }
    }
}
