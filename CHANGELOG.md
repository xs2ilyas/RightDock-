# Changelog

All notable changes to RightDock are recorded here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions use
semantic versioning. Development is organized in phases (see
`IMPLEMENTATION_PROGRESS.md`).

## [Unreleased]

### Planned
- Post-1.11 maintenance: drag reordering, per-monitor dock placement, per-module settings
  page, signed installer and scheduled backups.

Earlier releases (1.0.0–1.2.0: dock shell, pins, auto-hide, 181-icon line set, icon
picker, settings popup) are described in `IMPLEMENTATION_PROGRESS.md`.

---

## [1.12.0] — 2026-11-18 — Module icons, colors and sizes

### Added
- **Dock icon size** — the Appearance tab has an **ICON SIZE** picker with four steps
  (Small 20, Medium 26, Large 30, Extra large 34 px). It applies to module icons and
  pinned-app icons instantly; the Add, Settings and Close buttons keep their fixed
  38×38 size.
- **Per-module appearance** — every row in Settings → Modules has a **Customize icon…**
  link (also offered as **Customize icon…** in the module's dock context menu). The
  panel provides:
  - the 181-glyph icon picker (without the "this app" row) and a **Default icon** reset;
  - the ten preset colors plus the native Windows color dialog, with **Default color**;
  - a size override (Default / 20 / 26 / 30 / 34 px) on top of the global size;
  - **Reset module icon** to clear all three overrides at once.
- Changes rebuild the dock immediately and persist in `ModuleSettings` (`IconKey`,
  `IconColor`, `IconSize`); schema version 4 adds the three columns.

### Changed
- `IconPickerWindow` gained an `allowAppIcon` flag so per-module pickers omit the pinned
  app's own icon while the pin picker keeps it.
- Settings opening is centralized in `MainWindow.ShowSettings(moduleId)`; the existing
  Settings button passes no module and the context menu passes the module id.

### Verified
- Live Windows 11 session: switching the global size 26 → 30 enlarged module and pin
  icons while the bottom buttons stayed put; Dashboard was given a star glyph, red
  `#EF6B6B` and 34 px and the dock updated immediately; the row survived a restart
  (`appearance row dashboard / lucide:star / #EF6B6B / 34`, `PRAGMA user_version` 4);
  **Default icon** and **Reset module icon** restored the defaults; the open-from-menu
  path landed on Modules with the row expanded, and the pin icon picker still showed
  "Use MedHistory's own icon".

---

## [1.11.1] — 2026-11-18 — Fullscreen recovery fix

### Fixed
- **Dock no longer stays lost after a fullscreen video or browser.** The shell does not
  always send the `ABN_FULLSCREENAPP` "ended" notification when a player leaves
  fullscreen, which left the dock suppressed until a restart. The 110 ms hover tick now
  re-checks the foreground window and releases stale suppression about 0.7 s after no
  fullscreen app owns the dock's monitor.
- **Fullscreen is confirmed against the real foreground window** (`IsFullscreenOnDockMonitor`):
  a borderless window (no `WS_CAPTION`), covering the whole monitor that hosts the dock,
  excluding the shell desktop/taskbar classes and the dock's own process. Maximized
  windows keep their caption and are ignored, so an ordinary maximize never hides it.
- **Tray Open while a fullscreen app is running** is ignored instead of showing the dock
  under the video only to hide it again; a leftover suppression flag is cleared when the
  dock is restored normally.
- **Tray-restored dock with auto-hide enabled** stays visible until the pointer actually
  reaches it, instead of auto-hiding before the user can click a module.

### Added
- `tools/simulate-fullscreen.ps1` posts the AppBar `ABN_FULLSCREENAPP` callback to the
  running dock (`-State on|off`), making fullscreen handling testable deterministically.

### Changed
- Version 1.11.1.

### Verified
- With no fullscreen app, a simulated `ABN_FULLSCREENAPP` on hid the dock and the hover
  tick released the suppression ~0.7 s later ("Fullscreen app ended; releasing dock
  suppression." in the log); the work area returned to 2498 px without user action.
- A real borderless 2560×1600 window kept the dock hidden (work area 2560 px) for the
  whole session; closing the window returned the dock automatically each time.
- A real Edge F11 fullscreen over a local page hid the dock, and leaving fullscreen
  brought it back within ~3 s with no restart — the reported YouTube-style scenario.
- With auto-hide temporarily enabled, **Open Right Dock** from the tray kept the dock
  visible while the pointer stayed away; hovering the dock and then moving away hid it
  after the 650 ms delay. The user's settings file was restored afterwards.
- Notes popup open/close smoke test passed; only the publish build was used.
- Published 1.11.1 reports FileVersion 1.11.1.0; `dist\RightDock-1.11.1-win-x64.zip`
  holds 37 entries. Settled idle baseline: 117.0 MB working set, 63.4 MB private,
  0.016 CPU seconds over 10 s, 436 handles, 10 threads.

### Files
- Added: `tools/simulate-fullscreen.ps1`.
- Changed: `MainWindow.xaml.cs`, `NativeMethods.cs`, `RightDock.csproj`,
  `installer/RightDock.iss`, `README.md`, `PROJECT_ARCHITECTURE.md`,
  `IMPLEMENTATION_PROGRESS.md`.

---

## [1.11.0] — 2026-11-18 — Phase 9 system tray icon

### Added
- **Tray icon** (`TrayIcon.cs`) — the Right Dock application icon in the notification
  area, with the tooltip "Right Dock". Left-clicking brings the dock back from the tray.
- **Tray menu** — a dark four-item menu: **Open Right Dock**, **Minimize to tray**,
  **Restart Right Dock** and **Exit Right Dock**. Open is disabled while the dock is
  visible; Minimize is disabled while it is already in the tray or a modal dialog is open.
- **Minimize to tray** — hides the dock window, closes open module popups, unregisters
  the AppBar (so maximized windows regain the full width) and suspends edge reveal until
  the dock is restored.
- **Restart and exit** — Restart relaunches the executable with `--restart`, using the
  same single-instance handoff as restore; Exit shuts the application down cleanly and
  removes the icon.

### Changed
- `tools/ui.ps1` gained `rightclickxy` and no longer requires a window name for
  `clickglobal`/`invokeglobal`, which made tray-menu testing possible.
- Version 1.11.0.

### Verified
- The published build showed the app icon in the notification overflow with the
  "Right Dock" tooltip.
- Right-clicking the icon opened the dark menu with the expected labels; **Open** was
  disabled while the dock was visible and **Minimize** enabled.
- **Minimize to tray** removed the "Right Dock" window from the visible-window list and
  the work area returned from 2498 to 2560 px; hovering the right screen edge for two
  seconds did not reveal the dock. A left-click on the tray icon restored it and the
  work area returned to 2498 px.
- **Restart Right Dock** ended the old process and started a new one (single instance
  retained), with the dock visible and the work area reserved.
- **Exit Right Dock** terminated the process, removed the tray icon and restored the
  full work area. Closing the dock normally while the icon was active also exited.
- Published 1.11.0 reports FileVersion 1.11.0.0; `dist\RightDock-1.11.0-win-x64.zip`
  holds 37 entries. Settled idle baseline: 119.8 MB working set, 69.9 MB private,
  0.016 CPU seconds over 10 s, 409 handles, 13 threads.
- No test data was written; only window and work-area state changed.

### Files
- Added: `TrayIcon.cs`.
- Changed: `App.xaml.cs`, `MainWindow.xaml.cs`, `RightDock.csproj`,
  `installer/RightDock.iss`, `tools/ui.ps1`, `README.md`, `PROJECT_ARCHITECTURE.md`,
  `IMPLEMENTATION_PROGRESS.md`.

---

## [1.10.0] — 2026-11-18 — Phase 8 focus mini timer

### Added
- **Focus mini timer** (`Modules/Focus/FocusMiniWindow.xaml`) — a 176×64 DIP dark pill
  that appears automatically when a focus session starts. It shows the remaining time in
  tabular mm:ss digits, a pin button and a close button, and can be dragged anywhere on
  the desktop.
- **Pin toggle** — the pin button switches the pill between always-on-top and normal
  z-order; the active state uses an accent glyph on a tinted background. The choice is
  saved as `MiniTimerPinned` (default `true`).
- **Placement persistence** — the pill's position is saved as `MiniTimerLeft`/`MiniTimerTop`
  in `settings.json` after a drag and restored on the next start, clamped to the virtual
  screen. Without a saved position it opens at the bottom-right corner of the work area,
  left of the dock.
- **State feedback** — the time turns amber while the session is paused and white while
  it runs; the close button hides the pill for the current session (the focus session
  continues), and starting the next session shows it again. Reset, completion and module
  deactivation hide it.

### Changed
- Starting a focus session shows the pill without activating it, so the compact popup
  keeps focus.
- `ShutdownMode` is now `OnMainWindowClose`; closing the dock exits even when the timer
  pill or another auxiliary window is still open.
- Version 1.10.0.

### Verified
- Starting a 25-minute session from the Focus popup created the "Focus timer" window
  (176×64 DIP) at the default bottom-right position, with `24:59` ticking down while the
  compact view showed `FOCUSING`.
- Dragging from the time area moved the pill and wrote `MiniTimerLeft: 1780` /
  `MiniTimerTop: 1016.8` to `settings.json`; the next start reopened at that position.
- The pin button flipped `MiniTimerPinned` to `false` and `WS_EX_TOPMOST` disappeared from
  the window; the glyph turned gray and the tint cleared. After a restart-style relaunch
  the saved pin state was applied.
- Pausing changed the digits to amber; resuming returned them to white. Closing the pill
  hid it while the session stayed paused in the compact view, and Reset hid it for good;
  the following Start recreated it.
- Closing the dock with the pill open exited the process (previously it would have
  lingered); no RightDock process remained.
- Test focus sessions were removed from the live database afterwards and the saved
  position/pin preferences were reset to defaults.

### Files
- Added: `Modules/Focus/FocusMiniWindow.xaml`, `Modules/Focus/FocusMiniWindow.xaml.cs`.
- Changed: `App.xaml`, `AppSettings.cs`, `Modules/Focus/FocusModule.cs`,
  `RightDock.csproj`, `installer/RightDock.iss`, `README.md`, `PROJECT_ARCHITECTURE.md`,
  `IMPLEMENTATION_PROGRESS.md`.

---

## [1.9.0] — 2026-11-16 — Phase 7 bell sounds

### Added
- **Bell sounds** (`Services/BellSynth.cs`, `Services/SoundService.cs`) — ten synthesized
  timbres (Classic Bell, Soft Chime, Glass Bell, Marimba, Temple Bowl, Digital Chime, Harp
  Pluck, Music Box, Deep Resonant, Bright Bell) rendered in memory as 44.1 kHz PCM16 mono
  WAV, so no audio files, codecs or downloads are involved. `SoundPlayer` instances are
  cached per bell.
- **Distinct alert patterns** — finishing a focus session rings three times, 850 ms
  apart; a due reminder rings once. A new alert cancels the remainder of an in-flight
  sequence.
- **Sound pickers** — Settings → Notifications gains a SOUNDS section with a ten-item
  picker and Preview button for focus completion and for reminders, plus a status line.
  Choices save immediately (`FocusSound`/`ReminderSound` in `settings.json`), are
  validated against the catalog on load, and default to `classic`/`soft`.
- **Notification gating** — live bells respect the notifications master toggle; previews
  always play so the pickers stay testable while notifications are off.
- **Tooling** — `tools/ui.ps1` adds `expand`, `collapse`, `select` and `selected` actions
  and lists ComboBox controls, enabling automated verification of the pickers.

### Changed
- `DockServices` owns a `SoundService` and passes it through `ModuleManager`/`ModuleContext`
  to every module; it is disposed after the modules shut down.
- Focus completion and reminder firing call the sound service immediately before their
  notification card.
- Version 1.9.0.

### Verified
- All ten bells rendered offline: 0.85–2.6 s WAVs, 75–229 KB each, normalized to the same
  peak with distinct RMS values (no duplicates).
- Picked Glass Bell for focus and Music Box for reminders through the Settings combos;
  `settings.json` recorded `"FocusSound": "glass"` and `"ReminderSound": "musicbox"`, and
  the log showed `Bells: focus preview using "glass" x3.` and
  `Bells: reminder preview using "musicbox" x1.`
- With notifications off, Preview still played and the status line added "Notifications are
  off, so live alerts stay silent."
- A real reminder due at 14:39 produced one ring (`Bells: reminder using "musicbox" x1.`)
  and its card; with notifications off, a second reminder produced no bell log entry.
- A 15-minute focus session started through the Focus popup and rang three times at
  completion (`Bells: focus session complete using "glass" x3.`), with the
  "Focus session complete" card captured at the same moment.
- No errors in `rightdock.log`; selections survived an app restart (Glass Bell / Music Box
  were still selected after the process restarted).
- Release build: `publish\RightDock.exe` reports FileVersion 1.9.0.0 / ProductVersion
  1.9.0; `dist\RightDock-1.9.0-win-x64.zip` contains 37 entries including the
  `e_sqlite3.dll` natives. Fresh published process idles at 109.0 MB working set, 62.2 MB
  private, 0.016 CPU seconds over 10 s, 463 handles and 17 threads.

### Files
- Added: `Services/BellSynth.cs`, `Services/SoundService.cs`.
- Changed: `AppSettings.cs`, `DockServices.cs`, `Modules/ModuleContext.cs`,
  `Modules/ModuleManager.cs`, `Modules/Focus/FocusModule.cs`,
  `Modules/Reminders/RemindersModule.cs`, `RightDock.csproj`, `SettingsWindow.xaml`,
  `SettingsWindow.xaml.cs`, `tools/ui.ps1`.

---

## [1.8.0] — 2026-11-16 — Phase 6 hardening

### Added
- **Data & Backup settings tab** (`SettingsWindow`, sixth tab) — database path, live
  `SQLite · schema v3 · <size> · <rows>` summary, **Open data folder**, **Run integrity
  check**, **Create backup…**, **Restore backup…**, **Copy diagnostics** and an About row
  (`RightDock 1.8.0 · .NET 5 · schema v3`). Status lines report success in green and
  failures in red.
- **Backup service** (`Data/DataBackupService.cs`) — one ZIP with a consistent
  `SqliteConnection.BackupDatabase` copy of `rightdock.db`, `settings.json`, `pins.json`,
  optional `icons/` and `backup.json` (version, UTC time, schema version, per-table row
  counts). Creation validates integrity and schema before compressing.
- **Staged restore** — the archive is inspected and extracted with a path-traversal
  guard after user confirmation; `restore.pending` is written and the app restarts with
  `--restart`. At the next start the staged database is validated and applied before
  SQLite opens; the current database, settings and pins are first copied to
  `backups\pre-restore-yyyyMMdd-HHmmss\`, so a failure rolls back. A "Backup restored"
  toast reports the archive summary.
- **Database diagnostics** — `Database.SchemaVersion`, `SizeBytes`,
  `IntegrityCheck()`, whitelisted `CountRows(table)` and `BackupTo(path)`; backup copies
  are switched from WAL to DELETE journal mode so archives contain no stale
  `-wal`/`-shm` files.
- **OS-event hardening** — `WM_POWERBROADCAST` resume and `WM_TIMECHANGE` re-run
  `Scheduler.Reconcile()`; resume and `WM_DISPLAYCHANGE` re-read the monitor and
  re-reserve the AppBar.
- **Release assets** — embedded multi-size `Resources/RightDock.ico` (16–256 px PNG
  entries), FileVersion/ProductVersion 1.8.0, CompanyName and product description,
  `installer/RightDock.iss` (Inno Setup 6, per-user x64, AppMutex), `tools/publish.ps1`
  (portable ZIP and optional installer), `tools/measure.ps1`, `tools/stress-ui.ps1`,
  `tools/make-icon.ps1` and `THIRD-PARTY-NOTICES.md`.
- **Restart-aware single instance** — `--restart` waits up to 10 s for the previous
  process to release `Local\RightDock.SingleInstance`, so restore never opens the
  database twice. A normal second launch still exits immediately.

### Changed
- Settings window is 560×560 with six tabs (Data inserted before Startup).
- `DockServices` owns a `DataBackupService`; `App.OnStartup` applies any pending restore
  before creating services and shows the result toast.
- `.gitignore` excludes the generated `dist/` folder.

### Verified
- Data page showed the real database path, `SQLite · schema v3 · 260.1 KB · 44 rows` and
  `Integrity check passed · schema v3 · 260.1 KB · 44 rows`.
- Backup ZIP contained `backup.json`, `pins.json`, `settings.json`, `rightdock.db` and no
  WAL/SHM files; the embedded database returned `journal_mode=delete`, `user_version=3`,
  `integrity_check=ok` and 44 rows.
- Restore of that archive after adding a probe note (1 → 2 notes) restarted the process
  (PID 12380 → 24800), returned the notes to 1, kept a valid two-note safety copy in
  `backups\pre-restore-20261116-141505\` and logged the restored summary.
- A v1 database fixture migrated to v3 on startup (`Applied database migration v2`/`v3`)
  with `BreathingSessions`, `SnoozeUntilUtc` and all rows intact.
- Posted resume, clock-change and display-change messages produced the matching log lines
  ("System resumed…", "System clock changed…") and left the reserved rect at
  `2498,0 62×1540`.
- Idle profiling: 150.4 MB working set, 98.9 MB private, 0.016 CPU seconds over 10 s,
  476 handles, 9 threads. A 27-cycle UI stress run (~650 popup toggles) plateaued at
  ≈111 MB private / ≈163 MB working set with flat handles (483–492), GDI (38) and USER
  (31) — no leak.
- All twelve modules still open after migration, restore and stress; no errors in the log;
  `publish\RightDock.exe` carries version 1.8.0, the icon and the RightDock company name;
  `dist\RightDock-1.8.0-win-x64.zip` contains 37 files. Inno Setup 6 was not installed on
  the test machine, so the setup executable was not compiled.

### Files
- Added: `Data/DataBackupService.cs`, `Resources/RightDock.ico`,
  `installer/RightDock.iss`, `THIRD-PARTY-NOTICES.md`, `tools/make-icon.ps1`,
  `tools/measure.ps1`, `tools/publish.ps1`, `tools/stress-ui.ps1`.
- Changed: `.gitignore`, `App.xaml.cs`, `Data/Database.cs`, `DockServices.cs`,
  `MainWindow.xaml.cs`, `NativeMethods.cs`, `RightDock.csproj`, `SettingsWindow.xaml`,
  `SettingsWindow.xaml.cs`.

---

## [1.7.0] — 2026-11-16 — Phase 5 quick tools and learning

### Added
- **Quick Tools module** (`Modules/QuickTools/`, `lucide:sparkles`) — exactly three
  tools: quick note capture, guided breathing and screen-break reminders.
  - Quick note writes through `NoteStore` with category `Quick` and refreshes the Notes
    module; compact capture supports Ctrl+Enter.
  - Breathing supports Box (4-4-4-4), 4-7-8, Calm and Coherent patterns with 3/5/8
    cycles, pause/resume/stop, phase countdown, an animated circle in the expanded view
    and persisted sessions in the new `BreathingSessions` table (pattern, cycles,
    duration, UTC start/end).
  - Screen breaks persist `ScreenBreakEnabled`, `ScreenBreakMinutes` (1–240),
    `LastBreakDate`, `BreaksToday` and `BreathPattern` in `ModuleSettings.ConfigJson`;
    enabling registers the `quicktools:screenbreak` interval task, and firing shows the
    "Screen break" in-app toast with 20-20-20 guidance. "Remind now" and reset are
    available; disabling cancels the task.
- **Learning module** (`Modules/Learning/`, `lucide:graduation-cap`) — goals with
  Course/Reading/Skill/Other categories, units (minutes/hours/pages/chapters/lessons/
  sessions), target and progress values, optional due date and description. Search,
  status filters (All/Active/Completed) and category chips; complete/reopen and armed
  delete. Session logging stores minutes/notes in `LearningSessions` and auto-adds
  minutes (or converted hours) to minute/hour goals. Compact shows active goals and this
  week's minutes; expanded adds the goal editor, session form and recent sessions.
- **Schema version 3** — `BreathingSessions` table with an index on `StartedUtc`, kept
  separate from focus data. Learning reuses the existing `LearningGoals` and
  `LearningSessions` tables; deleting a goal keeps sessions via `ON DELETE SET NULL`.
- `ModuleContext.LoadModuleConfig` / `SaveModuleConfig` so modules can persist their own
  JSON config through `ModuleStateStore`.
- `BreathingStore` and `LearningStore` data-access classes.
- `ModuleUtil.FormatDuration` for sub-minute durations ("19 s", "1 min").
- Automation ids for the new surfaces (`QuickNoteCapture`, `QuickNoteSaveButton`,
  `BreatheStartButton`, `BreathePauseButton`, `BreatheStopButton`, `ScreenBreakToggle`,
  `ScreenBreakRemindButton`, `LearningSearch`, `GoalTitle`, `GoalSaveButton`,
  `SessionLogButton`, …).

### Changed
- Quick Tools and Learning registered after Statistics, so the dock now shows twelve
  modules: Dashboard, Focus, Tasks, Reminders, Notes, Habits, Medication,
  Health & Lifestyle, Calendar, Statistics, Quick Tools, Learning.
- Version 1.7.0.

### Verified
- All twelve dock buttons appear after restart; Quick Tools and Learning open compact and
  expanded; earlier modules and pins unchanged.
- Quick note saved "Phase 5 quick note" and appeared in Notes under category Quick
  (count 1 → 2); input cleared with a status message.
- Breathing start/pause/resume/stop saved 1 cycle/19 s; a full 5-cycle Box session
  completed at 80 s with "Complete · 5 cycles · 1 min"; session rows were present; the
  expanded circle animated.
- Screen break enabled at 15 min showed the next time; Remind now produced the toast,
  incremented breaks today and persisted the config; restart restored On/15 min/1 break/
  Box/2 notes; a 1-minute test interval fired automatically; settings were reset to
  off/30 min.
- Learning: created "Spanish Course" (hours, target 10, due 2026-12-31), logged 30 min →
  "0.5 / 10 hours" plus a session; complete/reopen worked; created "Read Atomic Habits"
  (pages, 240, progress 50); search/category filters worked; target edit kept progress;
  armed delete removed the goal but retained its session; restart showed one active goal
  and 30 min this week.
- Regression: Dashboard and earlier modules unchanged; single process; no errors logged.
- Idle after Phase 5: 126.8 MB working set, 71.5 MB private, 0.047 CPU seconds over 30 s.

### Files
- Added: `Data/BreathingStore.cs`, `Data/LearningStore.cs`,
  `Modules/QuickTools/QuickToolsModule.cs`,
  `Modules/QuickTools/QuickToolsCompactView.xaml(.cs)`,
  `Modules/QuickTools/QuickToolsExpandedView.xaml(.cs)`,
  `Modules/Learning/LearningModule.cs`,
  `Modules/Learning/LearningCompactView.xaml(.cs)`,
  `Modules/Learning/LearningExpandedView.xaml(.cs)`.
- Changed: `App.xaml.cs`, `Data/Database.cs`, `Modules/ModuleContext.cs`,
  `Modules/ModuleManager.cs`, `Modules/ModuleUtil.cs`, `RightDock.csproj`.

---

## [1.6.0] — 2026-11-16 — Phase 4 planning and insight

### Added
- **Dashboard module** (`Modules/Dashboard/`) — registered first so it heads the dock.
  `DashboardModule` builds a `DashboardSnapshot` from all existing stores (focus today
  plus 7-day totals, task due/overdue/completed counts, habit done/total and best streak,
  medication taken/planned and next actionable dose, active/overdue reminders, note
  counts, health today values and latest weight). The compact "Daily Overview" shows
  focus, tasks, habits, medication, next up and a health snapshot; the expanded view adds
  "Today at a glance", four quick actions and a 2×3 widget grid (Focus bars, Tasks,
  Habits, Medication, Health, Reminders & Notes), each navigating to its owning module.
  Refresh timers run only while a view is loaded (60 s compact, 30 s expanded).
- **Calendar planner module** (`Modules/Calendar/`) — a Monday-start 6-week month grid
  aggregating tasks by due date, reminders by effective due, planned medication
  occurrences from schedules and ended focus sessions. Day cells show entry counts, two
  titles and "+N more"; the selected-day agenda shows times, colored kind stripes,
  done/overdue styling and a legend. Quick add accepts a title and `h:mm` time (default
  09:00) and creates a task or reminder through the owning store, then refreshes it.
  Compact view lists today's agenda and the next seven days.
- **Statistics module** (`Modules/Statistics/`) — daily (7 days), weekly (8 Monday weeks)
  and monthly (6 months) buckets of focus minutes, completed tasks, habit check-in rate
  and medication adherence, with range labels and health averages. Shared `StatsChart`
  renders uniform bars with value labels and tooltips (rate metrics on a fixed 0–100 %
  scale); the expanded view adds summary cards, metric chips, a caption and a scrollable
  breakdown table. Compact view shows four totals and a mini chart.
- **Notes manager** — category filter chips built from stored categories (new notes
  prefill the active category), an Archived toggle with counts and empty state, and
  soft-hide archive/restore from the editor (`Notes.IsArchived`, no schema change).
- Shared reporting queries: `TaskStore.Counts/DueBetween/CompletedBetween`,
  `FocusStore.Between`, `ReminderStore.Between`, `MedicationStore.Between/Occurrences`,
  `HabitStore.LogsBetween/CountActive`, `HealthStore.Averages`, and
  `NoteStore.Categories/CountArchived/SetArchived`.
- Automation ids for the new surfaces (`DashboardOpenButton`, `CalendarOpenButton`,
  `CalendarAddButton`, `CalendarQuickTitle`, `CalendarDayYYYYMMDD`, `StatsOpenButton`,
  `StatsRefreshButton`, `StatsChartPanel`, `StatsTablePanel`, `NoteArchivedChip`, …).

### Changed
- Three new modules registered in `App.OnStartup` (Dashboard first, Calendar and
  Statistics after Health); the dock, Settings → Modules and module state store now cover
  ten modules. Existing saved states keep their order.
- Statistics expanded layout was tightened (150 px chart, 132 px breakdown viewport) so
  the 980×680 window fits without a page scrollbar; the breakdown header aligns with the
  scrolling rows.
- Notes list count and dashboard note values singularise ("1 note") and health values
  read naturally ("2 glasses of 8 glasses").
- Version 1.6.0.

### Verified
- All ten dock buttons appear in order after restart; Dashboard, Calendar and Statistics
  open compact and expanded; existing modules and pins unchanged.
- Dashboard showed "15 min / 2 sessions", "0 due", "2 of 10", "1 of 2", next reminder
  "Drink water · Tomorrow 11:44", health 2/8 glasses, 5/30 min, 0.5/8 h and 72.5 kg, plus
  a 7-day focus chart with today's bar; after quick adds from the planner it updated to
  "1 due", "3 active" and "1 note".
- Calendar rendered 70 entries for November 2026 with per-day counts; today's agenda
  listed 8 items with done/overdue styling; quick add created a task at 09:00 (visible in
  Tasks) and a reminder at 15:30 (visible in the agenda and counted by the dashboard).
- Statistics: Daily 15 min / 1 task / 6 % habits / 50 % medication; Weekly "Last 8 weeks"
  with 1 % habit rate; Monthly "Last 6 months" (Jun–Nov); metric chips re-rendered chart
  and caption; breakdown rows matched the summary.
- Notes: category filter, archive ("1 archived note", "1 note in archive", Restore),
  restore ("Note restored") and pin/unpin all worked; the archived chip renders as a
  compact chip.
- Regression: Tasks, Health and all Phase 2/3 popups still open with data; single
  process; no errors logged.
- Idle baseline after opening every Phase 4 view once: 145.7 MB working set, 94.3 MB
  private, 0.078 CPU seconds over 30 s.

### Files
- Added: `Modules/Dashboard/DashboardModule.cs`,
  `Modules/Dashboard/DashboardCompactView.xaml(.cs)`,
  `Modules/Dashboard/DashboardExpandedView.xaml(.cs)`,
  `Modules/Calendar/CalendarModule.cs`,
  `Modules/Calendar/CalendarCompactView.xaml(.cs)`,
  `Modules/Calendar/CalendarExpandedView.xaml(.cs)`,
  `Modules/Statistics/StatisticsModule.cs`, `Modules/Statistics/StatsChart.cs`,
  `Modules/Statistics/StatisticsCompactView.xaml(.cs)`,
  `Modules/Statistics/StatisticsExpandedView.xaml(.cs)`.
- Updated: `App.xaml.cs`, `RightDock.csproj`, `Data/TaskStore.cs`,
  `Data/FocusStore.cs`, `Data/ReminderStore.cs`, `Data/MedicationStore.cs`,
  `Data/HabitStore.cs`, `Data/HealthStore.cs`, `Data/NoteStore.cs`,
  `Modules/Notes/NotesModule.cs`, `Modules/Notes/NotesExpandedView.xaml(.cs)`,
  `PROJECT_ARCHITECTURE.md`, `IMPLEMENTATION_PROGRESS.md`, `CHANGELOG.md`.

---

## [1.5.0] — 2026-11-16 — Phase 3 personal tracking modules

### Added
- **Habits module** (`Modules/Habits/`) — 10 seeded habits on first run (Drink water,
  Exercise, Read, Meditate, Walk outside, Journal, No sugar, Stretch, Vitamins, Eat
  vegetables) plus custom boolean/counter/duration habits with target and unit.
  One-click complete from the compact popup, a `+` step control for counter/duration
  habits, current and best streaks, a 7-day completion matrix, and full add/edit/delete
  from the dashboard. `HabitStore` computes streaks over a 400-day log window anchored
  to today or yesterday and upserts daily values on `(HabitId, LogDate)`.
- **Medication module** (`Modules/Medication/`) — medications, schedules and intake logs
  are stored separately (`Medications`, `MedicationSchedules`, `MedicationLogs`).
  Multi-time daily schedules on every day/weekdays/weekends, dose materialization for
  the last 6 days bounded by the medication's creation time, and statuses
  pending/taken/skipped/snoozed/missed (doses older than 2 h without action roll to
  missed). Today and last-7-days views, take/snooze (10/15/30/60 min)/skip actions,
  toast reminders with click-to-open, rescheduling of snoozed doses, and a midnight
  rollover task that materializes the next day. No dosage advice is displayed.
- **Health & Lifestyle module** (`Modules/Health/`) — eight metrics (water, meals,
  sleep, activity, vegetables, fruits, relaxation, weight) with per-metric targets and
  steps. Quick-log `−`/`+` rows, three code-drawn progress rings (water, activity,
  sleep), today's values, a weight entry with latest value, 7-day average and days
  logged, and a last-7-days bar table with averages. `HealthStore` upserts on
  `(LogDate, MetricType)`.
- `RingView` (`Modules/Health/RingView.cs`) — a code-only WPF ring control using a
  stroked track plus a `StreamGeometry`/`EllipseGeometry` arc, rounded caps and centered
  value/label.
- Schema migration **v2**: `MedicationLogs.SnoozeUntilUtc` plus
  `IX_MedicationLogs_Status` for status/cutoff queries.
- Automation ids for the new editors (`HabitName`, `HabitDescription`, `HabitTarget`,
  `HabitUnit`, `MedName`, `MedDosage`, `MedTimes`, `MedNotes`, `WeightInput`).

### Changed
- Three new modules registered in `App.OnStartup` after Notes; the dock now shows
  Habits (`lucide:target`), Medication (`lucide:pill`) and Health & Lifestyle
  (`lucide:heart-pulse`) and all three appear in Settings → Modules.
- Habits are seeded exactly once, only when the `Habits` table is empty, so user edits
  and deletions are never re-seeded.
- Medication reminders use scheduler keys `med:dose:<logId>:<ticks>` plus
  `med:rollover`; all keys are cancelled on deactivation and rebuilt on every refresh.
- Version 1.5.0.

### Verified
- Habits: 10 seeded rows appeared; completing Drink water wrote 8/8 with a 1-day
  streak, two `+` taps moved Exercise to 10/30 min, completing Journal updated "2 of
  10" and "1 day"; a custom counter habit (target 4 cups) saved, incremented and
  persisted across restart, then deleted with its history; backfilled prior-day logs
  produced filled week dots and a 3-day streak/best streak.
- Medication: saved Vitamin D 1 tablet at 12:35 and 20:00 → two pending doses; taking
  the 20:00 dose early showed "Taken 12:32"; the 12:35 dose toasted "Medication ·
  Vitamin D / 1 tablet · scheduled for 12:35."; snooze 10 min moved it to 12:46 and
  rescheduled; skip moved it to Skipped; a backdated medication (created 3 days ago)
  materialized four daily doses and all rolled to Missed; the last-7-days view grouped
  Today/Sun/Sat/Fri correctly; deleting the medication cascaded its schedules and logs.
- Health: quick log produced 2/8 glasses, 1/3 meals, 5/30 min and 0.5/8 h; rings drew
  colored arcs; weight 72.5 kg saved with a 7-day average and "1 of 7 days logged";
  invalid weight text showed inline red text without changing data; the last-7-days
  table showed per-metric bars, averages and "4 entries logged".
- Restart persistence for habits (values, streak, custom habit), medication statuses
  and health logs; all Phase 2 popups still open (Focus, Tasks, Reminders, Notes);
  single process; no errors in `rightdock.log`.
- Idle baseline on the shipped 1.5.0 build: ≈124 MB working set, ≈72 MB private bytes,
  0.02 CPU seconds over 30 s.

### Files
- Added: `Data/HabitStore.cs`, `Data/MedicationStore.cs`, `Data/HealthStore.cs`,
  `Modules/Habits/HabitsModule.cs`, `Modules/Habits/HabitsCompactView.xaml(.cs)`,
  `Modules/Habits/HabitsExpandedView.xaml(.cs)`, `Modules/Medication/MedicationModule.cs`,
  `Modules/Medication/MedicationFormat.cs`,
  `Modules/Medication/MedicationCompactView.xaml(.cs)`,
  `Modules/Medication/MedicationExpandedView.xaml(.cs)`,
  `Modules/Health/HealthModule.cs`, `Modules/Health/RingView.cs`,
  `Modules/Health/HealthCompactView.xaml(.cs)`,
  `Modules/Health/HealthExpandedView.xaml(.cs)`.
- Modified: `App.xaml.cs`, `Data/Database.cs`, `RightDock.csproj`.

---

## [1.4.0] — 2026-11-16 — Phase 2 productivity modules

### Added
- **Focus module** (`Modules/Focus/`) — 15/25/45/60-minute presets plus a custom
  minutes field, start/pause/resume/reset, wall-clock timing with persisted segments,
  optional task link, completion notification, today's total and a 7-day bar strip,
  session history (Today / This week / All time), and `FocusStore` over
  `FocusSessions` (`PlannedSeconds`, `ActualSeconds`, `IsCompleted`, `TaskId`).
- **Tasks module** (`Modules/Tasks/`) — quick add, full editor (title, due date/time,
  priority low/normal/high, category, project, notes, repeat none/daily/weekly/monthly),
  search, Today/Upcoming/Completed filters, per-row complete with automatic
  rescheduling of the next occurrence, delete, and `TaskStore` over `Tasks`/`Projects`.
- **Reminders module** (`Modules/Reminders/`) — quick add, editor (title, due, notes,
  repeat one-time/daily/weekly/custom-N-days), scheduler integration with keys
  `reminder:<id>:<due-ticks>`, toast notification with click-to-open, snooze 5/10/15
  minutes, complete with next-occurrence creation, overdue catch-up on startup, active
  and history lists, and `ReminderStore` over `Reminders`.
- **Quick Ideas / Notes module** (`Modules/Notes/`) — instant capture from the compact
  popup, pinned notes first, search, edit/delete, and one-click conversion into a task
  or a reminder (creates the row, then refreshes the target module), plus `NoteStore`
  over `Notes`.
- **Shared module UI kit** (`Modules/ModuleTheme.xaml`, merged into `App.xaml`) — dark
  section titles, text inputs, chips, round checkboxes, pill filters, list rows, primary
  buttons, card surfaces, scrollbar styling.
- **`ModuleUtil`** — relative-due parsing (`today`, `tomorrow`, `yyyy-MM-dd`, `HH:mm`,
  combinations), friendly date formatting, and duration/priority helpers shared by the
  modules.
- `ModuleContext.OpenExpanded(moduleId)` and `ModuleContext.RefreshModule(moduleId)` so a
  module (e.g. Notes) can open or refresh another module after cross-module actions.
- Compact/expanded view pairs for all four modules, each with matching automation IDs
  for testability (`TaskTitle`, `TaskDue`, `TaskSearch`, `ReminderTitle`,
  `ReminderDue`, `ReminderCustomDays`, `NoteTitle`, `NoteBody`, `NoteCapture`, …).
- Test helpers `tools/ui.ps1`, `tools/watch-toast.ps1`, `tools/close-dock.ps1`.

### Changed
- Modules are registered in `DockServices`; Focus, Tasks, Reminders and Notes appear in
  the dock by default and can be hidden or disabled from Settings → Modules.
- Module popup chrome, Settings and the icon picker now use a fully opaque `#FF242424`
  background (was `#F4242424`) to remove faint bleed-through over bright windows.
- `ModulePopupWindow` hosts module `UserControl` views; compact windows stay at 320 DIP,
  expanded at 980×680.
- Version 1.4.0.

### Fixed
- Focus completion toast said "You finished 1 minutes" — added singular/plural wording.
- Reminder "Complete" in the list now reliably advances recurring reminders (the UIA
  toggle artifact aside, the click path writes `IsCompleted` and inserts the next row).

### Verified
- Reminder round-trip: quick add → fired at the exact minute → toast "Reminder · Drink
  water / Due Today 11:39" → click opened the dashboard → snooze 5 min (DB, "Snoozed ·
  Today 11:44", refire) → complete on a daily rule completed the old row and created the
  next day's row → history showed "1 completed" → custom-N-days rule rescheduled.
- Restart reconciliation: a reminder due after a restart fired at its due time; an
  overdue reminder fired immediately on launch (expected).
- Focus: linked a task, ran a 1-minute session to completion (temporary preset), saw
  FOCUSING state with locked preset/task chips, progress, completion toast, DB row with
  `PlannedSeconds=60`, `ActualSeconds=60`, `IsCompleted=1`, `TaskId` set, 7-day total and
  history entry; presets restored to 15/25/45/60 in the shipped build.
- Tasks: created via the expanded editor using UIA `ValuePattern` + real chip clicks →
  DB `Priority=3`, `RecurrenceRule='monthly'`, due `2026-11-18T05:00Z`; completing a
  daily task created the next occurrence.
- Notes: capture, pin, search, and convert-to-task/reminder confirmed in the UI and the
  `Tasks`/`Reminders` tables.
- Persistence across restarts for tasks, reminders (including next occurrences), notes,
  focus history and module visibility; single process; Settings → Modules lists the four
  modules with enable toggles and Hide/Show links.
- Idle baseline on the shipped 1.4.0 build: ≈123 MB working set, ≈71 MB private bytes,
  0 CPU seconds over 30 s.

### Files
- Added: `Data/FocusStore.cs`, `Data/TaskStore.cs`, `Data/ReminderStore.cs`,
  `Data/NoteStore.cs`, `Modules/ModuleTheme.xaml`, `Modules/ModuleUtil.cs`,
  `Modules/Focus/*`, `Modules/Tasks/*`, `Modules/Reminders/*`, `Modules/Notes/*`,
  `tools/ui.ps1`, `tools/watch-toast.ps1`, `tools/close-dock.ps1`.
- Modified: `App.xaml`, `App.xaml.cs`, `RightDock.csproj`, `Modules/ModuleContext.cs`,
  `Modules/ModuleManager.cs`, `Modules/ModulePopupWindow.xaml`, `SettingsWindow.xaml`,
  `IconPickerWindow.xaml`.

---

## [1.3.0] — 2026-11-16 — Phase 1 core infrastructure

### Added
- `DockServices` composition root owning settings, SQLite database, scheduler,
  notification service and module manager; disposed on exit.
- SQLite persistence in `%APPDATA%\RightDock\rightdock.db` via
  `Microsoft.Data.Sqlite` 5.0.17: WAL, foreign keys, busy timeout, parameterized
  commands, versioned migrations (`PRAGMA user_version`), and 16 productivity tables
  with indices (Tasks, Projects, Reminders, FocusSessions, Habits, Medicine, Health,
  Notes, Learning, AppSettings, ModuleSettings).
- `ModuleStateStore` persisting enabled/visible/sort/config per module id; disabling a
  module retains its data.
- Module contract (`IProductivityModule`, `ProductivityModule`) with lazy
  `Initialize`/`Activate`/`Deactivate` lifecycle, compact/expanded/settings views.
- `ModuleManager`: duplicate-id guard, persisted state, enable/disable, hide/show in
  dock, ordering, popup lifecycle, and dock entry events.
- `PopupHost` and `ModulePopupWindow`: one compact + one expanded window per module,
  320 DIP compact popup placed beside its dock button, 980×680 centered expanded view,
  Esc/outside-click behavior, named close button, and an anchor-cursor guard so the
  dock button toggles the popup instead of reopening it.
- Centralized `Scheduler`: keyed one-shot/interval tasks, one `DispatcherTimer` armed to
  the earliest UTC timestamp, 50 ms floor / 1 h ceiling, exception isolation, and
  `Reconcile()` for startup.
- `NotificationService` and native in-app `ToastWindow` cards: master enable flag,
  bottom-right stack (max 4), 6 s auto-dismiss, click actions, no activation stealing.
- Settings window grows to 500×520 with **Modules** (enable/disable, Hide/Show in dock)
  and **Notifications** (master toggle + test button) tabs; `NotificationsEnabled` added
  to `settings.json`.
- Structured `Log` with 1 MB rotation in `%APPDATA%\RightDock\logs`.
- Version 1.3.0.

### Changed
- `App.OnStartup` now creates `DockServices` before the window and starts module
  lifecycle.
- Module icons render in the dock between pins and the Add/Settings/Close stack; compact
  and expanded module popups pause auto-hide through `PopupCount`.
- Settings text for notifications now describes the in-app card behavior.

### Fixed
- Crash when a module popup was closed by deactivation during its own closing sequence
  (`Cannot … Close while a Window is closing`) — added a closing guard and deferred
  close.
- Compact popup failed to close when clicking its dock button (deactivation closed it
  before the click was processed) — added the anchor-cursor guard.

### Verified
- Schema v1 creation and module-setting round-trips; scheduled +3 s notification fired;
  toast card appeared bottom-right, faded after 6 s, and did not take focus.
- Module enable/disable, Hide/Show in dock, and persistence across restart; popup
  toggle/outside-click/Esc/expanded-size behavior; auto-hide pause with a popup open.
- Notifications off shows "Notifications are disabled."; on shows a test card.
- Launcher features (pins, icons, Add, Settings, Close) remain functional.

### Files
- Added: `DockServices.cs`, `Data/Database.cs`, `Data/ModuleStateStore.cs`,
  `Modules/IProductivityModule.cs`, `Modules/ModuleContext.cs`,
  `Modules/ModuleManager.cs`, `Modules/PopupHost.cs`,
  `Modules/ModulePopupWindow.xaml`, `Modules/ModulePopupWindow.xaml.cs`,
  `Services/Log.cs`, `Services/Scheduler.cs`, `Services/NotificationService.cs`,
  `Services/ToastWindow.cs`.
- Modified: `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`,
  `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, `AppSettings.cs`,
  `RightDock.csproj`.

---

## [1.2.0] — 2026-11-15 — Settings

### Added
- Settings gear button between Add and Close, opening a 440×470 borderless dark popup
  beside the dock.
- Segmented three-tab layout: **General**, **Appearance**, **Startup**; tabs switch
  pages in place, Esc closes, header drags the window.
- Auto-hide toggle with a short explanation of the 50 px reservation and edge reveal.
- Ten preset icon colors plus a rainbow custom swatch that opens the native Windows
  color dialog (`FullOpen`); selected swatch shows a ring and check, current hex is
  displayed.
- Run-at-Windows-startup toggle writing
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\RightDock`; failures revert the
  toggle and show inline error text; the resolved exe path is shown for reference.
- Live updates: `AutoHideChanged` and `IconColorChanged` events apply changes to the
  dock immediately and save `settings.json` on every change.

### Changed
- Pin icon rendering now uses the global settings color instead of a fixed gray; the
  icon picker previews glyphs in the same color.
- Auto-hide scheduling respects the new setting: disabled forces the dock shown and
  reserved, enabled schedules a hide.

### Files
- Added: `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, `AppSettings.cs`,
  `StartupRegistration.cs`.
- Modified: `RightDock.csproj` (`UseWindowsForms`), `MainWindow.xaml`,
  `MainWindow.xaml.cs`, `IconPickerWindow.xaml.cs`, `App.xaml`.

### Verified
- Preset and custom colors recolor the dock live and persist across restart
  (`#123456` test); canceling the native dialog leaves the color unchanged.
- Auto-hide off keeps the dock visible and reserved after 3 s idle; on restores
  hide-and-release.
- Startup registry value written and removed; settings restored to defaults after
  testing.

---

## [1.1.0] — 2026-11-15 — Auto-hide and icon system

### Added
- Auto-hide engine: 110 ms hover poll, right-edge (5 px) reveal, 650 ms leave delay,
  170 ms cubic ease-out slide.
- Full space release while hidden — the dock slides fully offscreen, then `ABM_REMOVE`
  restores the desktop work area; reveal re-reserves before sliding in.
- Fullscreen suppression via `ABN_FULLSCREENAPP`, and modal-popup pausing through a
  depth counter.
- 181-glyph Lucide outline icon catalog generated into `IconCatalog.cs`, organized by
  category (Health, Office, Time, Marketing, Plotter, Printing, Files, Design, Media,
  Security, Transport, Weather, Utility).
- `IconRepository`: built-in geometry cache, user SVG loading from
  `%APPDATA%\RightDock\icons`, uniform 1.5 px stroked rendering, graceful fallbacks.
- Icon picker popup (440×640): search, category sections, "This app" reset row,
  current-icon highlight, "Open icons folder".
- Right-click **Change icon…** on each pin.

### Changed
- Pin visuals support three icon sources: catalog line icon, the app's own shell icon
  (sentinel `app`), or a user SVG (`user:<file>`).
- Dock tooltips, context menu and icon buttons restyled for consistency.

### Files
- Added: `IconCatalog.cs`, `IconRepository.cs`, `IconPickerWindow.xaml`,
  `IconPickerWindow.xaml.cs`, `tools/gen-icons.js`, `tools/lucide-icon-nodes.json`.
- Modified: `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml`, `Icons.cs`,
  `NativeMethods.cs`.

### Verified
- Hidden dock released the reservation; edge hover re-reserved and revealed it.
- Search filtering, category rendering, custom SVG icons, and persistence of icon
  assignments across restart.

---

## [1.0.0] — 2026-11-15 — Dock core

### Added
- Native Windows 11 right-side dock: 50 DIP wide, full monitor height, dark
  `#1C1C1C` surface with a hairline left border.
- AppBar reservation (`ABM_NEW` / `ABM_QUERYPOS` / `ABM_SETPOS`) so no window can
  overlap the dock strip; removal restores the work area.
- Topmost, non-activating tool window (`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`,
  `ShowActivated=false`), hidden from taskbar and Alt+Tab.
- Add button with multi-select file picker (`exe`, `lnk`, `bat`, `cmd`, `msc`,
  `appref-ms`), duplicate guard, and pins list.
- Click to launch with the app's directory as working directory.
- Right-click **Remove from dock**.
- Close button that exits and releases the reservation.
- Shell icon extraction with a letter-tile fallback.
- Single-instance mutex; hover tooltips; dark context menu and tooltip styles.
- PerMonitorV2 DPI awareness with geometry refresh on `WM_DPICHANGED` and
  `WM_DISPLAYCHANGE`; fullscreen app notifications.
- Persistence in `%APPDATA%\RightDock\pins.json`.

### Files
- Added: `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`,
  `AppBarDock.cs`, `NativeMethods.cs`, `PinStore.cs`, `Icons.cs`, `app.manifest`,
  `RightDock.csproj`.

### Verified
- 50 px work-area reservation at 125 % and 100 % DPI; maximized windows respected it.
- Launching, adding, removing, closing, single-instance behavior, and pin persistence.
