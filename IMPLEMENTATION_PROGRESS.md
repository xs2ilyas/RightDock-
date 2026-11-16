# RightDock — Implementation Progress

> Release **1.3.0** — launcher + Phase 1 productivity infrastructure complete.
> Next up: **Phase 2** (Focus, Tasks, Reminders, Notes).
> Last updated: 2026-11-16.

Status legend: **✔ Done** · **✔ Tested** (verified on a live Windows 11 session) ·
**◻ Pending** · **△ Partial**

---

## 1. Phase summary

| Phase | Scope | Status |
| --- | --- | --- |
| v1.0.0 | Dock shell, AppBar reservation, pins, Add/Close | ✔ Done ✔ Tested |
| v1.1.0 | Auto-hide + edge reveal, 181-icon line set, icon picker | ✔ Done ✔ Tested |
| v1.2.0 | Settings popup (auto-hide, icon color, startup) | ✔ Done ✔ Tested |
| v1.3.0 | Phase 1: Module manager, popup host, SQLite, scheduler, notifications, settings integration | ✔ Done ✔ Tested |
| Docs | Architecture, progress, changelog, GitHub repository | ✔ Done |
| Phase 2 | Focus, Tasks, Reminders, Quick Ideas / Notes | ◻ Pending |
| Phase 3 | Habits, Medication, Health & Lifestyle | ◻ Pending |
| Phase 4 | Dashboard, Daily Overview, Notes, Calendar, Statistics | ◻ Pending |
| Phase 5 | Breathing, Screen-break, Learning tracker | ◻ Pending |
| Phase 6 | Profiling, leak/migration/backup/multi-monitor testing | ◻ Pending |

---

## 2. v1.0.0 — Dock core (completed, tested)

- **Right-edge AppBar dock** — 50 DIP wide, full monitor height, dark `#1C1C1C` surface
  with a 1 px left border; reserves work area via `ABM_NEW/QUERYPOS/SETPOS` so maximized
  windows stop at the dock edge.
- **Always on top, non-activating tool window** — `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`,
  no taskbar button, no focus stealing, square corners via DWM.
- **Pin management** — Add (multi-select file picker for exe/lnk/bat/cmd/msc/appref-ms),
  left-click launch, right-click **Remove from dock**, duplicate-path guard, JSON
  persistence.
- **Close button** — exits the app and releases the reservation (`ABM_REMOVE`).
- **Single instance** — second launch exits immediately.
- **DPI/display resilience** — PerMonitorV2 manifest; `WM_DPICHANGED` and
  `WM_DISPLAYCHANGE` re-reserve and re-place the dock.
- **Icon fallback chain** — shell icon → first letter tile, so a missing file never
  leaves a blank button.

Tested on Windows 11 (2560×1600 @125 % and 1920×1200 @100 %): work area shrank by
exactly 50 px, maximized window respected the reserve, launching and removal worked,
close restored the work area, second instance exited.

---

## 3. v1.1.0 — Auto-hide and icon system (completed, tested)

### Auto-hide
- Hover polling at 110 ms; reveal zone = right 5 px of the monitor.
- 650 ms leave delay, 170 ms cubic ease-out slide, startup grace of 1600 ms.
- Hidden = fully offscreen **and** reservation released; reveal re-reserves before
  sliding in.
- 250 ms guard prevents `ABN_POSCHANGED` feedback loops.
- Fullscreen apps (`ABN_FULLSCREENAPP`) always hide the dock; it returns after exit.
- Menus/dialogs pause auto-hide through `_modalDepth`.

Tested: after slide-out the window sat at `left = monitor.Right` and the work area
returned to full width; edge hover re-reserved 50 px and slid the dock back; auto-hide
respects fullscreen; context menus could be used without the dock disappearing.

### Icon system
- **181 Lucide outline glyphs** across Health, Office, Time, Marketing, Plotter,
  Printing, Files, Design, Media, Security, Transport, Weather and Utility categories.
- 1.5 px round-stroked `Path` rendering at 26 px in the dock, uniform scaling.
- **Icon picker popup** (440×640): category sections, live search by name/category,
  "This app" reset row, current-selection highlight, Esc to close, draggable header.
- **User SVG icons** — drop `*.svg` into `%APPDATA%\RightDock\icons`; basic shapes
  (path/circle/ellipse/rect/line/polyline/polygon) parse into a geometry group.
- Geometry caching, frozen brushes/geometries, invalid icons degrade gracefully.

Tested: picker rendered all categories, search ("print") filtered correctly, custom SVG
beakers/sparkles appeared under "My icons", selections persisted in `pins.json`, and
assigned icons re-rendered after restart.

