# RightDock — Project Architecture

> Current release: **1.5.0** (launcher + Phase 1 infrastructure + Phase 2 productivity
> modules + Phase 3 personal tracking modules)
> Target direction: native Windows **Productivity & Life Management Dock** (see §11)

RightDock is a single-process WPF application that renders a 50 px taskbar-like dock on
the right edge of the primary monitor. It reserves desktop work area through the Windows
AppBar protocol, launches pinned applications, and supports a 181-glyph outlined icon
system, auto-hide with edge reveal, a five-tab settings popup, and a productivity layer:
SQLite persistence, a module manager with compact/expanded popups, a centralized
scheduler, in-app notification cards, and the Focus, Tasks, Reminders, Notes, Habits,
Medication, and Health & Lifestyle modules.

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
├─ SettingsWindow.xaml/.cs   Five-tab settings window (General, Appearance, Modules, Notifications, Startup)
├─ Data/
│  ├─ Database.cs            SQLite connection, pragmas, versioned migrations, query helpers
│  ├─ ModuleStateStore.cs    ModuleSettings persistence (enabled/visible/sort/config)
│  ├─ FocusStore.cs          FocusSessions rows, recent activity, daily totals
│  ├─ TaskStore.cs           Tasks CRUD, views, search, recurrence-insert on completion
│  ├─ ReminderStore.cs       Reminders CRUD, snooze, completion, recurrence
│  └─ NoteStore.cs           Notes CRUD, search, pin, timestamps
├─ Modules/
│  ├─ IProductivityModule.cs Module contract + ProductivityModule base class
│  ├─ ModuleContext.cs       Services handed to modules + OpenExpanded/RefreshModule callbacks
│  ├─ ModuleManager.cs       Registration, lifecycle, dock entries, enable/disable/visibility
│  ├─ ModuleTheme.xaml       Shared dark styles: cards, inputs, chips, buttons, progress
│  ├─ ModuleUtil.cs          Shared formatting, parsing and recurrence helpers
│  ├─ PopupHost.cs           One compact + one expanded popup per module
│  ├─ ModulePopupWindow.xaml/.cs  Shared popup shell (title, icon, close, placement)
│  ├─ Focus/                 FocusModule + compact/expanded timer views
│  ├─ Tasks/                 TasksModule + compact/expanded list/editor views
│  ├─ Reminders/             RemindersModule (scheduler integration) + views
│  └─ Notes/                 NotesModule (capture + conversion) + views
├─ Services/
│  ├─ Log.cs                 Append-only log under %APPDATA%\RightDock\logs
│  ├─ Scheduler.cs           Central timestamp-based scheduler (one DispatcherTimer)
│  ├─ NotificationService.cs In-app notification coordinator (enabled flag, stacking)
│  └─ ToastWindow.cs         Borderless layered notification card
├─ app.manifest              asInvoker, Win10/11 supportedOS, PerMonitorV2
├─ RightDock.csproj          Build configuration
├─ tools/
│  ├─ gen-icons.js           Generates IconCatalog.cs from Lucide data
│  ├─ lucide-icon-nodes.json Lucide source data (icon name → SVG nodes)
│  ├─ ui.ps1                 UIA test helper: list/inspect/invoke/click/type/screenshot
│  ├─ watch-toast.ps1        Polls for notification cards and can click them
│  └─ close-dock.ps1         Graceful WM_CLOSE of the running dock
├─ PROJECT_ARCHITECTURE.md   This document
├─ IMPLEMENTATION_PROGRESS.md
└─ CHANGELOG.md
```

`bin/`, `obj/` and `publish/` are build outputs and are not versioned. User data lives
outside the repository (see §8).

---

## 3. Startup and lifecycle

```
RightDock.exe
   └─ App.OnStartup
        ├─ Mutex "Local\RightDock.SingleInstance" (second instance → Shutdown)
        ├─ new DockServices()
        │    ├─ settings.json → AppSettings
        │    ├─ Database.Initialize() (WAL, foreign keys, migrations)
        │    ├─ Scheduler, NotificationService, ModuleManager
        │    └─ Modules.Start() → Initialize/Activate each enabled module
        └─ new MainWindow(services).Show()
             ├─ constructor: build pin buttons + module buttons, create timers
             ├─ OnSourceInitialized: tool-window + no-activate styles, no DWM rounding,
             │                       AppBar callback registration, AppBarDock instance
             └─ OnLoaded: reserve AppBar, place window, start 110 ms hover timer
