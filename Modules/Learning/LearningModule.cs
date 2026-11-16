using System;
using System.Collections.Generic;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Learning
{
    internal sealed class LearningModule : ProductivityModule
    {
        internal static readonly string[] CategoryOptions = { "Course", "Reading", "Skill", "Other" };

        internal static readonly string[] UnitOptions =
        {
            "minutes", "hours", "pages", "chapters", "lessons", "sessions",
        };

        private ModuleContext _context;
        private LearningStore _store;

        internal event Action Changed;

        public override string Id => "learning";

        public override string Name => "Learning";

        public override string Description => "Learning goals, courses and reading targets with sessions and progress.";

        public override string IconKey => "lucide:graduation-cap";

        internal string SearchText { get; private set; } = string.Empty;

        internal string CategoryFilter { get; private set; } = string.Empty;

        internal string StatusFilter { get; private set; } = "active";

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new LearningStore(context.Database);
        }

        public override void Activate()
        {
            Changed?.Invoke();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new LearningCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new LearningExpandedView(this);
        }

        internal void SetSearch(string text)
        {
            SearchText = text ?? string.Empty;
            Changed?.Invoke();
        }

        internal void SetCategory(string category)
        {
            CategoryFilter = category ?? string.Empty;
            Changed?.Invoke();
        }

        internal void SetStatus(string status)
        {
            StatusFilter = string.IsNullOrWhiteSpace(status) ? "active" : status;
            Changed?.Invoke();
        }

        internal List<LearningGoalItem> Query()
        {
            return _store?.Query(SearchText,
                string.IsNullOrWhiteSpace(CategoryFilter) ? null : CategoryFilter,
                StatusFilter) ?? new List<LearningGoalItem>();
        }

        internal List<string> Categories()
        {
            return _store?.Categories() ?? new List<string>();
        }

        internal List<LearningGoalItem> ActiveGoals(int limit)
        {
            List<LearningGoalItem> goals = _store?.Query(null, null, "active")
                ?? new List<LearningGoalItem>();

            if (limit < goals.Count) goals = goals.GetRange(0, Math.Max(0, limit));
            return goals;
        }

        internal LearningGoalItem Get(string goalId)
        {
            return _store?.Get(goalId);
        }

        internal string Save(LearningGoalItem goal)
        {
            if (goal == null || string.IsNullOrWhiteSpace(goal.Title) || _store == null) return null;

            string id;
            if (string.IsNullOrWhiteSpace(goal.Id))
            {
                id = _store.Add(goal);
            }
            else
            {
                id = _store.Update(goal) ? goal.Id : null;
            }

            if (id != null) Changed?.Invoke();
            return id;
        }

        internal void SetCompleted(string goalId, bool completed)
        {
            _store?.SetCompleted(goalId, completed);
            Changed?.Invoke();
        }

        internal void Delete(string goalId)
        {
            _store?.Delete(goalId);
            Changed?.Invoke();
        }

        internal string LogSession(string goalId, int minutes, string notes)
        {
            if (minutes < 1) return null;

            LearningGoalItem goal = _store?.Get(goalId);
            if (goal == null) return null;

            string sessionId = _store.AddSession(goalId, minutes * 60, notes, DateTime.UtcNow);
            if (sessionId == null) return null;

            double added = 0;
            string unit = (goal.Unit ?? string.Empty).Trim().ToLowerInvariant();
            if (unit == "minutes" || unit == "minute" || unit == "min")
            {
                added = minutes;
            }
            else if (unit == "hours" || unit == "hour" || unit == "h")
            {
                added = minutes / 60.0;
            }

            if (added > 0)
            {
                goal.ProgressValue = Math.Round(goal.ProgressValue + added, 2);
                _store.Update(goal);
            }

            Changed?.Invoke();

            return "Logged " + minutes + " min"
                + (added > 0 ? " · " + goal.ProgressText : string.Empty);
        }

        internal List<LearningSessionItem> RecentSessions(int limit)
        {
            return _store?.RecentSessions(limit) ?? new List<LearningSessionItem>();
        }

        internal List<LearningSessionItem> SessionsFor(string goalId, int limit)
        {
            return _store?.RecentSessions(limit, goalId) ?? new List<LearningSessionItem>();
        }

        internal LearningStats Stats()
        {
            return _store?.GetStats() ?? new LearningStats();
        }

        internal void OpenExpandedView()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }
    }
}
