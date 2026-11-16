# RightDock — Project Architecture

> Current release: **1.2.0** (application launcher dock)
> Target direction: native Windows **Productivity & Life Management Dock** (see §11)

RightDock is a single-process WPF application that renders a 50 px taskbar-like dock on
the right edge of the primary monitor. It reserves desktop work area through the Windows
AppBar protocol, launches pinned applications, and supports a 181-glyph outlined icon
system, auto-hide with edge reveal, and a three-tab settings popup.

All functionality so far is implemented in one WPF process. No databases, services,
WebView, or third-party UI frameworks are used.

---

## 1. Technology stack

| Item | Choice |
| --- | --- |
| Language / runtime | C# 9, .NET 5 (`net5.0-windows`) |
| UI framework | WPF (`UseWPF`), plus `UseWindowsForms` only for the native `ColorDialog` |
| Windows integration | Win32 P/Invoke (`shell32`, `user32`, `dwmapi`), registry |
| Persistence | `System.Text.Json` files under `%APPDATA%\RightDock` |
| Theming | Hand-written XAML styles (no theme libraries) |
| Icons | Lucide (ISC) path data committed as `IconCatalog.cs`; optional user SVGs |
| Packaging | `dotnet publish` framework-dependent output in `publish/` |
| Min OS | Windows 10 1809+ (manifest targets Windows 10/11, PerMonitorV2 DPI) |

Dependencies outside the .NET/WPF runtime: **none**. `tools/` contains a Node.js script
used only at development time to regenerate the icon catalog.

---

## 2. Repository layout

```
RightDock/
├─ App.xaml                  Application resources: DockButton, dark ToolTip, ContextMenu, MenuItem
├─ App.xaml.cs               Single-instance mutex + main window startup
├─ MainWindow.xaml           ｜ Dock shell: 50 px column, pin list, Add / Settings / Close
├─ MainWindow.xaml.cs        ｜ AppBar reservation, auto-hide state machine, pin actions
├─ AppBarDock.cs             SHAppBarMessage wrapper (ABM_NEW / QUERYPOS / SETPOS / REMOVE)
├─ NativeMethods.cs          Win32 structs and P/Invoke declarations
├─ PinStore.cs               Pin model + pins.json load/save
├─ AppSettings.cs            AppSettings model + settings.json load/save
├─ StartupRegistration.cs    HKCU Run-key registration
├─ Icons.cs                  Shell file icon extraction (SHGetFileInfo)
├─ IconCatalog.cs            Auto-generated 181 Lucide entries (key, name, category, path)
├─ IconRepository.cs         Built-in + user icon resolution, geometry cache, rendering
├─ IconPickerWindow.xaml/.cs Icon browser popup ("Choose icon")
├─ SettingsWindow.xaml/.cs   Three-tab settings popup
├─ app.manifest              asInvoker, Win10/11 supportedOS, PerMonitorV2
├─ RightDock.csproj          Build configuration
├─ tools/
│  ├─ gen-icons.js           Generates IconCatalog.cs from Lucide data
│  └─ lucide-icon-nodes.json Lucide source data (icon name → SVG nodes)
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
        └─ new MainWindow().Show()
             ├─ constructor: load settings + pins, build pin buttons, create timers
             ├─ OnSourceInitialized: tool-window + no-activate styles, no DWM rounding,
             │                       AppBar callback registration, AppBarDock instance
             └─ OnLoaded: reserve AppBar, place window, start 110 ms hover timer
```

Shutdown is user-driven only: the Close button calls `Window.Close()`.
`OnClosing` sets `_closing`, stops timers, and calls `ABM_REMOVE` so the reserved work
area is released immediately.

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
popup is open (`_modalDepth > 0`), or geometry is unknown.

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
| Type | Segoe UI Variable Text / Segoe UI; Fluent Icons for chrome glyphs |
| Motion | 170 ms slide, no popup animations |

Dock buttons are 38×38 with 26 px icons. The bottom stack is fixed:
**Add (E710) → Settings (E713) → Close (E711)**, each with a left-placed tooltip.

Settings popup (`SettingsWindow`, 440×470) uses a segmented tab strip with three pages:

1. **General** — auto-hide toggle + explanation of the 50 px reservation / edge reveal.
2. **Appearance** — 10 preset swatches + rainbow "custom" swatch, current hex label,
   native Windows color dialog (`FullOpen`).
3. **Startup** — run-at-Windows-startup toggle, resolved exe path, inline error text if
   the registry write fails (toggle reverts on failure).

Settings are applied live through events (`AutoHideChanged`, `IconColorChanged`) and
persisted immediately; there is no Save/Apply step.

---

## 8. Persistence and OS integration

```
%APPDATA%\RightDock\
├─ pins.json       [ { "Name": …, "Path": …, "Icon": "lucide:…" } ]
├─ settings.json   { "AutoHide": true, "IconColor": "#A9A9A9" }
└─ icons\          optional user *.svg icons (created on demand)
```

