# RightDock — Project Architecture

> Current release: **1.7.0** (launcher + Phase 1 infrastructure + Phase 2 productivity
> modules + Phase 3 personal tracking + Phase 4 planning and insight + Phase 5 quick
> tools and learning)
> Target direction: native Windows **Productivity & Life Management Dock** (see §11)

RightDock is a single-process WPF application that renders a 50 px taskbar-like dock on
the right edge of the primary monitor. It reserves desktop work area through the Windows
AppBar protocol, launches pinned applications, and supports a 181-glyph outlined icon
system, auto-hide with edge reveal, a five-tab settings popup, and a productivity layer:
SQLite persistence, a module manager with compact/expanded popups, a centralized
scheduler, in-app notification cards, and the Dashboard, Focus, Tasks, Reminders, Notes,
Habits, Medication, Health & Lifestyle, Calendar, Statistics, Quick Tools and Learning
modules.

All functionality is implemented in one WPF process. No WebViews, Electron, separate
module executables, background services, or third-party UI frameworks are used.

---

## 1. Technology stack

| Item | Choice |
| --- | --- |
| Language / runtime | C# 9, .NET 5 (`net5.0-windows`) |
| UI framework | WPF (`UseWPF`), plus `UseWindowsForms` only for the native `ColorDialog` |
| Windows integration | Win32 P/Invoke (`shell32`, `user32`, `dwmapi`), registry |
| Persistence | `System.Text.Json` files + SQLite (`Microsoft.Data.Sqlite`) under `%APPDATA%\RightDock` |
| Theming | Hand-written XAML styles (no theme libraries) |
| Icons | Lucide (ISC) path data committed as `IconCatalog.cs`; optional user SVGs |
| Packaging | `dotnet publish` framework-dependent output in `publish/` |
| Min OS | Windows 10 1809+ (manifest targets Windows 10/11, PerMonitorV2 DPI) |

Dependencies outside the .NET/WPF runtime: **Microsoft.Data.Sqlite 5.0.17** (managed
assembly plus the `e_sqlite3` native runtime copied by `dotnet publish`). `tools/` contains
a Node.js script used only at development time to regenerate the icon catalog.

---

## 2. Repository layout

```
RightDock/
├─ App.xaml                  Application resources: DockButton, dark ToolTip, ContextMenu, MenuItem
├─ App.xaml.cs               Single-instance mutex + service startup + main window
├─ DockServices.cs           Owns settings, database, scheduler, notifications, module manager
├─ MainWindow.xaml           ｜ Dock shell: 50 px column, pins, module buttons, Add / Settings / Close
├─ MainWindow.xaml.cs        ｜ AppBar reservation, auto-hide state machine, pin/module actions
├─ AppBarDock.cs             SHAppBarMessage wrapper (ABM_NEW / QUERYPOS / SETPOS / REMOVE)
├─ NativeMethods.cs          Win32 structs and P/Invoke declarations
├─ PinStore.cs               Pin model + pins.json load/save
├─ AppSettings.cs            AppSettings model + settings.json load/save
├─ StartupRegistration.cs    HKCU Run-key registration
├─ Icons.cs                  Shell file icon extraction (SHGetFileInfo)
├─ IconCatalog.cs            Auto-generated 181 Lucide entries (key, name, category, path)
├─ IconRepository.cs         Built-in + user icon resolution, geometry cache, rendering
├─ IconPickerWindow.xaml/.cs Icon browser popup ("Choose icon")
├─ SettingsWindow.xaml/.cs   Six-tab settings window (General, Appearance, Modules, Notifications, Data, Startup)
├─ Data/
│  ├─ Database.cs            SQLite connection, pragmas, versioned migrations, query helpers
│  ├─ DataBackupService.cs   ZIP backup, inspection, staged restore and safety copies
│  ├─ ModuleStateStore.cs    ModuleSettings persistence (enabled/visible/sort/config)
│  ├─ FocusStore.cs          FocusSessions rows, recent activity, daily totals, ranges
│  ├─ TaskStore.cs           Tasks CRUD, views, search, counts, ranges, recurrence-insert
│  ├─ ReminderStore.cs       Reminders CRUD, snooze, completion, recurrence, date ranges
│  ├─ NoteStore.cs           Notes CRUD, search, pin, categories, archive, timestamps
│  ├─ HabitStore.cs          Habits + log window, batches, day-range queries
│  ├─ MedicationStore.cs     Medications, schedules, intake logs, occurrences by day
│  ├─ HealthStore.cs         HealthLogs, metric definitions, day-range averages
│  ├─ BreathingStore.cs      Breathing sessions, recent history, session/seconds totals
│  └─ LearningStore.cs       Learning goals, sessions, progress, filters and statistics
├─ Modules/
│  ├─ IProductivityModule.cs Module contract + ProductivityModule base class
│  ├─ ModuleContext.cs       Services handed to modules + OpenExpanded/RefreshModule callbacks
│  ├─ ModuleManager.cs       Registration, lifecycle, dock entries, enable/disable/visibility
│  ├─ ModuleTheme.xaml       Shared dark styles: cards, inputs, chips, buttons, progress
│  ├─ ModuleUtil.cs          Shared formatting, parsing and recurrence helpers
│  ├─ PopupHost.cs           One compact + one expanded popup per module
│  ├─ ModulePopupWindow.xaml/.cs  Shared popup shell (title, icon, close, placement)
│  ├─ Dashboard/             DashboardModule + daily overview widgets
│  ├─ Focus/                 FocusModule + compact/expanded timer views
│  ├─ Tasks/                 TasksModule + compact/expanded list/editor views
│  ├─ Reminders/             RemindersModule (scheduler integration) + views
│  ├─ Notes/                 NotesModule (capture, conversion, archive) + views
│  ├─ Habits/                HabitsModule + compact/expanded tracking views
│  ├─ Medication/            MedicationModule + dose views and schedule editor
│  ├─ Health/                HealthModule + rings, quick log, 7-day views
│  ├─ Calendar/              CalendarModule + month grid planner and agenda
│  ├─ Statistics/            StatisticsModule + StatsChart + daily/weekly/monthly views
│  ├─ QuickTools/            QuickToolsModule + note capture, breathing and screen breaks
│  └─ Learning/              LearningModule + goals, sessions and progress views
├─ Services/
│  ├─ Log.cs                 Append-only log under %APPDATA%\RightDock\logs
│  ├─ Scheduler.cs           Central timestamp-based scheduler (one DispatcherTimer)
│  ├─ NotificationService.cs In-app notification coordinator (enabled flag, stacking)
│  ├─ SoundService.cs        Bell playback, triple/single ring sequencing, preview cache
│  ├─ BellSynth.cs           Ten synthesized bell timbres rendered to in-memory PCM WAV
│  └─ ToastWindow.cs         Borderless layered notification card
├─ app.manifest              asInvoker, Win10/11 supportedOS, PerMonitorV2
├─ RightDock.csproj          Build configuration
├─ tools/
│  ├─ gen-icons.js           Generates IconCatalog.cs from Lucide data
│  ├─ lucide-icon-nodes.json Lucide source data (icon name → SVG nodes)
│  ├─ make-icon.ps1          Draws and writes the multi-size app icon (Resources\RightDock.ico)
│  ├─ ui.ps1                 UIA test helper: list/inspect/invoke/click/expand/select/shot
│  ├─ watch-toast.ps1        Polls for notification cards and can click them
│  ├─ close-dock.ps1         Graceful WM_CLOSE of the running dock
│  ├─ measure.ps1            Process CPU/RAM/handle profiling sample
│  ├─ stress-ui.ps1          Repeated module popup open/close with leak metrics
│  └─ publish.ps1            Release publish, portable ZIP and optional Inno Setup compile
├─ Resources/RightDock.ico   Application icon (PNG entries 16–256 px)
├─ installer/RightDock.iss   Inno Setup 6 installer definition
├─ THIRD-PARTY-NOTICES.md    Lucide, Feather, Microsoft.Data.Sqlite and SQLite notices
├─ PROJECT_ARCHITECTURE.md   This document
├─ IMPLEMENTATION_PROGRESS.md
└─ CHANGELOG.md
```

