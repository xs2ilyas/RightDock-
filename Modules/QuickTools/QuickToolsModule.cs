using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using RightDock.Data;
using RightDock.Services;

namespace RightDock.Modules.QuickTools
{
    internal sealed class BreathingPattern
    {
        internal BreathingPattern(string key, string name, string detail,
            int inhale, int holdIn, int exhale, int holdOut)
        {
            Key = key;
            Name = name;
            Detail = detail;
            PhaseSeconds = new[] { inhale, holdIn, exhale, holdOut };
        }

        internal string Key { get; }

        internal string Name { get; }

        internal string Detail { get; }

        internal int[] PhaseSeconds { get; }

        internal int CycleSeconds => PhaseSeconds.Sum();
    }

    internal sealed class QuickToolsConfig
    {
        public bool ScreenBreakEnabled { get; set; }

        public int ScreenBreakMinutes { get; set; } = 30;

        public string LastBreakDate { get; set; } = string.Empty;

        public int BreaksToday { get; set; }

        public string BreathPattern { get; set; } = "box";
    }

    internal sealed class QuickToolsModule : ProductivityModule
    {
        private const string ScreenBreakKey = "quicktools:screenbreak";

        private static readonly BreathingPattern[] PatternList =
        {
            new BreathingPattern("box", "Box", "4-4-4-4 · steady", 4, 4, 4, 4),
            new BreathingPattern("478", "4-7-8", "Relaxing · longer exhale", 4, 7, 8, 0),
            new BreathingPattern("calm", "Calm", "4 in · 6 out", 4, 0, 6, 0),
            new BreathingPattern("coherent", "Coherent", "5 in · 5 out", 5, 0, 5, 0),
        };

        private ModuleContext _context;
        private NoteStore _notes;
        private BreathingStore _breathing;
        private QuickToolsConfig _config = new QuickToolsConfig();
        private DispatcherTimer _timer;

        private BreathingPattern _pattern = PatternList[0];
        private DateTime _startedUtc;
        private int _elapsedSeconds;
        private int _cyclesCompleted;
        private int _phaseIndex;
        private int _phaseRemaining;
        private int _cycle;
        private int _totalCycles;
        private bool _active;
        private bool _paused;
        private string _breathStatus = string.Empty;

        private DateTime _nextBreakUtc;

        internal event Action Changed;

        internal event Action BreathingTick;

        public override string Id => "quicktools";

        public override string Name => "Quick Tools";

        public override string Description => "Three tools: quick note capture, guided breathing, and screen-break reminders.";

        public override string IconKey => "lucide:sparkles";

        internal IReadOnlyList<BreathingPattern> Patterns => PatternList;

        internal BreathingPattern SelectedPattern => _pattern;

        internal bool IsBreathing => _active;

        internal bool IsPaused => _paused;

        internal string BreathStatus => _breathStatus;

        internal int BreathCycle => _cycle;

        internal int BreathTotalCycles => _totalCycles;

        internal int BreathSecondsLeft => _phaseRemaining;

        internal int BreathPhaseSeconds =>
            _phaseIndex >= 0 && _phaseIndex < 4 ? _pattern.PhaseSeconds[_phaseIndex] : 0;

        internal int BreathElapsedSeconds => _elapsedSeconds;

        internal int BreathPhaseIndex => _phaseIndex;

        internal string BreathPhaseName
        {
            get
            {
                switch (_phaseIndex)
                {
                    case 0: return "Breathe in";
                    case 2: return "Breathe out";
                    default: return "Hold";
                }
            }
        }

        internal double BreathTargetScale => _phaseIndex == 0 || _phaseIndex == 1 ? 1.0 : 0.74;

        internal bool ScreenBreakEnabled => _config.ScreenBreakEnabled;

        internal int ScreenBreakMinutes => _config.ScreenBreakMinutes;

        internal int BreaksToday
        {
            get
            {
                if (_config.LastBreakDate != DateTime.Today.ToString("yyyy-MM-dd"))
                {
                    _config.LastBreakDate = DateTime.Today.ToString("yyyy-MM-dd");
                    _config.BreaksToday = 0;
                }

                return _config.BreaksToday;
            }
        }

