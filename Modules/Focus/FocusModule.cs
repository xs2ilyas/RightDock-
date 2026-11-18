using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using RightDock.Data;

namespace RightDock.Modules.Focus
{
    internal sealed class FocusModule : ProductivityModule
    {
        internal static readonly int[] Presets = { 15, 25, 45, 60 };

        private ModuleContext _context;
        private FocusStore _store;
        private TaskStore _tasks;
        private DispatcherTimer _timer;

        private string _sessionId;
        private int _plannedSeconds = 25 * 60;
        private int _elapsedSeconds;
        private DateTime _segmentStartUtc;
        private bool _running;
        private bool _paused;
        private string _taskId;
        private string _taskTitle;
        private int _presetMinutes = 25;

        private FocusStats _stats = new FocusStats();
        private List<FocusSessionItem> _history = new List<FocusSessionItem>();
        private FocusMiniWindow _mini;

        internal event Action Changed;

        public override string Id => "focus";

        public override string Name => "Focus";

        public override string Description => "Timed focus sessions with daily totals and session history.";

        public override string IconKey => "lucide:timer";

        internal bool IsRunning => _running;

        internal bool IsPaused => _paused;

        internal bool IsActive => _running || _paused;

        internal int PresetMinutes => _presetMinutes;

        internal string TaskTitle => _taskTitle;

        internal string SelectedTaskId => _taskId;

        internal FocusStats Stats => _stats;

        internal IReadOnlyList<FocusSessionItem> History => _history;

        internal int ElapsedSeconds
        {
            get
            {
                double seconds = _elapsedSeconds;
                if (_running) seconds += (DateTime.UtcNow - _segmentStartUtc).TotalSeconds;
                if (seconds < 0) seconds = 0;
                if (seconds > _plannedSeconds) seconds = _plannedSeconds;
                return (int)seconds;
            }
        }

        internal int RemainingSeconds => Math.Max(0, _plannedSeconds - ElapsedSeconds);

        internal double Progress => _plannedSeconds <= 0
            ? 0
            : Math.Min(1.0, (double)ElapsedSeconds / _plannedSeconds);

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _store = new FocusStore(context.Database);
            _tasks = new TaskStore(context.Database);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTick;
        }

        public override void Activate()
        {
            _store?.FinalizeStaleSessions();
            RefreshData();
        }

        public override void Deactivate()
        {
            CloseMiniTimer();
            _timer?.Stop();

            if (_sessionId != null)
            {
                int elapsed = ElapsedSeconds;
                if (elapsed > 0) _store?.Finish(_sessionId, DateTime.UtcNow, elapsed, false);
                _sessionId = null;
            }

            _running = false;
            _paused = false;
            _elapsedSeconds = 0;
            RefreshData();
        }

        public override FrameworkElement CreateCompactView()
        {
            return new FocusCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new FocusExpandedView(this);
        }

        internal void SetPreset(int minutes)
        {
            if (IsActive || Array.IndexOf(Presets, minutes) < 0) return;

            _presetMinutes = minutes;
            _plannedSeconds = minutes * 60;
            Changed?.Invoke();
        }

        internal void Start()
        {
            if (IsActive || _store == null) return;

            DateTime now = DateTime.UtcNow;
            _plannedSeconds = _presetMinutes * 60;
            _sessionId = _store.Start(now, _plannedSeconds, _taskId);
            _elapsedSeconds = 0;
            _segmentStartUtc = now;
            _running = true;
            _paused = false;

            _timer.Start();
            ShowMiniTimer();
            Changed?.Invoke();
        }

        internal void Pause()
        {
            if (!_running) return;

            _elapsedSeconds = ElapsedSeconds;
            _running = false;
            _paused = true;
            _timer.Stop();

            Changed?.Invoke();
        }

        internal void Resume()
        {
            if (!_paused) return;

            _segmentStartUtc = DateTime.UtcNow;
            _running = true;
            _paused = false;
            _timer.Start();

            Changed?.Invoke();
        }

        internal void Reset()
        {
            CloseMiniTimer();
            _timer?.Stop();

            if (_sessionId != null)
            {
                _store?.Discard(_sessionId);
                _sessionId = null;
            }

            _running = false;
            _paused = false;
            _elapsedSeconds = 0;

            Changed?.Invoke();
        }

        internal void SetTask(string taskId, string title)
        {
            if (IsActive) return;

            _taskId = string.IsNullOrWhiteSpace(taskId) ? null : taskId;
            _taskTitle = _taskId == null ? null : title;
            Changed?.Invoke();
        }

        internal List<TaskItem> GetActiveTasks()
        {
            return _tasks?.ActiveForFocus(8) ?? new List<TaskItem>();
        }

        internal void OpenDashboard()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }

        internal void RefreshData()
        {
            if (_store == null) return;

            _stats = _store.GetStats();
            _history = _store.Recent(20);
            Changed?.Invoke();
        }

        private void ShowMiniTimer()
        {
            AppSettings settings = _context?.Settings;
            if (settings == null) return;

            if (_mini == null)
            {
                FocusMiniWindow mini = new FocusMiniWindow(this, settings);
                mini.Closed += (_, __) =>
                {
                    if (ReferenceEquals(_mini, mini)) _mini = null;
                };
                _mini = mini;
            }

            _mini.Present();
        }

        private void CloseMiniTimer()
        {
            FocusMiniWindow mini = _mini;
            _mini = null;
            mini?.Close();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_running && ElapsedSeconds >= _plannedSeconds)
            {
                Complete();
                return;
            }

            Changed?.Invoke();
        }

        private void Complete()
        {
            CloseMiniTimer();
            _timer.Stop();

            string completedTask = _taskTitle;
            int minutes = _presetMinutes;

            _store?.Finish(_sessionId, DateTime.UtcNow, _plannedSeconds, true);
            _sessionId = null;
            _running = false;
            _paused = false;
            _elapsedSeconds = 0;

            RefreshData();

            string length = minutes == 1 ? "1 minute" : minutes + " minutes";
            string message = string.IsNullOrWhiteSpace(completedTask)
                ? "You finished " + length + " of focused work."
                : "You finished " + length + " on \"" + completedTask + "\".";

            _context?.Sounds?.PlayFocusComplete();
            _context?.Notifications?.Show("Focus session complete", message,
                () => _context.OpenExpanded?.Invoke(Id));
        }
    }
}
