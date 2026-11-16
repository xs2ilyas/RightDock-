# RightDock — Implementation Progress

> Release **1.2.0** — application launcher dock complete.
> Productivity modules: **not started** (Phase 1 is next).
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
| Docs | Architecture, progress, changelog, GitHub repository | ✔ Done |
| Phase 1 | Module manager, popup host, SQLite, scheduler, notifications | ◻ Pending |
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

## 5. Documentation and repository (completed)

- `PROJECT_ARCHITECTURE.md` — current architecture + target module architecture.
- `IMPLEMENTATION_PROGRESS.md` — this file.
- `CHANGELOG.md` — per-phase change log.
- Git repository initialized and pushed to `https://github.com/xs2ilyas/RightDock-.git`
  (source only; `bin`, `obj`, `publish`, `.vs` ignored).

---

## 6. Measured resource baseline

Measured on a running `publish\RightDock.exe` (Windows 11, 1920×1200 @100 %, ~20 min
after launch, dock auto-hidden most of the time):

| Metric | Value |
| --- | --- |
| Processes | 1 |
| Working set | ≈76 MB |
| Private bytes | ≈58 MB |
| Total CPU time | 2.67 s over 20.3 min → ≈0.22 % average |
| Timers | 110 ms hover poll; 16 ms animation timer only during 170 ms slides |
| Network | none |

Phase 6 will repeat this with profiling tools, memory-leak checks, and idle-vs-active
comparisons per module.

---

## 7. Known limitations (current release)

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
- **Settings window is fixed-size** (440×470) and scrolls nowhere; new categories will
  require a scrollable layout.

---

## 8. Pending roadmap

### Phase 1 — Core infrastructure
- [ ] `ModuleManager` registration, enable/disable, persisted configuration.
- [ ] `PopupHost` — single instance per module, placement, Esc/outside-click, no
      duplicate popups, auto-hide integration.
- [ ] SQLite `DataService` with migrations, indices, transactions, parameterized queries.
- [ ] Centralized timestamp-based `Scheduler` (sleep/resume + restart reconciliation).
- [ ] `NotificationService` (toasts/balloons, sound, snooze, quiet hours).
- [ ] Settings integration for module categories.
- [ ] Preserve launcher behavior and measured resource baseline.

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

## 9. Acceptance criteria tracking

From the master specification §11:

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Existing application launching unchanged | ✔ Tested |
| 2 | Existing custom icons still work | ✔ Tested |
| 3 | Dock approximately 50 px wide | ✔ Tested |
| 4 | Productivity modules open as compact popups | ◻ Pending |
| 5 | Expanded workspace works | ◻ Pending |
| 6 | Compact and expanded views share data | ◻ Pending |
| 7 | Tasks persist after restart | ◻ Pending |
| 8 | Reminders work correctly | ◻ Pending |
| 9 | Recurring schedules work | ◻ Pending |
| 10 | Focus sessions record accurate durations | ◻ Pending |
| 11 | Habits retain daily/weekly history | ◻ Pending |
| 12 | Medication schedules and intake logs separate | ◻ Pending |
| 13 | Quick Ideas / Notes saves information | ◻ Pending |
| 14 | Breathing sessions function | ◻ Pending |
| 15 | Screen-break reminders function | ◻ Pending |
| 16 | Module settings persist | ◻ Pending |
| 17 | Disabled modules preserve data | ◻ Pending |
| 18 | Backup and restore work | ◻ Pending |
| 19 | No mandatory internet access | ✔ Tested (no network code) |
| 20 | No unnecessary background processes | ✔ Tested (single process) |
| 21 | CPU and memory measured | ✔ Baseline recorded (§6); full profiling Phase 6 |
| 22 | Launcher functionality not broken | ✔ Tested |

---

## 10. Test method notes

- UI automation via PowerShell + UIAutomation (`AutomationElement` root search by name
  "Right Dock"; owned popups found as descendants), TogglePattern/InvokePattern, plus
  DPI-aware screenshots.
- Geometry verified through Win32 (`GetWindowRect`, `SystemParameters.WorkArea`) rather
  than visual estimates.
- Persistence verified by killing/restarting the published exe and re-reading
  `pins.json`, `settings.json`, and the Run registry value.
- The desktop's display configuration changed mid-testing (2560×1600 @125 % →
  1920×1200 @100 %); all measurements were repeated successfully at the new DPI.
- Native `ColorDialog` descendants are not exposed to UIA; it was verified visually and
  dismissed with `WM_CLOSE`.
