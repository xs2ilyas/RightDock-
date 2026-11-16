using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Health
{
    internal sealed class HealthModule : ProductivityModule
    {
        private ModuleContext _context;
        private HealthStore _store;
        private List<HealthMetricState> _states = new List<HealthMetricState>();

        internal event Action Changed;

        public override string Id => "health";

        public override string Name => "Health & Lifestyle";

        public override string Description => "Water, meals, sleep, activity, fruit, vegetables, relaxation and weight.";

        public override string IconKey => "lucide:heart-pulse";

        internal IReadOnlyList<HealthMetricState> States => _states;

        internal HealthMetricState State(string key)
        {
            return _states.FirstOrDefault(state => state.Metric.Key == key);
        }

        internal int RingsOnTarget => HealthMetrics.RingKeys.Count(key =>
        {
            HealthMetricState state = State(key);
            return state != null && state.Today >= state.Metric.Target - 0.0001;
        });

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new HealthStore(context.Database);
        }

        public override void Activate()
        {
            Refresh();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new HealthCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new HealthExpandedView(this);
        }

        internal void Refresh()
        {
            _states = _store?.Load() ?? new List<HealthMetricState>();
            Changed?.Invoke();
        }

        internal void Add(string metricKey)
        {
            if (string.IsNullOrWhiteSpace(metricKey)) return;
            _store?.AddDelta(metricKey, 1);
            Refresh();
        }

        internal void Subtract(string metricKey)
        {
            if (string.IsNullOrWhiteSpace(metricKey)) return;
            _store?.AddDelta(metricKey, -1);
            Refresh();
        }

        internal bool SaveWeight(double value)
        {
            bool saved = _store?.SetToday("weight", value) ?? false;
            Refresh();
            return saved;
        }

        internal void OpenDashboard()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }
    }
}