---

## 4. v1.2.0 — Settings popup (completed, tested)

Three-tab dark popup (440×470) opened from the gear button, positioned beside the dock:

1. **General** — auto-hide toggle with explanation of the 50 px release and edge reveal.
2. **Appearance** — 10 preset colors (`#A9A9A9` default, white, blue, teal, green,
   yellow, orange, red, pink, purple) + rainbow custom swatch opening the native Windows
   **ColorDialog** (`FullOpen`); current hex displayed; selected swatch ring + check.
3. **Startup** — run-at-Windows-startup toggle backed by
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\RightDock` (quoted exe path);
   registry errors revert the toggle and show inline red text.

Live application: dock icons recolor immediately (picker previews use the same color);
auto-hide changes take effect without restart; all settings persist in
`%APPDATA%\RightDock\settings.json`.

Tested: all 10 swatches applied; dock turned `#5AB0F2` and back to `#A9A9A9`; native
color dialog opened at the current color and cancel left it unchanged; auto-hide off
kept the dock visible and reserved after 3 s with the cursor away, on restored
hide+release; startup toggle wrote and removed the Run value; restarted the app with
`AutoHide:false` and custom `#123456` — dock started visible with navy icons and the
custom swatch selected; settings were then restored to defaults for the user.

---

## 5. v1.3.0 — Phase 1 core infrastructure (completed, tested)

### Services and persistence
- **`DockServices` composition root** — created in `App.OnStartup`, owns settings, the
  SQLite database, scheduler, notification service and module manager; disposed on exit.
- **SQLite (`Data/Database.cs`)** — `%APPDATA%\RightDock\rightdock.db`, WAL,
  `foreign_keys`, `busy_timeout`, versioned migrations via `PRAGMA user_version`.
  Version 1 creates all 16 productivity tables plus indices; schema verified at runtime.
- **`ModuleStateStore`** — loads/saves `IsEnabled`, `IsVisible`, `SortOrder` and
  `ConfigJson` per module; disabling never deletes data.
- **`Log`** — append-only UTC log in `%APPDATA%\RightDock\logs` with 1 MB rotation.

### Modules
- **`IProductivityModule` / `ProductivityModule`** — identity (`Id`, `Name`,
  `Description`, `IconKey`, `ShowInDockByDefault`), lifecycle (`Initialize`,
  `Activate`, `Deactivate`), and views (`CreateCompactView`, `CreateExpandedView`,
  `CreateSettingsView`).
- **`ModuleManager`** — registration with duplicate-id guard, lazy initialization on
  enable/open, `SetEnabled` (closes popups, deactivates, hides dock entry), `SetVisible`
  (keeps module running, removes/appends dock icon), ordered entries, `DockItemsChanged`.
- **Dock integration** — enabled and visible modules render as 38×38 icon buttons above
  the Add/Settings/Close stack; left-click toggles the compact popup; right-click offers
  **Open** and **Hide from dock**.

### Popup hosting
- **`PopupHost`** — one compact and one expanded window per module, `OpenCountChanged`
  feeds the dock's auto-hide pause, closes all on module disable/shutdown.
- **`ModulePopupWindow`** — shared borderless shell with header icon, title and a named
  **Close popup** button. Compact: `SizeToContent` at 320 DIP, placed left of its dock
  button with DPI-correct conversion. Expanded: 980×680 centered. Esc closes; outside
  click closes compact windows; clicking the owning dock button is detected and does not
  trigger the deactivation close, so it toggles correctly. Expanded views stay open on
  focus loss. Closing-state guard prevents nested `Close()` crashes.

### Scheduler and notifications
- **`Scheduler`** — keyed one-shot/interval tasks, single `DispatcherTimer` armed to the
  earliest due UTC timestamp, 50 ms floor / 1 h ceiling, serialized state, exception
  isolation, `Reconcile()` after startup.
- **`NotificationService` + `ToastWindow`** — master enable flag, in-app dark cards in the
  bottom-right corner (topmost, no-activate, up to 4 stacked, 6 s auto-dismiss, click
  runs an action then fades out). Chosen because shell balloon/toast notifications were
  suppressed in the test environment and cannot be verified reliably.

### Settings
- Window grows to 500×520 with **five tabs**: General, Appearance, **Modules** (per-module
  enable toggle and Hide/Show in dock link, empty state), **Notifications** (master
  toggle + test button with inline status), Startup.
- `settings.json` gains `NotificationsEnabled` (default `true`).

### Verified (temporary Smoke Test module, removed before release)
- Database schema v1 created; round-trip read/write of module config; log confirms
  `Database ready`.
