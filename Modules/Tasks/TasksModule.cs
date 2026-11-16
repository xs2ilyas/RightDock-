using System;
using System.Collections.Generic;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Tasks
{
    internal sealed class TasksModule : ProductivityModule
    {
        private ModuleContext _context;
        private TaskStore _store;

        internal event Action Changed;

        public override string Id => "tasks";

        public override string Name => "Tasks";

        public override string Description => "Tasks with priorities, due dates, categories, projects and recurrence.";

        public override string IconKey => "lucide:clipboard-list";

        internal string CurrentView { get; private set; } = "today";

        internal string SearchText { get; private set; } = string.Empty;

        internal string PendingEditId { get; set; }

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new TaskStore(context.Database);
        }

        public override void Activate()
        {
            Changed?.Invoke();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new TasksCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new TasksExpandedView(this);
        }

        internal void SetView(string view)
        {
            if (string.IsNullOrWhiteSpace(view)) return;
            CurrentView = view;
            Changed?.Invoke();
        }

        internal void SetSearch(string text)
        {
            SearchText = text ?? string.Empty;
            Changed?.Invoke();
        }

        internal void ClearSearch()
        {
            SearchText = string.Empty;
            Changed?.Invoke();
        }

        internal List<TaskItem> QueryCurrent()
        {
            return _store?.Query(CurrentView, SearchText) ?? new List<TaskItem>();
        }

        internal List<TaskItem> QueryToday()
        {
            return _store?.Query("today", null) ?? new List<TaskItem>();
        }

        internal TaskItem Get(string taskId)
        {
            return _store?.Get(taskId);
        }

        internal string Save(TaskItem task)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.Title) || _store == null) return null;

            string id;
            if (string.IsNullOrWhiteSpace(task.Id))
            {
                id = _store.Add(task);
            }
            else
            {
                id = _store.Update(task) ? task.Id : null;
            }

            Changed?.Invoke();
            return id;
        }

        internal void AddQuick(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || _store == null) return;

            _store.Add(new TaskItem { Title = title.Trim(), Priority = 2, Recurrence = "none" });
            Changed?.Invoke();
        }

        internal void ToggleCompleted(string taskId, bool completed)
        {
            if (string.IsNullOrWhiteSpace(taskId)) return;

            _store?.SetCompleted(taskId, completed);
            Changed?.Invoke();
        }

        internal void DeleteTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) return;

            _store?.Delete(taskId);
            Changed?.Invoke();
        }

        internal void EditFromCompact(string taskId)
        {
            PendingEditId = taskId;
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
    }
}