`bin/`, `obj/`, `publish/` and `dist/` are build outputs and are not versioned. User data
lives outside the repository (see §8).

---

## 3. Startup and lifecycle

```
RightDock.exe [--restart]
   └─ App.OnStartup
        ├─ Mutex "Local\RightDock.SingleInstance" (second instance → Shutdown;
        │  --restart waits up to 10 s for the previous instance to release it)
        ├─ DataBackupService.ApplyPendingRestore() (before the database is opened)
        ├─ new DockServices()
        │    ├─ settings.json → AppSettings
        │    ├─ Database.Initialize() (WAL, foreign keys, migrations)
        │    ├─ DataBackupService
        │    ├─ Scheduler, NotificationService, SoundService, ModuleManager
        │    └─ Modules.Start() → Initialize/Activate each enabled module
        └─ new MainWindow(services).Show()
             ├─ constructor: build pin buttons + module buttons, create timers
             ├─ OnSourceInitialized: tool-window + no-activate styles, no DWM rounding,
             │                       AppBar callback registration, AppBarDock instance
             └─ OnLoaded: reserve AppBar, place window, start 110 ms hover timer
```

Shutdown is user-driven only, except the restore flow, which stages a backup and starts a
new process with `--restart` before shutting the current one down. `OnClosing` sets
`_closing`, stops timers, and calls `ABM_REMOVE` so the reserved work area is released
immediately. `App.OnExit` disposes `DockServices` (module lifecycle, scheduler,
notifications, database).

The dock is a tool window: `ShowInTaskbar=false`, `ShowActivated=false`,
`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, topmost via both `Topmost=true` and
`SetWindowPos(HWND_TOPMOST)`. It never steals focus from the foreground application.

---

## 4. Window shell and desktop reservation

### 4.1 AppBar protocol (`AppBarDock.cs`)

`Reserve(double dpiScale)` performs a full reservation cycle:

1. `ABM_NEW` once per app lifetime, with `uEdge = ABE_RIGHT` and the callback message
   registered by `RegisterWindowMessage("RightDock_AppBarCallback")`.
2. `MonitorFromPoint(0,0, MONITOR_DEFAULTTONEAREST)` selects the primary monitor.
3. `GetMonitorInfo` reads `rcMonitor` (physical pixels).
4. `ABM_QUERYPOS` lets the shell suggest a rectangle for a right-docked bar.
5. The rectangle's left edge is set to `right − round(50 × dpiScale)`, then `ABM_SETPOS`
   commits it. The shell shrinks every maximized window's work area accordingly, so no
   window can overlap the reserved strip.
6. `ReservedRect`, `MonitorRect` and `LastReserveTicks` are stored for callers.

`Unregister()` sends `ABM_REMOVE`, which restores the desktop work area. This is used
both on close and while the dock is auto-hidden.

### 4.2 Callback handling

`MainWindow.WndProc` handles the registered AppBar message:

| Notification | Reaction |
| --- | --- |
| `ABN_POSCHANGED` | Re-reserve and re-place the window unless a reserve just happened (< 250 ms `WithinRecentReserve` guard against self-triggered loops) |
| `ABN_FULLSCREENAPP` | Suppress/hide the dock while a fullscreen app owns the monitor; resume a delayed hide when it ends |

`WM_DISPLAYCHANGE` and `WM_DPICHANGED` also trigger `RefreshGeometry()`; DPI scale is
re-read from `wParam` so the physical width stays 50 DIP.

### 4.3 Placement and sliding

`PlaceWindow` wraps `SetWindowPos` and caches the current physical rect
(`_currentLeft/Top/Width/Height`) used by hit-testing. `SlideTo` animates the left edge
with a 16 ms `DispatcherTimer` over 170 ms using a cubic ease-out
(`1 − (1−t)³`). The timer runs only while an animation is in progress.

---

## 5. Auto-hide state machine

Input: 110 ms hover polling (`GetCursorPos`) — chosen because a global mouse hook would
be heavier and more invasive. Each tick exits early when the dock is closing, a modal
dialog is open (`_modalDepth > 0`), a module popup is open (`_popupCount > 0`), or
geometry is unknown.

States and transitions:

```
                 edge hover (x ≥ monitor.Right − 5) or cursor over dock
   HIDDEN  ───────────────────────────────────────────────────────────▶  SHOWN
     ▲     ◀───────────────────────────────────────────────────────────
     │        cursor outside dock + HideDelayMs (650 ms) elapsed
     │
     └─ slide fully offscreen (left = monitor.Right) → ABM_REMOVE  [space released]
