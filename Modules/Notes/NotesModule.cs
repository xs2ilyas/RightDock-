using System;
using System.Collections.Generic;
using System.Windows;
using RightDock.Data;

namespace RightDock.Modules.Notes
{
    internal sealed class NotesModule : ProductivityModule
    {
        private ModuleContext _context;
        private NoteStore _store;
        private TaskStore _tasks;
        private ReminderStore _reminders;

        internal event Action Changed;

        public override string Id => "notes";

        public override string Name => "Notes";

        public override string Description => "Quick capture, pinned notes and note-to-task or note-to-reminder conversion.";

        public override string IconKey => "lucide:notebook-pen";

        internal string SearchText { get; private set; } = string.Empty;

        internal string CategoryFilter { get; private set; } = string.Empty;

        internal bool ShowArchived { get; private set; }

        internal string PendingEditId { get; set; }

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new NoteStore(context.Database);
            _tasks = new TaskStore(context.Database);
            _reminders = new ReminderStore(context.Database);
        }

        public override void Activate()
        {
            Changed?.Invoke();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new NotesCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new NotesExpandedView(this);
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

        internal void SetShowArchived(bool show)
        {
            ShowArchived = show;
            Changed?.Invoke();
        }

        internal List<NoteItem> Query()
        {
            return _store?.Query(SearchText,
                string.IsNullOrWhiteSpace(CategoryFilter) ? null : CategoryFilter,
                ShowArchived) ?? new List<NoteItem>();
        }

        internal List<string> Categories()
        {
            return _store?.Categories() ?? new List<string>();
        }

        internal int ArchivedCount()
        {
            return _store?.CountArchived() ?? 0;
        }

        internal List<NoteItem> QueryAll()
        {
            return _store?.Query(null) ?? new List<NoteItem>();
        }

        internal NoteItem Get(string noteId)
        {
            return _store?.Get(noteId);
        }

        internal string Save(NoteItem note)
        {
            if (note == null || _store == null) return null;
            if (string.IsNullOrWhiteSpace(note.Title) && string.IsNullOrWhiteSpace(note.Body)) return null;

            string id;
            if (string.IsNullOrWhiteSpace(note.Id))
            {
                id = _store.Add(note);
            }
            else
            {
                id = _store.Update(note) ? note.Id : null;
            }

            Changed?.Invoke();
            return id;
        }

        internal string Capture(string body)
        {
            if (string.IsNullOrWhiteSpace(body) || _store == null) return null;

            string id = _store.Add(new NoteItem { Body = body.Trim() });
            Changed?.Invoke();
            return id;
        }

        internal void SetPinned(string noteId, bool pinned)
        {
            _store?.SetPinned(noteId, pinned);
            Changed?.Invoke();
        }

        internal void Delete(string noteId)
        {
            _store?.Delete(noteId);
            Changed?.Invoke();
        }

        internal void SetArchived(string noteId, bool archived)
        {
            _store?.SetArchived(noteId, archived);
            Changed?.Invoke();
        }

        internal string ConvertToTask(string noteId)
        {
            NoteItem note = _store?.Get(noteId);
            if (note == null || _tasks == null) return null;

            string title = Shorten(note.DisplayTitle, 120);
            string id = _tasks.Add(new TaskItem
            {
                Title = title,
                Notes = note.Body,
                Priority = 2,
                Recurrence = "none",
            });

            if (id == null) return null;

            _context?.RefreshModule?.Invoke("tasks");
            return "Task created: " + title;
        }

        internal string ConvertToReminder(string noteId)
        {
            NoteItem note = _store?.Get(noteId);
            if (note == null || _reminders == null) return null;

            DateTime due = DateTime.UtcNow.AddHours(1);
            string id = _reminders.Add(new ReminderItem
            {
                Title = Shorten(note.DisplayTitle, 120),
                Notes = note.Body,
                DueUtc = due,
                Recurrence = "none",
            });

            if (id == null) return null;

            _context?.RefreshModule?.Invoke("reminders");
            return "Reminder set for " + ModuleUtil.FormatDue(due);
        }

        internal void EditFromCompact(string noteId)
        {
            PendingEditId = noteId;
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

        private static string Shorten(string text, int max)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length <= max) return text;
            return text.Substring(0, max - 1) + "…";
        }
    }
}