```

Shutdown is user-driven only: the Close button calls `Window.Close()`.
`OnClosing` sets `_closing`, stops timers, and calls `ABM_REMOVE` so the reserved work
area is released immediately. `App.OnExit` disposes `DockServices` (module lifecycle,
scheduler, notifications, database).

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

Settings (`SettingsWindow`, 500×520) uses a segmented tab strip with five pages:

1. **General** — auto-hide toggle + explanation of the 50 px reservation / edge reveal.
2. **Appearance** — 10 preset swatches + rainbow "custom" swatch, current hex label,
   native Windows color dialog (`FullOpen`).
3. **Modules** — one row per registered module with icon, name, description, an
   enable/disable toggle, and a **Hide from dock / Show in dock** link; the empty state
   reads "No modules are registered yet."
4. **Notifications** — master notifications toggle and a **Send test notification**
   button; the inline status reports success or "Notifications are disabled."
5. **Startup** — run-at-Windows-startup toggle, resolved exe path, inline error text if
   the registry write fails (toggle reverts on failure).

Settings are applied live through events (`AutoHideChanged`, `IconColorChanged`) or
immediate `ModuleManager` calls, and persisted immediately; there is no Save/Apply step.

---

## 8. Persistence and OS integration

```
%APPDATA%\RightDock\
├─ pins.json       [ { "Name": …, "Path": …, "Icon": "lucide:…" } ]
├─ settings.json   { "AutoHide": true, "IconColor": "#A9A9A9", "NotificationsEnabled": true }
├─ rightdock.db    SQLite database (WAL): module state + all productivity data
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
`(LogDate, MetricType)`.

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
| Fullscreen detection | `ABN_FULLSCREENAPP` from the shell |
| Notifications | In-app `ToastWindow` cards (no shell tray icon) |

Storage failures are non-fatal by design: corrupt or missing JSON falls back to
defaults, database errors are logged and degrade to in-memory module defaults, and
writes are wrapped so a locked file cannot take down the dock. Pins are never deleted by
settings changes; startup registration is the only registry state the app owns.

---

## 9. Threading and resource policy

- UI thread only; no worker threads or background services yet.
- `DispatcherTimer`s: the 110 ms hover poll while running, a 16 ms animation timer that
  exists only during a 170 ms slide, the scheduler's single timer armed to the earliest
  due timestamp (re-armed at most once per hour so sleep/resume is handled), and Focus's
  1 s tick that runs only while a session is active.
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
  seconds over 30 s** (Windows 11, 2560×1600 @125 %).

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

---

## 11. Productivity modules

The master specification evolves RightDock into a productivity and life-management
system while preserving everything above. **Phase 1 (core infrastructure) is implemented
in 1.3.0, Phase 2 (Focus, Tasks, Reminders, Notes) in 1.4.0, and Phase 3 (Habits,
Medication, Health & Lifestyle) in 1.5.0**; the launcher, AppBar, auto-hide, icons and
settings keep their contracts.

### 11.1 Layering

```
Presentation      MainWindow (dock), PopupHost, ModulePopupWindow, SettingsWindow,
                  module views (XAML, added per module)
Application       ModuleManager, Scheduler, NotificationService
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

### 11.6 Notifications (implemented)

`NotificationService.Show(title, message, onClick?)` respects the master
`NotificationsEnabled` setting and posts a native in-app `ToastWindow` card in the
bottom-right corner (topmost, no-activate tool window, up to four stacked cards, 6 s
auto-dismiss, click runs the action and fades the card). In-app cards replace shell
tray/balloon notifications, which are frequently suppressed by Focus Assist/DND and
cannot be verified reliably. Settings → Notifications offers a live test button.

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
| Notes | `lucide:notebook-pen` | Multiline capture (Ctrl+Enter), recent notes | Search, pin/unpin, category, convert to task/reminder | `Notes` |

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
  search, and one-way conversion: "Create task" inserts a `Tasks` row (no due date,
  normal priority) and "Create reminder" inserts a `Reminders` row due in one hour, then
  calls `ModuleContext.RefreshModule` so the other module refreshes and reschedules.

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

### 11.9 Settings evolution

The current settings window covers General, Appearance, **Modules** (enable/disable,
hide/show in dock) and **Notifications** (master toggle + test). Later phases add Dock,
Data & Backup, About, and per-module settings pages while keeping `settings.json` keys
compatible and defaulting safely.

### 11.10 Module phases

| Phase | Scope | Status |
| --- | --- | --- |
| 1 | ModuleManager, PopupHost, SQLite service, settings integration, notifications, scheduler | ✔ Done ✔ Tested (1.3.0) |
| 2 | Focus, Tasks, Reminders, Quick Ideas/Notes | ✔ Done ✔ Tested (1.4.0) |
| 3 | Habits, Medication, Health & Lifestyle | ✔ Done ✔ Tested (1.5.0) |
| 4 | Dashboard, Daily Overview, Notes manager, Calendar/planner, Statistics | ◻ Pending |
| 5 | Breathing, Screen-break reminder, Learning tracker (Quick Tools stays at exactly three tools) | ◻ Pending |
| 6 | Profiling, leak checks, migration/backup tests, startup/sleep/multi-monitor tests, installer readiness | ◻ Pending |

### 11.11 Invariants for future work

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
dotnet publish -c Release -r win-x64 --self-contained false -o publish
publish\RightDock.exe
```

Stop the running instance before republishing (the exe/dll are locked while running).
`publish/` must include the SQLite native runtimes (`runtimes/win-x64/native/e_sqlite3.dll`);
`dotnet publish` copies them automatically.

User-facing behavior:

- Hover the right screen edge to reveal; the dock hides 650 ms after the cursor leaves.
- Left-click a pin to launch; right-click for **Change icon…** / **Remove from dock**.
- **Add** opens a multi-select file picker (`exe`, `lnk`, `bat`, `cmd`, `msc`, `appref-ms`).
- Module icons appear above the Add/Settings/Close stack; left-click opens the compact
  popup (click again to close), right-click offers **Open** / **Hide from dock**.
- **Settings** opens the five-tab window; changes apply instantly.
- Notifications appear as dark cards in the bottom-right corner.
- **Close** exits and releases the reserved space.