        internal string NextBreakText
        {
            get
            {
                if (!_config.ScreenBreakEnabled) return "Off";
                if (_nextBreakUtc <= DateTime.UtcNow) return "Due now";

                DateTime local = _nextBreakUtc.ToLocalTime();
                TimeSpan left = _nextBreakUtc - DateTime.UtcNow;
                if (left.TotalMinutes < 1) return "In less than a minute";
                return "Next " + local.ToString("HH:mm");
            }
        }

        public override void Initialize(ModuleContext context)
        {
            _context = context;
            _notes = new NoteStore(context.Database);
            _breathing = new BreathingStore(context.Database);

            LoadConfig();

            _timer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            _timer.Tick += OnBreathTick;
        }

        public override FrameworkElement CreateCompactView()
        {
            return new QuickToolsCompactView(this);
        }

        public override FrameworkElement CreateExpandedView()
        {
            return new QuickToolsExpandedView(this);
        }

        public override void Activate()
        {
            ScheduleScreenBreak();
            Changed?.Invoke();
        }

        public override void Deactivate()
        {
            _context?.Scheduler?.Cancel(ScreenBreakKey);
            StopBreathing(true);
        }

        internal string CaptureNote(string body)
        {
            if (string.IsNullOrWhiteSpace(body) || _notes == null) return null;

            string id = _notes.Add(new NoteItem
            {
                Body = body.Trim(),
                Category = "Quick",
            });

            if (id != null)
            {
                _context?.RefreshModule?.Invoke("notes");
                Changed?.Invoke();
            }

            return id;
        }

        internal List<NoteItem> RecentNotes(int limit)
        {
            return _notes?.Query(null)?.Take(Math.Max(1, limit)).ToList() ?? new List<NoteItem>();
        }

        internal int NoteCount()
        {
            return _notes?.Query(null)?.Count ?? 0;
        }

        internal void OpenNotes()
        {
            _context?.OpenExpanded?.Invoke("notes");
        }

        internal void OpenExpandedView()
        {
            _context?.OpenExpanded?.Invoke(Id);
        }

        internal void SelectPattern(string key)
        {
            BreathingPattern pattern = PatternList.FirstOrDefault(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            if (pattern == null || _active) return;

            _pattern = pattern;
            _config.BreathPattern = pattern.Key;
            SaveConfig();
            Changed?.Invoke();
        }

        internal void StartBreathing(int cycles)
        {
            if (_active) StopBreathing(true);

            _totalCycles = Math.Max(1, cycles);
            _cycle = 1;
            _cyclesCompleted = 0;
            _elapsedSeconds = 0;
            _startedUtc = DateTime.UtcNow;
            _phaseIndex = FirstPhaseIndex();
            _phaseRemaining = _pattern.PhaseSeconds[_phaseIndex];
            _active = true;
            _paused = false;
            _breathStatus = string.Empty;

            _timer?.Start();
            BreathingTick?.Invoke();
            Changed?.Invoke();
        }

        internal void TogglePauseBreathing()
        {
            if (!_active) return;

            _paused = !_paused;
            BreathingTick?.Invoke();
            Changed?.Invoke();
        }

        internal void StopBreathing(bool save)
        {
            bool wasActive = _active;
            int cycles = _cyclesCompleted;
            int seconds = _elapsedSeconds;
            DateTime started = _startedUtc;
            BreathingPattern pattern = _pattern;

            _timer?.Stop();
            _active = false;
            _paused = false;
            _phaseIndex = 0;
            _phaseRemaining = 0;
            _cycle = 0;
            _elapsedSeconds = 0;
            _cyclesCompleted = 0;

            if (wasActive && save && cycles > 0 && seconds > 0)
            {
                _breathing?.Add(pattern.Key, pattern.Name, cycles, seconds, started, DateTime.UtcNow);
                _breathStatus = "Session saved · " + cycles + (cycles == 1 ? " cycle · " : " cycles · ")
                    + ModuleUtil.FormatDuration(seconds);
            }
            else if (wasActive)
            {
                _breathStatus = "Session ended";
            }

            BreathingTick?.Invoke();
            Changed?.Invoke();
        }

        internal BreathingStats BreathingStatistics()
        {
            return _breathing?.GetStats() ?? new BreathingStats();
        }

        internal List<BreathingSessionItem> RecentBreathingSessions(int limit)
        {
            return _breathing?.Recent(limit) ?? new List<BreathingSessionItem>();
        }

        internal void SetScreenBreakEnabled(bool enabled)
        {
            _config.ScreenBreakEnabled = enabled;
            SaveConfig();
            ScheduleScreenBreak();
            Changed?.Invoke();
        }

        internal void SetScreenBreakMinutes(int minutes)
        {
            _config.ScreenBreakMinutes = Math.Max(1, Math.Min(240, minutes));
            SaveConfig();
            ScheduleScreenBreak();
            Changed?.Invoke();
        }

        internal void ResetScreenBreakTimer()
        {
            ScheduleScreenBreak();
            Changed?.Invoke();
        }

        internal void FireBreakNow()
        {
            ShowBreakNotification();
            RecordBreak();
            Changed?.Invoke();
        }

        private void OnBreathTick(object sender, EventArgs e)
        {
            if (!_active || _paused) return;

            _elapsedSeconds++;

            if (_phaseRemaining > 1)
            {
                _phaseRemaining--;
                BreathingTick?.Invoke();
                return;
            }

            AdvancePhase();
            BreathingTick?.Invoke();
        }

        private void AdvancePhase()
        {
            int next = _phaseIndex + 1;
            while (next < 4 && _pattern.PhaseSeconds[next] <= 0) next++;

            if (next >= 4)
            {
                _cyclesCompleted++;

                if (_cycle >= _totalCycles)
                {
                    CompleteBreathing();
                    return;
                }

                _cycle++;
                next = FirstPhaseIndex();
            }

            _phaseIndex = next;
            _phaseRemaining = _pattern.PhaseSeconds[next];
        }

        private void CompleteBreathing()
        {
            int cycles = _cyclesCompleted;
            int seconds = _elapsedSeconds;
            DateTime started = _startedUtc;
            BreathingPattern pattern = _pattern;

            _timer?.Stop();
            _active = false;
            _paused = false;
            _phaseIndex = 0;
            _phaseRemaining = 0;
            _cycle = 0;
            _elapsedSeconds = 0;
            _cyclesCompleted = 0;

            _breathing?.Add(pattern.Key, pattern.Name, cycles, seconds, started, DateTime.UtcNow);
            _breathStatus = "Complete · " + cycles + (cycles == 1 ? " cycle · " : " cycles · ")
                + ModuleUtil.FormatDuration(seconds);

            Changed?.Invoke();
        }

        private int FirstPhaseIndex()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_pattern.PhaseSeconds[i] > 0) return i;
            }