- Scheduler fired a +3 s callback and produced a visible notification card; the card
  auto-dismissed and did not take focus.
- Compact popup: open, toggle-close by clicking the dock icon, outside-click close, Esc
  close, no crash on close; expanded 980×680 opened and stayed open after focus loss.
- Modules tab: disable removed the dock icon and persisted `IsEnabled=0`; re-enable
  restored it; **Hide from dock** kept the module enabled, label changed to **Show in
  dock**, `IsVisible=0`; show restored the icon. All states survived an app restart.
- Notifications tab: master off → test shows "Notifications are disabled."; on → visible
  card. Auto-hide stayed paused (`PopupCount > 0`) while a popup was open.
- Launcher behavior (pins, icons, Add, Settings, Close) unaffected.

---

## 6. Documentation and repository (completed)

- `PROJECT_ARCHITECTURE.md` — current architecture + target module architecture.
- `IMPLEMENTATION_PROGRESS.md` — this file.
- `CHANGELOG.md` — per-phase change log.
- Git repository initialized and pushed to `https://github.com/xs2ilyas/RightDock-.git`
  (source only; `bin`, `obj`, `publish`, `.vs` ignored).

---

## 7. Measured resource baseline

Measured on a running `publish\RightDock.exe` (Windows 11, 2560×1600 @125 %, dock
visible, Phase 1 services active, 30 s idle sample):

| Metric | Value |
| --- | --- |
| Processes | 1 |
| Working set | ≈99 MB |
| Private bytes | ≈59 MB |
| Idle CPU | 0.02 s over 30 s → ≈0.05 % of one core |
| Timers | 110 ms hover poll; scheduler timer (idle until work is due); animation timer only during slides |
| Database | one SQLite connection (WAL), no background threads |
| Network | none |

The launcher-only 1.2.0 baseline was ≈76 MB working set / ≈58 MB private / ≈0.22 %
average CPU; Phase 1 adds ≈23 MB working set while keeping private bytes flat.
Phase 6 will repeat this with profiling tools, memory-leak checks, and idle-vs-active
comparisons per module.

---

## 8. Known limitations (current release)

- **Primary monitor only** — the monitor containing (0,0) is used; per-monitor dock
  placement is not implemented.
- **Hover polling** — 110 ms `GetCursorPos` instead of event hooks; cheap but scheduled
  for review in Phase 6.
- **No pin reordering** — order is insertion order; drag-to-reorder is pending.
- **Fixed pin icon size** (26 px) — icon-size setting is pending.
- **Missing dock settings** — width, position, always-on-top and tooltip toggles from the
  design mockup are not implemented yet; auto-hide is the only behavior toggle.
- **Global icon color only** — per-icon colors are not supported; shell icons keep their
  original colors.
- **SVG parser** is intentionally minimal (no transforms, styles, gradients, arcs
  outside path data).
- **Startup entry is static** — if the exe is moved, the Run value must be re-toggled.
- **No installer or code signing** — distributed as a `publish` folder.
- **No product modules in 1.3.0** — the module infrastructure ships empty (the Modules
  page shows "No modules are registered yet."); Focus, Tasks, Reminders and Notes arrive
  in Phase 2.
- **Notifications are in-app cards only** — they stack above the desktop but are not sent
  to the Windows Action Center, and quiet hours/snooze are not implemented yet.
- **Scheduler intervals are fixed** — recurrence rules, snooze and per-item timezones are
  module-level concerns for later phases.
- **Module settings page is reserved** — `CreateSettingsView()` exists on the contract but
  no generic host for module-specific settings exists yet.
- **Settings window is fixed-size** (500×520, five tabs); future categories will require
  a scrollable layout.

---

## 9. Pending roadmap

### Phase 1 — Core infrastructure (completed in 1.3.0)
- [x] `ModuleManager` registration, enable/disable, persisted configuration.
- [x] `PopupHost` — single instance per module, placement, Esc/outside-click, no
      duplicate popups, auto-hide integration.
- [x] SQLite `Database` with migrations, indices, transactions, parameterized queries.
- [x] Centralized timestamp-based `Scheduler` (sleep/resume + restart reconciliation).
- [x] `NotificationService` with in-app cards, master toggle and click actions.
- [x] Settings integration for Modules and Notifications.
- [x] Preserve launcher behavior and measured resource baseline.
- [x] Compact/expanded popup shell, toggle behavior and expanded persistence validated
      with a temporary module (removed before release).