```

| Rule | Implementation |
| --- | --- |
| Reveal zone | Right 5 physical px of the monitor, full monitor height band |
| Hide delay | 650 ms after the cursor leaves dock + 4 px margin |
| Start-up grace | First hide scheduled 1600 ms after load |
| Space release | `ABM_REMOVE` only after the slide completes (`CompleteHide`) |
| Space reclaim | `Reserve()` before the slide-in (`ShowDock`) |
| Auto-hide disabled | Dock forced shown and reserved; hides are cancelled |
| Fullscreen app | Always wins: dock hides and stays hidden until `ABN_FULLSCREENAPP=false` |
| Modal popups | Context menu, file dialog, icon picker and settings increment `_modalDepth`, freezing hide logic |
| Module popups | `PopupHost.OpenCountChanged` updates `_popupCount`; the dock stays revealed while any compact/expanded popup is open |
| Re-entrancy | `_dockShown` flag + `_slideHides` flag; slide timer restarted per transition |

When auto-hide is toggled in Settings, `MainWindow` re-evaluates immediately:
enabling schedules a hide, disabling shows and reserves the dock.

---

## 6. Icon system

Three icon sources are merged into one picker:

| Source | Key format | Resolution |
| --- | --- | --- |
| Lucide catalog (181 glyphs) | `lucide:<name>` | Path geometry from `IconCatalog`, rendered as stroked `Path` |
| Pinned app's own icon (sentinel) | `app` | `SHGetFileInfo` + `Imaging.CreateBitmapSourceFromHIcon` |
| User SVGs in `%APPDATA%\RightDock\icons` | `user:<filename>` | Minimal SVG parser (path, circle, ellipse, rect, line, polyline, polygon) |

`IconRepository` responsibilities:

- `ResolveKey` — blank keys fall back to `lucide:app-window`.
- `GetItems` / `GetCategoryOrder` — ordered catalog for the picker, with a lazy-loaded
  user-icon section ("My icons").
- `CreateIconElement(key, size, stroke)` — `System.Windows.Shapes.Path` with
  `StrokeThickness=1.5`, round caps/joins, `Stretch=Uniform`. Returns `null` for invalid
  geometry so callers can fall back to a shell icon or a letter tile.
- Geometry cache (`Dictionary<string, Geometry>`) frozen after parse; SVGs reparsed only
  when `RefreshUserIcons()` is requested.
- All failures are swallowed and degrade to a fallback; a malformed file cannot crash
  the dock.

Rendering color is global: `MainWindow` converts `settings.IconColor` into a frozen
`SolidColorBrush` and repaints pins on change. The picker receives the same color so
previews match the dock.

`IconCatalog.cs` is generated (see §10). `Entry` records key, display name, category
(Health, Office, Time, Marketing, Plotter, Printing, Files, Design, Media, Security,
Weather, Transport, Utility, …) and SVG path data. Every path is validated with
`Geometry.Parse` by the generator before being emitted.

The picker popup (`IconPickerWindow`) is 440×640, dark, borderless, draggable by its
header, closes on Esc, filters by icon/category name, highlights the current icon, and
offers "Use <app>'s own icon" plus "Open icons folder" for user SVG authoring.

---

## 7. UI design system

Shared visual language across the dock and both popups:

| Token | Value |
| --- | --- |
| Dock background | `#FF1C1C1C`, left border `#26FFFFFF` |
| Popup surface | `#F4242424`, border `#2EFFFFFF`, radius 14, soft drop shadow |
| Hover / pressed overlay | `#2BFFFFFF` / `#40FFFFFF` (dock), `#2EFFFFFF` / `#40FFFFFF` (popups) |
| Primary text | `#F0FFFFFF`–`#F7F7F7` |
| Secondary text | `#9A9A9A`, section captions `#8A8A8A` uppercase 10.5 px |
| Accent link | `#9CC7E9`, hover `#C9E4FA` |
| Toggle ON | light track `#E6E6E6`, dark thumb `#1C1C1C` |
| Radius | 8 (dock buttons), 6 (menu items), 7 (tabs), 12 (toggle), 14 (popup shell) |
| Notification card | surface `#F5242426`, border `#33FFFFFF`, radius 12, 336 px wide, bottom-right stack |
| Type | Segoe UI Variable Text / Segoe UI; Fluent Icons for chrome glyphs |
| Motion | 170 ms dock slide; 180 ms toast fade-in, 160 ms fade-out |

Dock buttons are 38×38 with 26 px icons. The bottom stack is fixed:
**Add (E710) → Settings (E713) → Close (E711)**, each with a left-placed tooltip.

Settings (`SettingsWindow`, 560×560, six tabs) uses a segmented tab strip with
six pages:

1. **General** — auto-hide toggle + explanation of the 50 px reservation / edge reveal.
2. **Appearance** — 10 preset swatches + rainbow "custom" swatch, current hex label,
   native Windows color dialog (`FullOpen`).
3. **Modules** — one row per registered module with icon, name, description, an
   enable/disable toggle, and a **Hide from dock / Show in dock** link; the empty state
   reads "No modules are registered yet."
4. **Notifications** — master notifications toggle, a **Send test notification** button,
   and a **SOUNDS** section with two bell pickers (Focus completion, Reminders), each with
   a **Preview** button and a shared status line; the inline status reports success or
   "Notifications are disabled."
5. **Data** — database path and live summary, integrity check, backup creation, staged
   restore with restart, copy diagnostics and the About row.
6. **Startup** — run-at-Windows-startup toggle, resolved exe path, inline error text if
   the registry write fails (toggle reverts on failure).

Settings are applied live through events (`AutoHideChanged`, `IconColorChanged`) or
immediate `ModuleManager` calls, and persisted immediately; there is no Save/Apply step.

---

## 8. Persistence and OS integration

```
%APPDATA%\RightDock\
├─ pins.json       [ { "Name": …, "Path": …, "Icon": "lucide:…" } ]
├─ settings.json   { "AutoHide": true, "IconColor": "#A9A9A9", "NotificationsEnabled": true,
│                    "FocusSound": "classic", "ReminderSound": "soft" }
├─ rightdock.db    SQLite database (WAL): module state + all productivity data
├─ backups\        ZIP backups chosen by the user, plus pre-restore-<timestamp>\ safety copies
├─ restore-staging\  extracted backup awaiting apply at next start
├─ restore.pending   marker written by StageRestore; deleted after a successful apply
├─ logs\           rightdock.log (1 MB rotation, UTC timestamps)
└─ icons\          optional user *.svg icons (created on demand)
```

