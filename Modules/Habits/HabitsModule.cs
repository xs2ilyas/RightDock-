using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Habits
{
    internal sealed class HabitsModule : ProductivityModule
    {
        private ModuleContext _context;
        private HabitStore _store;
        private List<HabitItem> _items = new List<HabitItem>();

        internal event Action Changed;

        public override string Id => "habits";

        public override string Name => "Habits";

        public override string Description => "Daily habits with counters, durations, streaks and weekly history.";

        public override string IconKey => "lucide:target";

        internal IReadOnlyList<HabitItem> Items => _items;

        internal int CompletedToday => _items.Count(item => item.TodayCompleted);

        internal int TotalCount => _items.Count;

        internal int BestStreak => _items.Count == 0 ? 0 : _items.Max(item => item.CurrentStreak);

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new HabitStore(context.Database);
        }

        public override void Activate()
        {
            _store?.EnsureSeeded();
            Refresh();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new HabitsCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new HabitsExpandedView(this);
        }

        internal void Refresh()
        {
            _items = _store?.Load() ?? new List<HabitItem>();
            Changed?.Invoke();
        }

        internal HabitItem Get(string habitId)
        {
            return _store?.Get(habitId);
        }

        internal string Save(HabitItem habit)
        {
            if (habit == null || string.IsNullOrWhiteSpace(habit.Name) || _store == null) return null;

            string id;
            if (string.IsNullOrWhiteSpace(habit.Id))
            {
                id = _store.Add(habit);
                if (id == null) return null;
            }
            else
            {
                if (!_store.Update(habit)) return null;
                id = habit.Id;
            }

            Refresh();
            return id;
        }

        internal void Toggle(string habitId)
        {
            if (string.IsNullOrWhiteSpace(habitId)) return;
            _store?.ToggleToday(habitId);
            Refresh();
        }

        internal void Increment(string habitId)
        {
            if (string.IsNullOrWhiteSpace(habitId)) return;
            _store?.AddDelta(habitId, 1);
            Refresh();
        }

        internal void Decrement(string habitId)
        {
            if (string.IsNullOrWhiteSpace(habitId)) return;
            _store?.AddDelta(habitId, -1);
            Refresh();
        }

        internal void Delete(string habitId)
        {
            if (string.IsNullOrWhiteSpace(habitId)) return;
            _store?.Delete(habitId);
            Refresh();
        }

        internal void OpenDashboard()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }
    }
}