### Phase 2 — Productivity modules
- [ ] Focus: presets, start/pause/resume/reset, session history, daily stats, task link.
- [ ] Tasks: CRUD, priority, due date/time, categories, projects, recurring, search,
      Today/Upcoming/Completed views.
- [ ] Reminders: one-time/daily/weekly/custom recurrence, snooze, complete, history.
- [ ] Quick Ideas / Notes: capture, pin, search, convert to task/reminder.

### Phase 3 — Personal tracking
- [ ] Habits: boolean/counter/duration, 10 seeded habits + custom, daily/weekly history,
      streaks, one-click completion from compact popup.
- [ ] Medication: schedules vs intake logs stored separately, daily/weekly views,
      taken/skipped/snoozed/missed, no dosage recommendations.
- [ ] Health & Lifestyle: water, meals, sleep, relaxation, vegetables, fruits, activity,
      weight; rings, averages, history.

### Phase 4 — Expanded workspace
- [ ] Dashboard window with focus/task/habit overview and widget grid.
- [ ] Daily Overview compact + detailed summary.
- [ ] Notes manager (categories, archive, pinning).
- [ ] Calendar / daily planner (internal calendar; tasks, reminders, medication, focus).
- [ ] Statistics (daily/weekly/monthly, native simple charts, tables).

### Phase 5 — Quick Tools and learning
- [ ] Quick Ideas/Notes tool, Breathing tool, Screen-break reminder — exactly three.
- [ ] Learning tracker: goals, courses, reading goals, sessions, progress.
- [ ] Enforce the "do not implement" list (no stopwatch, calculator, clipboard manager,
      file manager, journal, etc.).

### Phase 6 — Hardening
- [ ] CPU/RAM profiling per module; memory-leak checks.
- [ ] Database migration, backup/restore/export/import tests.
- [ ] Startup, sleep/resume, notification, multi-monitor, high-DPI tests.
- [ ] Installer/upgrade readiness.

---

## 10. Acceptance criteria tracking

From the master specification §11:

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Existing application launching unchanged | ✔ Tested |
| 2 | Existing custom icons still work | ✔ Tested |
| 3 | Dock approximately 50 px wide | ✔ Tested |
| 4 | Productivity modules open as compact popups | ✔ Tested (infrastructure via temporary module) |
| 5 | Expanded workspace works | ✔ Tested (980×680 shell; content arrives in Phase 2) |
| 6 | Compact and expanded views share data | ◻ Pending (no product module yet) |
| 7 | Tasks persist after restart | ◻ Pending |
| 8 | Reminders work correctly | ◻ Pending |
| 9 | Recurring schedules work | ◻ Pending |
| 10 | Focus sessions record accurate durations | ◻ Pending |
| 11 | Habits retain daily/weekly history | ◻ Pending |
| 12 | Medication schedules and intake logs separate | ◻ Pending |
| 13 | Quick Ideas / Notes saves information | ◻ Pending |
| 14 | Breathing sessions function | ◻ Pending |
| 15 | Screen-break reminders function | ◻ Pending |
| 16 | Module settings persist | ✔ Tested |
| 17 | Disabled modules preserve data | ✔ Tested |
| 18 | Backup and restore work | ◻ Pending |
| 19 | No mandatory internet access | ✔ Tested (no network code) |
| 20 | No unnecessary background processes | ✔ Tested (single process) |
| 21 | CPU and memory measured | ✔ Baseline recorded (§6); full profiling Phase 6 |
| 22 | Launcher functionality not broken | ✔ Tested |

---

## 11. Test method notes

- UI automation via PowerShell + UIAutomation. The dock is found by window title
  "Right Dock"; module popups and the Settings window appear as owned windows nested in
  the dock's UIA tree, so window-scoped searches must attach directly to the HWND
  (`AutomationElement.FromHandle`). Tab buttons expose `AutomationId`s
  (`TabModules`, `TabNotifications`); module toggles use `TogglePattern`, links and
  buttons use `InvokePattern`.
- Geometry verified through Win32 (`GetWindowRect`, `SystemParameters.WorkArea`) rather
  than visual estimates. Note that non-DPI-aware processes receive virtualized
  coordinates (125 % scaling → 80 % values), so UIA queries must set `SetProcessDPIAware`
  before computing click points.
- Persistence verified by closing/restarting the app and re-reading `pins.json`,
  `settings.json`, the Run registry value, and `ModuleSettings` directly from
  `rightdock.db`.
- Notifications were verified with timed DPI-aware screen captures because shell
  balloon/toast notifications were suppressed by the environment.
- The temporary Smoke Test module used for end-to-end validation was deleted before
  release, along with its `ModuleSettings` row.