The SQLite database is created lazily by `Database.Initialize()` with
`journal_mode=WAL`, `foreign_keys=ON`, and `busy_timeout=5000`. Migrations run in
transactions and are versioned with `PRAGMA user_version`; version 1 creates all
productivity tables (`Tasks`, `TaskCategories`, `Projects`, `Reminders`,
`FocusSessions`, `Habits`, `HabitLogs`, `Medications`, `MedicationSchedules`,
`MedicationLogs`, `HealthLogs`, `Notes`, `LearningGoals`, `LearningSessions`,
`AppSettings`, `ModuleSettings`) plus indices on due dates, completion state, log dates
and updated timestamps. Version 2 (1.5.0) adds `MedicationLogs.SnoozeUntilUtc` and
`IX_MedicationLogs_Status`, so snoozed doses and the missed-cutoff scan stay indexed.
Habit logs are upserted on `(HabitId, LogDate)` and health logs on
`(LogDate, MetricType)`. 1.6.0 adds no schema changes: the Notes manager reuses the
existing `Notes.IsArchived` column for soft-hide/restore, and Dashboard, Calendar and
Statistics read-only aggregate the existing tables. Version 3 (1.7.0) adds
`BreathingSessions` (`SessionId`, `PatternKey`, `PatternName`, `Cycles`,
`DurationSeconds`, `StartedUtc`, `EndedUtc`, `CreatedUtc`) with an index on `StartedUtc`;
breathing data is kept separate from Focus so it never distorts focus totals. Learning
reuses the version 1 `LearningGoals` and `LearningSessions` tables, with sessions linked
by `GoalId` (`ON DELETE SET NULL`) so deleting a goal keeps its history.

`ModuleSettings` stores `IsEnabled`, `IsVisible`, `SortOrder`, and `ConfigJson` per
module id; `ModuleManager` loads it at startup and saves on every change. Disabling a
module keeps its row and all module data intact.

| Integration | Mechanism |
| --- | --- |
| Single instance | Named mutex `Local\RightDock.SingleInstance` |
| Start with Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\RightDock` = quoted exe path |
| Desktop reservation | AppBar protocol (see §4) |
| File launch | `Process.Start(UseShellExecute=true)` with the exe's folder as working directory |
| Icon extraction | `SHGetFileInfo` with `.exe` fallback for missing targets |
| DPI | PerMonitorV2 manifest + `WM_DPICHANGED` handling |
| Display change | `WM_DISPLAYCHANGE` → re-read monitor rect and re-reserve |
| Sleep / resume | `WM_POWERBROADCAST` (`PBT_APMRESUMEAUTOMATIC`, `PBT_APMRESUMESUSPEND`) → scheduler reconcile + re-reserve |
| Clock change | `WM_TIMECHANGE` → scheduler reconcile |
| Fullscreen detection | `ABN_FULLSCREENAPP` from the shell |
| Notifications | In-app `ToastWindow` cards (no shell tray icon) |
| Backup / restore | ZIP archive with manifest, staged restore applied before DB open, safety copy |

Storage failures are non-fatal by design: corrupt or missing JSON falls back to
defaults, database errors are logged and degrade to in-memory module defaults, and
writes are wrapped so a locked file cannot take down the dock. Pins are never deleted by
settings changes; startup registration is the only registry state the app owns.

### 8.1 Backup and restore (`Data/DataBackupService.cs`, 1.8.0)

A backup is one ZIP chosen by the user (default name
`RightDock-backup-yyyyMMdd-HHmmss.zip`) containing:

| Entry | Source |
| --- | --- |
| `rightdock.db` | `SqliteConnection.BackupDatabase` copy, switched to `journal_mode=DELETE` so the archive is one self-contained file (no `-wal`/`-shm`) |
| `settings.json` | dock settings (auto-hide, icon color, notifications) |
| `pins.json` | dock pins |
| `icons/` | user SVG icons, when present |
| `backup.json` | app version, UTC creation time, schema version and per-table row counts |

Create validates the copy (`PRAGMA integrity_check`, schema range 1…current) before
zipping, so a corrupt archive is never produced. `InspectBackup` extracts the database
to a temp folder, re-validates it and returns a summary used by the confirmation dialog
(`schema v3 · 4.2 MB · 1,234 rows`).

Restore is staged, not applied live: `StageRestore` extracts entries with a path-traversal
guard, validates the staged database and JSON, then writes `restore.pending`. The settings
window starts a new process with `--restart` and shuts down; at the next start
`ApplyPendingRestore()` runs **before** the database is opened:

1. Re-validate the staged database; stop on failure.
2. Copy the current database, settings and pins to
   `backups\pre-restore-yyyyMMdd-HHmmss\`.
3. Delete `rightdock.db-wal`/`-shm`, copy the staged database (and its WAL/SHM when the
   archive contains them), then settings, pins and icons.
4. Delete the staging folder and marker, log the row counts, and show the
   "Backup restored" toast.
5. On any exception, roll the database back from the safety copy, delete the marker and
   show "Restore failed" with the reason.

The `--restart` process waits for the single-instance mutex (40 × 250 ms) so it cannot
open the database while the old process is still shutting down; a duplicate start without
`--restart` exits immediately.

---

## 9. Threading and resource policy

- UI thread only; no worker threads or background services yet.
- `DispatcherTimer`s: the 110 ms hover poll while running, a 16 ms animation timer that
  exists only during a 170 ms slide, the scheduler's single timer armed to the earliest
  due timestamp (re-armed at most once per hour so sleep/resume is handled), Focus's 1 s
  tick only while a session is active, Quick Tools' 1 s breathing tick only while a
  breathing session runs, and a refresh timer that exists only while a Dashboard (60 s
  compact / 30 s expanded), Calendar or Statistics view is loaded.
- SQLite access is synchronous and serialized behind one connection lock; operations are
  short and scoped to the UI thread. Callbacks from the scheduler run on the UI thread.
- Notifications are owned by `NotificationService`: at most four `ToastWindow` cards are
  stacked bottom-right, each auto-dismissing after 6 s with a 160 ms fade-out.
- All Win32 calls are non-blocking; `SHGetFileInfo` is the only synchronous shell call.
- Brush and geometry objects are frozen and reused; pin visuals are rebuilt only when the
  pin list or icon color changes.
- Measured baseline: launcher-only 1.2.0 was **~76 MB working set, ~58 MB private,
  ≈0.2 % average CPU**; 1.3.0 with Phase 1 services idled at **~99 MB working set,
  ~59 MB private, ≈0.05 % of one core**; 1.4.0 with all four Phase 2 modules initialized
  idled at **~123 MB working set, ~71 MB private, 0 % CPU over 30 s**; 1.5.0 with all
  seven modules registered idles at **~124 MB working set, ~72 MB private, 0.02 CPU
  seconds over 30 s**; 1.6.0 with all ten modules registered and every Phase 4 view
  opened once idles at **~146 MB working set, ~94 MB private, 0.08 CPU seconds over
  30 s**; 1.7.0 with twelve modules registered idles at **126.8 MB working set, 71.5 MB
  private, 0.047 CPU seconds over 30 s** (Windows 11, 2560×1600 @125 %); 1.8.0 measured
  **150.4 MB working set, 98.9 MB private, 0.016 CPU seconds over 10 s, 476 handles,
  9 threads** after a stress run; 1.9.0 (bell sounds registered, no sound playing)
  idles at **109.0 MB working set, 62.2 MB private, 0.016 CPU seconds over 10 s,
  463 handles, 17 threads** on a fresh published process. Bell streams are created on
  the first ring and cached per selected tone, so an idle session holds no audio buffers.
  A 27-cycle UI stress run (~650 popup open/close operations across all
  twelve modules) plateaued at **≈111 MB private / ≈163 MB working set with flat handles
  (483–492), GDI (38) and USER (31) counts**, confirming popups are reclaimed after GC.

These constraints are the benchmark for every new module: reuse the process, avoid
polling, prefer timestamp math over per-item timers, load UI on demand.

---

## 10. Tooling

`tools/gen-icons.js` reads `tools/lucide-icon-nodes.json` (Lucide icon name → SVG node
tree), flattens supported node types into SVG path data, validates each with .NET
`Geometry.Parse` expectations (via the generated catalog build), and writes
`IconCatalog.cs` with stable ordering and categories.

Regenerate after editing the JSON:

```bash
node tools/gen-icons.js
dotnet build RightDock.csproj
```

`IconCatalog.cs` is marked auto-generated; manual edits are overwritten.

Release tooling:

| Script | Purpose |
| --- | --- |
| `tools/publish.ps1` | Refuses to run while RightDock is open, reads the version from the csproj, publishes `win-x64` framework-dependent (or `-SelfContained`), writes `dist\RightDock-<version>-win-x64.zip`, and compiles the installer with `-Installer` when Inno Setup 6 is installed |
| `tools/make-icon.ps1` | Draws the 256 px dock glyph and writes `Resources\RightDock.ico` with seven PNG-compressed sizes (16–256) |
| `tools/measure.ps1` | Samples working set, private bytes, CPU seconds/percent, handles and threads over a fixed window |
| `tools/stress-ui.ps1` | Invokes every module button twice per cycle, then reports private/working set, handle, GDI and USER deltas |

The installer is Inno Setup 6 (`installer/RightDock.iss`): per-user install under
`{autopf}\RightDock`, x64, Windows 10 1809+, Start Menu and optional desktop icons,
`AppMutex=Local\RightDock.SingleInstance`, and the same framework-dependent files as the
portable ZIP. The .NET 5 Desktop Runtime is a documented prerequisite; no installer is
included in source control because `dist/` is generated.

---

## 11. Productivity modules

The master specification evolves RightDock into a productivity and life-management
system while preserving everything above. **Phase 1 (core infrastructure) is implemented
in 1.3.0, Phase 2 (Focus, Tasks, Reminders, Notes) in 1.4.0, Phase 3 (Habits,
Medication, Health & Lifestyle) in 1.5.0, Phase 4 (Dashboard, Notes manager,
Calendar/planner, Statistics) in 1.6.0, Phase 5 (Quick Tools and Learning) in
1.7.0, Phase 6 (hardening, data tools and release assets) in 1.8.0, and Phase 7
(bell sounds for focus and reminders) in 1.9.0**; the
launcher, AppBar, auto-hide, icons and settings keep their contracts.

### 11.1 Layering

```
Presentation      MainWindow (dock), PopupHost, ModulePopupWindow, SettingsWindow,
                  module views (XAML, added per module)