            return 0;
        }

        private void ScheduleScreenBreak()
        {
            Scheduler scheduler = _context?.Scheduler;
            if (scheduler == null) return;

            scheduler.Cancel(ScreenBreakKey);

            if (!_config.ScreenBreakEnabled) return;

            TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, _config.ScreenBreakMinutes));
            _nextBreakUtc = DateTime.UtcNow.Add(interval);

            scheduler.Schedule(ScreenBreakKey, _nextBreakUtc, () =>
            {
                _nextBreakUtc = DateTime.UtcNow.Add(interval);
                ShowBreakNotification();
                RecordBreak();
                Changed?.Invoke();
            }, interval);
        }

        private void ShowBreakNotification()
        {
            _context?.Notifications?.Show(
                "Screen break",
                "Look 20 feet away for 20 seconds, then stand up and stretch.",
                () => _context?.OpenExpanded?.Invoke(Id));
        }

        private void RecordBreak()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            if (_config.LastBreakDate != today)
            {
                _config.LastBreakDate = today;
                _config.BreaksToday = 0;
            }

            _config.BreaksToday++;
            SaveConfig();
        }

        private void LoadConfig()
        {
            try
            {
                string json = _context?.LoadModuleConfig?.Invoke(Id);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    QuickToolsConfig config = JsonSerializer.Deserialize<QuickToolsConfig>(json);
                    if (config != null) _config = config;
                }
            }
            catch (Exception ex)
            {
                _context?.LogError("Could not load Quick Tools settings; defaults will be used.", ex);
            }

            _config.ScreenBreakMinutes = Math.Max(1, Math.Min(240, _config.ScreenBreakMinutes));

            _pattern = PatternList.FirstOrDefault(item =>
                string.Equals(item.Key, _config.BreathPattern, StringComparison.OrdinalIgnoreCase))
                ?? PatternList[0];
        }

        private void SaveConfig()
        {
            try
            {
                _context?.SaveModuleConfig?.Invoke(Id, JsonSerializer.Serialize(_config));
            }
            catch (Exception ex)
            {
                _context?.LogError("Could not save Quick Tools settings.", ex);
            }
        }
    }
}