| Integration | Mechanism |
| --- | --- |
| Single instance | Named mutex `Local\RightDock.SingleInstance` |
| Start with Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\RightDock` = quoted exe path |
| Desktop reservation | AppBar protocol (see §4) |
| File launch | `Process.Start(UseShellExecute=true)` with the exe's folder as working directory |
| Icon extraction | `SHGetFileInfo` with `.exe` fallback for missing targets |
| DPI | PerMonitorV2 manifest + `WM_DPICHANGED` handling |
| Fullscreen detection | `ABN_FULLSCREENAPP` from the shell |

Storage failures are non-fatal by design: corrupt or missing JSON falls back to
defaults, and writes are wrapped so a locked file cannot take down the dock.
Pins are never deleted by settings changes; startup registration is the only registry
state the app owns.

---

## 9. Threading and resource policy

- UI thread only; no worker threads or background services yet.
- Two `DispatcherTimer`s: the 110 ms hover poll while running, and a 16 ms animation
  timer that exists only during a 170 ms slide.
- All Win32 calls are non-blocking; `SHGetFileInfo` is the only synchronous shell call.
- Brush and geometry objects are frozen and reused; pin visuals are rebuilt only when the
  pin list or icon color changes.
- Measured baseline (2026-11-16, Windows 11, 1920×1200 @100 %, idle 20 min):
  **~76 MB working set, ~58 MB private, ≈0.2 % average CPU** for one process.

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

## 11. Target architecture — productivity modules (not yet implemented)

The master specification evolves RightDock into a productivity and life-management
system while preserving everything above. The planned structure is additive; the
launcher, AppBar, auto-hide, icons and settings keep their current contracts.

### 11.1 Layering

```
Presentation      MainWindow (dock), PopupHost, DashboardWindow, module views (XAML)
Application       ModuleManager, Scheduler, NotificationService, QuickCapture
Domain            Focus, Tasks, Reminders, Habits, Medication, Health, Notes,
                  Learning, Statistics (pure C# models + rules)
Infrastructure    AppBarDock, PinStore, SettingsStore, SqliteDataService,
                  StartupRegistration, icon repository
```

Business logic and data access stay out of code-behind; module views bind to
view-models/services supplied by `ModuleManager`.

### 11.2 Module contract

Every module is an in-process C# component registered with `ModuleManager`:

| Member | Purpose |
| --- | --- |
| `Id`, `Name`, `Description` | Identity and settings text |
| `IconKey` | Reuses the existing icon catalog (`lucide:*`) |
| `IsEnabled`, `IsVisibleByDefault` | Dock visibility and lifecycle |
| `CreateCompactView()` | 280–360 px popup shown beside the dock |
| `CreateExpandedView()` | Full management view |
| `CreateSettingsView()` | Module-specific settings page |
| `Initialize()` / `Activate()` / `Deactivate()` | Lazy lifecycle; resources released on deactivate |

`ModuleManager` owns registration, persisted module config, enable/disable (data is
retained), dock ordering, and popup lifecycle. `PopupHost` guarantees a single instance
per module, positions popups to the left of the dock, closes on Esc/outside click, and
participates in `_modalDepth` so auto-hide does not fight open popups.

### 11.3 Data layer

One SQLite database (`%APPDATA%\RightDock\rightdock.db`) behind `SqliteDataService`:
parameterized queries, explicit transactions, versioned migrations, UTC timestamps,
indices on date/user-facing keys. Planned tables: `Tasks`, `TaskCategories`,
`Projects`, `Reminders`, `FocusSessions`, `Habits`, `HabitLogs`, `Medications`,
`MedicationSchedules`, `MedicationLogs`, `HealthLogs`, `Notes`, `LearningGoals`,
`LearningSessions`, `AppSettings`, `ModuleSettings`. Backup/restore/export/import live
in the Data & Backup settings page.

`pins.json` and `settings.json` remain the launcher's storage; the database is used by
productivity modules only. If the existing JSON approach proves sufficient for a module,
it is reused rather than introducing parallel stores.

### 11.4 Scheduling and notifications

A single centralized `Scheduler` computes the next due timestamp and arms one timer
(timestamp-based, sleep/resume aware, restart reconciliation). Reminders, habits,
medication, screen-break prompts and focus completion all subscribe to it — no
per-reminder timers. `NotificationService` wraps Windows toast/balloon notifications,
sound preferences, snooze and quiet hours.

### 11.5 Settings evolution

`SettingsWindow` grows the categories from the specification: General, Dock (position,
width, auto-hide, always-on-top, icon size/arrangement), Appearance (dark/light/auto,
accents, popup dimensions, animation), Apps & Shortcuts, Modules, Notifications, Data &
Backup, Startup, About. Existing `settings.json` keys keep working; new keys default
safely.

### 11.6 Module phases

| Phase | Scope |
| --- | --- |
| 1 | ModuleManager, PopupHost, SQLite service, settings integration, notifications, scheduler |
| 2 | Focus, Tasks, Reminders, Quick Ideas/Notes |
| 3 | Habits, Medication, Health & Lifestyle |
| 4 | Dashboard, Daily Overview, Notes manager, Calendar/planner, Statistics |
| 5 | Breathing, Screen-break reminder, Learning tracker (Quick Tools stays at exactly three tools) |
| 6 | Profiling, leak checks, migration/backup tests, startup/sleep/multi-monitor tests, installer readiness |

### 11.7 Invariants for future work

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

User-facing behavior:

- Hover the right screen edge to reveal; the dock hides 650 ms after the cursor leaves.
- Left-click a pin to launch; right-click for **Change icon…** / **Remove from dock**.
- **Add** opens a multi-select file picker (`exe`, `lnk`, `bat`, `cmd`, `msc`, `appref-ms`).
- **Settings** opens the three-tab popup; changes apply instantly.
- **Close** exits and releases the reserved space.