Application       ModuleManager, Scheduler, NotificationService, SoundService
Domain            Focus, Tasks, Reminders, Habits, Medication, Health, Notes,
                  Learning, Statistics (pure C# models + rules, Phase 2+)
Infrastructure    Database, ModuleStateStore, AppBarDock, PinStore, SettingsStore,
                  StartupRegistration, IconRepository, Log
```

Business logic and data access stay out of code-behind; module views use the services
supplied through `ModuleContext`.

### 11.2 Module contract (implemented)

Every module is an in-process C# component extending `ProductivityModule` and registered
with `ModuleManager`:

| Member | Purpose |
| --- | --- |
| `Id`, `Name`, `Description` | Identity and settings text |
| `IconKey` | Reuses the existing icon catalog (`lucide:*`) |
| `ShowInDockByDefault` | Initial dock visibility for a new module |
| `Initialize(ModuleContext)` | Receives settings, database, scheduler, notifications, logging |
| `CreateCompactView()` | 320 DIP popup shown beside the dock (required) |
| `CreateExpandedView()` | 980×680 management view (optional) |
| `CreateSettingsView()` | Reserved for module-specific settings pages |
| `Activate()` / `Deactivate()` | Lazy lifecycle; resources released on disable, data retained |

`ModuleManager` owns registration (duplicate-id guard), persisted `ModuleState`,
enable/disable, dock visibility, ordering, and popup lifecycle. Enabled + visible modules
appear as 38×38 icon buttons between pins and the Add/Settings/Close stack; each button
toggles its compact popup and has a context menu with **Open** and **Hide from dock**.
Disabling closes popups and calls `Deactivate()` but never deletes data.

### 11.3 Popup hosting (implemented)

`PopupHost` keeps one compact and one expanded window per module and raises
`OpenCountChanged` so the dock pauses auto-hide while any popup is open.
`ModulePopupWindow` is the shared borderless shell (header icon, title, close button
named "Close popup" for accessibility). Compact windows use `SizeToContent` at 320 DIP
and are placed to the left of their dock button with DPI-correct conversion; expanded
windows are 980×680 and centered in the work area. Esc closes. Clicking outside closes
compact windows, but a deactivation caused by clicking the owning dock button is ignored
so the click can toggle the popup closed; expanded windows stay open until closed
explicitly. A closing guard prevents nested `Close()` calls from crashing.

### 11.4 Data layer (implemented)

One SQLite database (`%APPDATA%\RightDock\rightdock.db`) behind `Database`:
parameterized commands, one serialized connection, WAL, foreign keys, versioned
transactions, UTC timestamps, and indices on date/user-facing keys. Version 1 creates all
productivity tables listed in §8 plus `AppSettings` and `ModuleSettings`. Module enable,
visibility, sort order and JSON config persist through `ModuleStateStore`.

### 11.5 Scheduling (implemented)

`Scheduler` is a single keyed timestamp scheduler: `Schedule(key, dueUtc, callback,
interval?)`, `Cancel`, `IsScheduled`, and `Reconcile`. It arms one `DispatcherTimer` for
the earliest due item (50 ms floor, 1 h ceiling so clock/resume changes are re-evaluated),
supports one-shot and fixed-interval items, serializes access, and isolates callback
exceptions via logging + `CallbackFailed`. Modules re-register pending work during
`Activate()`, which provides restart reconciliation.

### 11.6 Notifications and sounds (implemented)

`NotificationService.Show(title, message, onClick?)` respects the master
`NotificationsEnabled` setting and posts a native in-app `ToastWindow` card in the
bottom-right corner (topmost, no-activate tool window, up to four stacked cards, 6 s
auto-dismiss, click runs the action and fades the card). In-app cards replace shell
tray/balloon notifications, which are frequently suppressed by Focus Assist/DND and
cannot be verified reliably. Settings → Notifications offers a live test button.

`SoundService` rings an audible bell next to the card. `BellSynth` synthesizes ten bell
timbres (Classic Bell, Soft Chime, Glass Bell, Marimba, Temple Bowl, Digital Chime, Harp
Pluck, Music Box, Deep Resonant, Bright Bell) as decaying sine-partial mixes and renders
each to a 44.1 kHz PCM16 mono WAV in memory — no audio assets, codecs or downloads.
`SoundService` caches one `SoundPlayer` per bell key, reminds with one ring
(`PlayReminder`), celebrates a finished focus session with three rings 850 ms apart
(`PlayFocusComplete`), and cancels an in-flight sequence when a new one starts or the
service is disposed. `PreviewFocus`/`PreviewReminder` bypass the notifications toggle so
the Settings pickers stay testable; live alert rings respect it. Selected keys persist in
`settings.json` (`FocusSound`, `ReminderSound`) and are validated on load with defaults
`classic` (focus) and `soft` (reminder).

### 11.7 Phase 2 modules (implemented in 1.4.0)

All four modules follow the same pattern: a `ProductivityModule` subclass owns a store
and an event (`Changed`), and exposes snapshot properties/commands to two XAML
`UserControl` views. Views subscribe on `Loaded`, unsubscribe on `Unloaded`, guard
programmatic updates with a `_syncing` flag, and rebuild lists only when a view key
(count + first id + timestamp) changes. All UI uses `Modules/ModuleTheme.xaml` and
`ModuleUtil` for parsing/formatting.

| Module | Dock icon | Compact | Expanded | Persistence |
| --- | --- | --- | --- | --- |
| Focus | `lucide:timer` | Daily totals/session count, 15/25/45/60 presets, start/pause/resume/reset, recent sessions | 7-day chart, session history, task linking | `FocusSessions` |
| Tasks | `lucide:clipboard-list` | Quick add (Enter), today's tasks, complete toggle | Views Today/Upcoming/Completed/All, search, full editor (due, category, project, priority, monthly/daily/weekly recurrence, notes) | `Tasks` |
| Reminders | `lucide:bell` | Quick add (+1 h), complete, snooze menu | Active/History, one-time/daily/weekly/custom-N-days recurrence, snooze, notes | `Reminders` |
| Notes | `lucide:notebook-pen` | Multiline capture (Ctrl+Enter), recent notes | Search, category filter chips, pin/unpin, archive/restore, convert to task/reminder | `Notes` |

Key behaviors:

- **Focus** runs on one shared 1 s `DispatcherTimer`, only while active. Elapsed time is
  derived from wall-clock segments, so pausing accumulates accurately; starting inserts a
  `FocusSessions` row and completion finalizes planned/actual seconds plus `IsCompleted`.
  `Deactivate()` finalizes an in-flight session as not completed; stale sessions are
  finalized on activation so a crash/restart cannot leave an open timer. Preset and linked
  task are locked while running. Completion posts a toast that opens the expanded view.
- **Tasks** complete-and-reschedule: when a recurring task is completed, the row is marked
  completed and a new occurrence is inserted from the due date, so history is preserved.
  Due dates are parsed from human-friendly input (`14:30`, `11/20 09:00`,
  `2026-11-20 09:00`); a time-only value in the past rolls to tomorrow; date-only defaults
  to 17:00. Search is a case-insensitive `LIKE` over title/notes/category/project.
- **Reminders** registers one scheduler key per active reminder
  (`reminder:<id>:<due-ticks>`) on `Activate()` and after every mutation, and cancels all
  keys on `Deactivate()`. Firing shows a toast with the reminder title/notes and opens the
  expanded view on click; recurring reminders only create their next occurrence on
  completion (and snooze shifts only the current occurrence). Overdue reminders fire once
  ~1 s after startup. Custom recurrence is stored as `custom:<days>`.
- **Notes** supports body-only capture (title derived from the first line), pinning,
  search, category filtering, and archive/restore (soft hide via `IsArchived`, the note
  stays in the database and can be restored from the Archived view). One-way conversion:
  "Create task" inserts a `Tasks` row (no due date, normal priority) and "Create
  reminder" inserts a `Reminders` row due in one hour, then calls
  `ModuleContext.RefreshModule` so the other module refreshes and reschedules.

Cross-module commands use the two `ModuleContext` callbacks: `OpenExpanded(moduleId)`
(note/reminder/task row → dashboard, notification click) and `RefreshModule(moduleId)`
(conversions so the target module reloads immediately).

### 11.8 Phase 3 modules (implemented in 1.5.0)

The personal-tracking modules reuse the Phase 2 contract, views, theme and scheduler.

| Module | Dock icon | Compact | Expanded | Persistence |
| --- | --- | --- | --- | --- |
| Habits | `lucide:target` | Done today, best streak, one-click check rows, `+` step for counter/duration | Habit list, boolean/counter/duration editor with target/unit, armed delete, LAST 7 DAYS dot matrix with streaks | `Habits`, `HabitLogs` |
| Medication | `lucide:pill` | Taken today, next dose, today's doses with Take/Skip | Today / Last 7 days views, Take/Snooze/Skip, multi-time schedule editor, medication list | `Medications`, `MedicationSchedules`, `MedicationLogs` |
| Health & Lifestyle | `lucide:heart-pulse` | Three rings (water/activity/sleep), quick log, latest weight | Rings, quick log for seven metrics, weight card, 7-day bars/averages table | `HealthLogs` |

Key behaviors:

- **Habits** seeds ten habits only when the table is empty. `ToggleToday` completes to the
  target or clears the day; `AddDelta` steps duration by 5 minutes and other counters by
  1, capped at the target. `HabitStore` loads a 400-day log window, anchors the current
  streak to today or yesterday, computes the best streak, and upserts on
  `(HabitId, LogDate)`. The week matrix renders the last seven local dates, oldest to
  today.
- **Medication** keeps medications, schedules and intake logs separate. `EnsureDoses`
  backfills pending logs for the last six days (never before the medication was created)
  and `MarkMissed` moves pending/snoozed doses older than two hours to `missed`.
  Actionable doses are pending or snoozed; taken/skipped/missed rows are read-only.
  Each actionable dose registers `med:dose:<logId>:<ticks>` (snoozed at
  `SnoozeUntilUtc`), a `med:rollover` task materializes the next day at 00:00:02, and
  firing shows `Medication · <name>` with the dosage and scheduled time. Dosage is free
  text; the module gives no dosage advice.
- **Health & Lifestyle** stores eight fixed metrics with targets and steps and upserts on
  `(LogDate, MetricType)`; decrementing to zero deletes the day's value. `RingView` draws
  a frozen ellipse track and a `StreamGeometry` arc (full `EllipseGeometry` at 100 %),
  with rounded caps, centered value/label and a value/target tooltip. Averages are
  computed over logged days in the 7-day window.

### 11.9 Phase 4 modules (implemented in 1.6.0)

Phase 4 adds read-oriented planning and insight modules on top of the same contract.
They own no new tables: every widget and bucket is aggregated from the existing stores.

| Module | Dock icon | Compact | Expanded | Data source |
| --- | --- | --- | --- | --- |
| Dashboard | `lucide:layout-dashboard` | DAILY OVERVIEW: focus minutes/sessions, tasks due, habit progress, medication, next up, health snapshot | Today at a glance, quick actions, six widgets (Focus 7-day bars, Tasks, Habits, Medication, Health, Reminders & Notes) | All stores, read-only |
| Calendar | `lucide:calendar-days` | Today's agenda (up to 5), next 7 days, Plan today | Monday-start 6-week month grid with per-day entry counts and titles, selected-day agenda, quick add task/reminder | Tasks, Reminders, medication occurrences, FocusSessions |
| Statistics | `lucide:chart-column` | 2×2 totals (focus, tasks, habit rate, medication adherence) + mini chart | Daily/Weekly/Monthly chips, four summary cards, metric chart, breakdown table | FocusSessions, Tasks, HabitLogs, MedicationLogs, HealthLogs |

Key behaviors:

- **Dashboard** builds one `DashboardSnapshot` per refresh (focus today plus seven daily
  bars, task due/overdue/completed today, habit done/total and best streak, medication
  taken/planned and next actionable dose, active/overdue reminders, note/pin/archive
  counts, health today values and latest weight). Widgets and quick actions navigate via
  `ModuleContext.OpenExpanded`, so editing stays in the owning module. A refresh timer
  exists only while a view is loaded (60 s compact, 30 s expanded).
- **Calendar** renders a fixed 42-cell Monday-start grid for the visible month. Weekends
  in adjacent months are dimmed; a day cell shows its entry count and up to two titles
  plus "+N more". Entries aggregate tasks by due date, reminders by effective due
  (`COALESCE(SnoozeUntilUtc, DueUtc)`), planned medication occurrences from schedules
  (existing log status included), and ended focus sessions. Quick add validates a title
  and `h:mm` time (default 09:00), inserts a task or reminder through the owning store,
  refreshes that module, and selects the edited day. Done entries are struck through and
  overdue entries are shown in red.
- **Statistics** buckets the last 7 days, 8 ISO weeks, or 6 calendar months. Focus sums
  ended-session minutes, tasks count `CompletedUtc`, habit rate is completed check-ins ÷
  (active habits × days in bucket), medication adherence is taken ÷ (taken + missed +
  skipped), and health shows day-range averages. `StatsChart` builds the shared bar chart
  (fixed 100 % scale for rate metrics) with value labels and tooltips; the table lists
  every bucket with focus/tasks/habits/medication values. Granularity and metric
  selections persist for the session and the view refreshes on a 60 s timer while loaded.

### 11.10 Phase 5 modules (implemented in 1.7.0)

Phase 5 adds a multi-tool convenience module and a learning tracker. Quick Tools is
deliberately limited to **exactly three tools**; no stopwatch, calculator, clipboard
manager, file manager or journal was added.

| Module | Dock icon | Compact | Expanded | Persistence |
| --- | --- | --- | --- | --- |
| Quick Tools | `lucide:sparkles` | Quick note capture (Ctrl+Enter), breathing card with pattern/cycle presets and countdown, screen-break toggle with interval chips and "Remind now" | Note capture + recent notes, animated breathing circle with four patterns and session history, screen-break interval editor (1–240 min), recent breathing sessions | `Notes` (category Quick), `BreathingSessions`, `ModuleSettings.ConfigJson` |
| Learning | `lucide:graduation-cap` | Active goal count, this-week minutes, up to four active goals with progress bars | Goal list with search/status/category filters, goal editor (category, unit, target, progress, due date, description), log-session form, recent sessions, complete/reopen, armed delete | `LearningGoals`, `LearningSessions` |

Key behaviors:

- **Quick note capture** inserts a note into the Notes module with category `Quick`
  through `NoteStore`, then calls `ModuleContext.RefreshModule("notes")` so the Notes
  module and Dashboard reload immediately. No separate note store exists.
- **Breathing** offers four patterns: Box (4-4-4-4), 4-7-8, Calm (4 in · 6 out) and
  Coherent (5 in · 5 out). One 1 s timer runs only during a session; each phase advances
  the inhale/hold/exhale/hold sequence, and the expanded view animates a circle with a
  0.9 s scale animation per tick (0.74 ⇄ 1.0). Stop saves a partial session when at
  least one cycle completed; completing the selected cycle count saves the full session.
  Sessions record pattern key/name, cycles, duration and UTC start/end in
  `BreathingSessions`, so they stay separate from Focus statistics.
- **Screen-break reminder** persists `ScreenBreakEnabled`, `ScreenBreakMinutes` (1–240,
  clamped), `LastBreakDate`, `BreaksToday` and the last pattern in
  `ModuleSettings.ConfigJson`. When enabled it registers the fixed-interval scheduler key
  `quicktools:screenbreak`; firing shows the in-app toast "Screen break" with the 20-20-20
  guidance and opens the expanded view on click. "Remind now" fires immediately.
  Disabling, closing the module or `Deactivate()` cancels the scheduler key.
- **Learning goals** support categories Course/Reading/Skill/Other and units
  minutes/hours/pages/chapters/lessons/sessions. Goals store target and progress values,
  an optional due date (`yyyy-MM-dd`) and a completed flag. The editor validates title
  and target; delete is armed (second click confirms) and deleting a goal keeps its
  sessions with `GoalId` set to NULL.
- **Learning sessions** log minutes and optional notes. For goals measured in minutes or
  hours, the logged minutes are added automatically to the goal's progress (`hours` is
  converted, rounded to two decimals); other units advance only via manual progress.
  Search is a case-insensitive `LIKE` over title/description/category; status filters are
  All/Active/Completed; category chips are derived from the goal data. Week statistics
  start Monday and count sessions in the current week.

### 11.11 Data tools and hardening (implemented in 1.8.0)

Phase 6 adds no new productivity module; it hardens the platform and exposes data
maintenance in Settings.

- **Database diagnostics (`Database.cs`)** — `SchemaVersion` (`PRAGMA user_version`),
  `SizeBytes` (database + WAL + SHM), `IntegrityCheck()` (`PRAGMA integrity_check`),
  `CountRows(table)` over an explicit whitelist (`KnownTables`) and `BackupTo(path)` via
  `SqliteConnection.BackupDatabase`, which also switches the copy from WAL to DELETE mode.
- **Data & Backup tab** (`SettingsWindow`) — shows the database path, schema, size and row
  total; "Open data folder", "Run integrity check", "Create backup…", "Restore backup…",
  "Copy diagnostics". Status lines turn green on success and red on failure. The About row
  reads `RightDock <version> · .NET 5 · schema v3`.
- **Restart flow** — restore stages the archive, then `Process.Start(exe, "--restart")`
  and `Application.Shutdown()`. The new process waits on the instance mutex and applies
  the staged restore before opening SQLite, so no module ever sees a replaced database.
- **OS resilience** — `WM_POWERBROADCAST` resume and `WM_TIMECHANGE` call
  `Scheduler.Reconcile()`; resume and `WM_DISPLAYCHANGE` call `RefreshGeometry()`, which
  re-reads the monitor and re-reserves the AppBar. This covers sleep/resume, clock
  changes, monitor hot-plug and resolution changes on the primary display.
- **Release assets** — deterministic `RightDock.ico`, portable ZIP, Inno Setup script,
  third-party notices and version/company metadata in the executable.

### 11.12 Bell sounds (implemented in 1.9.0)

Phase 7 adds audible alerts without shipping audio files.

- **`Services/BellSynth.cs`** — ten built-in voices defined by fundamental, duration,
  decay, attack, partial ratios/gains and optional beating: Classic Bell, Soft Chime,
  Glass Bell, Marimba, Temple Bowl, Digital Chime, Harp Pluck, Music Box, Deep Resonant and
  Bright Bell. `CreateWav(key)` renders a decaying partial mix, applies attack/release
  fades, normalizes to 0.55 peak, and writes a 44.1 kHz PCM16 mono WAV into memory.
- **`Services/SoundService.cs`** — owns cached `SoundPlayer` instances (one per bell),
  plays event alerts and previews, sequences three rings 850 ms apart for a finished focus
  session, rings once for a due reminder, cancels an in-flight sequence when a new one
  starts, and disposes streams/players on shutdown. Focus completion and reminders call
  `PlayFocusComplete()`/`PlayReminder()`; both respect the notifications master toggle.
  `PreviewFocus(key)`/`PreviewReminder(key)` bypass it so the Settings pickers can always
  be auditioned.
- **Settings** — the Notifications tab gains a SOUNDS section with a ten-item picker and
  Preview button for each alert, plus a status line. Choices persist immediately in
  `settings.json` as `FocusSound`/`ReminderSound`, are validated against the catalog on
  load, and default to `classic`/`soft`.

### 11.13 Settings evolution

The settings window covers General, Appearance, **Modules** (enable/disable, hide/show in
dock), **Notifications** (master toggle, test card, bell pickers and previews), **Data**
(diagnostics, backup, restore, copy diagnostics) and **Startup**. `settings.json` keys
stay compatible and default safely; the window is 560×560 with six tabs.

### 11.14 Module phases

| Phase | Scope | Status |
| --- | --- | --- |
| 1 | ModuleManager, PopupHost, SQLite service, settings integration, notifications, scheduler | ✔ Done ✔ Tested (1.3.0) |
| 2 | Focus, Tasks, Reminders, Quick Ideas/Notes | ✔ Done ✔ Tested (1.4.0) |
| 3 | Habits, Medication, Health & Lifestyle | ✔ Done ✔ Tested (1.5.0) |
| 4 | Dashboard, Daily Overview, Notes manager, Calendar/planner, Statistics | ✔ Done ✔ Tested (1.6.0) |
| 5 | Breathing, Screen-break reminder, Learning tracker (Quick Tools stays at exactly three tools) | ✔ Done ✔ Tested (1.7.0) |
| 6 | Profiling, leak checks, migration/backup/restore, startup/sleep/display tests, installer readiness | ✔ Done ✔ Tested (1.8.0) |
| 7 | Ten synthesized bell sounds, triple focus ring, single reminder ring, sound pickers | ✔ Done ✔ Tested (1.9.0) |

### 11.15 Invariants for future work

1. The dock remains ~50 px wide, topmost, and reserves work area via AppBar.
2. Existing pins, icons, colors and settings keep working without migration steps.
3. One process, no WebView/Electron, no per-module executables, no mandatory network.
4. Icons reuse `IconRepository`; popups reuse the established dark visual language.
5. Module data survives disabling a module.
6. No feature ships with placeholder buttons or untested claims.

---

## 12. Build and run

```bash
cd "D:\Quder Test\RightDock"
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File tools\publish.ps1
publish\RightDock.exe
```

`tools\publish.ps1` writes `publish\` and the portable ZIP
`dist\RightDock-1.9.0-win-x64.zip`; add `-Installer` to compile
`dist\RightDock-1.9.0-setup.exe` with Inno Setup 6 (install it from
https://jrsoftware.org/isinfo.php first). Stop the running instance before republishing
(the exe/dll are locked while running). `publish/` must include the SQLite native
runtimes (`runtimes/win-x64/native/e_sqlite3.dll`); `dotnet publish` copies them
automatically.

User-facing behavior:

- Hover the right screen edge to reveal; the dock hides 650 ms after the cursor leaves.
- Left-click a pin to launch; right-click for **Change icon…** / **Remove from dock**.
- **Add** opens a multi-select file picker (`exe`, `lnk`, `bat`, `cmd`, `msc`, `appref-ms`).
- Module icons appear above the Add/Settings/Close stack; left-click opens the compact
  popup (click again to close), right-click offers **Open** / **Hide from dock**.
- **Settings** opens the six-tab window; changes apply instantly. **Data** creates or
  restores a backup, runs an integrity check, opens the data folder and copies
  diagnostics; restore restarts the app automatically.
- Notifications appear as dark cards in the bottom-right corner.
- **Close** exits and releases the reserved space.
