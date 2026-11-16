# RightDock — Implementation Progress

> Release **1.5.0** — launcher + Phase 1 infrastructure + Phase 2 productivity modules
> (Focus, Tasks, Reminders, Notes) + Phase 3 personal tracking (Habits, Medication,
> Health & Lifestyle) complete.
> Next up: **Phase 4** (dashboard, daily overview, notes manager, calendar/planner,
> statistics).
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
| v1.4.0 | Phase 2: Focus, Tasks, Reminders, Quick Ideas / Notes | ✔ Done ✔ Tested |
| v1.5.0 | Phase 3: Habits, Medication, Health & Lifestyle | ✔ Done ✔ Tested |
| Docs | Architecture, progress, changelog, GitHub repository | ✔ Done |
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

## 6. v1.4.0 — Phase 2 productivity modules (completed, tested)

Four modules are registered in `App.OnStartup` before `ModuleManager.Start()`, so they
appear in the dock and Modules settings with their data loaded from SQLite.

### Focus (`Modules/Focus`)
- Presets 15/25/45/60 minutes; 38 px clock, progress bar, READY/FOCUSING/PAUSED state.
- Start inserts a `FocusSessions` row; Pause accumulates wall-clock segments; Resume;
  Reset discards the in-flight row; completion finalizes planned/actual seconds with
  `IsCompleted=1` and posts a "Focus session complete" toast that opens the dashboard.
- Compact: today's minutes, session count, presets, primary action, recent sessions.
- Expanded: 7-day bars, session history with task/duration/completion, and a task link
  (active tasks from `TaskStore`; locked while running; "No task" option).
- Stale open sessions are finalized on activation; deactivation finalizes an in-flight
  session as ended early, so a crash/restart cannot leave a stuck timer.

### Tasks (`Modules/Tasks`)
- Full CRUD with title, due date/time, category, project, priority (Low/Normal/High),
  recurrence (once/daily/weekly/monthly) and notes.
- Due parser accepts `14:30`, `11/20 09:00`, `2026-11-20 09:00`, date-only (17:00) and
  rolls a past time-only value to tomorrow; invalid input shows inline red text.
- Views Today / Upcoming / Completed / All plus case-insensitive search across
  title/notes/category/project; compact shows today's tasks and quick add on Enter.
- Completing a recurring task marks the row completed and inserts the next occurrence;
  rows show due/overdue state, priority dot, recurrence and category/project metadata.

### Reminders (`Modules/Reminders`)
- One-time, daily, weekly and `custom:<days>` recurrence; notes; quick add (+1 h).
- Snooze 5/10/30/60 minutes; complete; active/history views.
- One scheduler key per active reminder, rebuilt on activation and every mutation;
  firing shows a toast (`Reminder · <title>` + due/notes) that opens the dashboard.
- Recurring reminders create their next occurrence on completion; overdue reminders fire
  once shortly after startup; all schedules survive restart.

### Notes (`Modules/Notes`)
- Quick capture (Ctrl+Enter), title derived from the first line, pin/unpin, category and
  search; "Pinned" marker and updated time in lists.
- "Create task" inserts a task (no due date, normal priority); "Create reminder" inserts
  a reminder due in one hour. Both refresh the target module immediately so its lists
  and scheduler pick up the new row.
- Expanded editor body was reduced to 150 px so Save/Delete and the Convert section are
  visible without scrolling at 980×680.

### Shared UI work
- `Modules/ModuleTheme.xaml`: shared dark styles (cards, section titles, inputs, primary/
  secondary/ghost/danger buttons, chips, round check, divider, progress).
- `Modules/ModuleUtil.cs`: formatting (`Today 11:44`, `Fri 09:00`), clock/duration text,
  due-date parsing, recurrence labels and next-occurrence math.
- Popup chrome, Settings and Icon Picker backgrounds changed from 96 % to fully opaque
  (`#FF242424`) to remove faint bleed-through over bright windows.
- Text inputs got automation ids (`TaskQuickAdd`, `TaskTitle`, `TaskDue`, `NoteCapture`,
  `ReminderDue`, …) for reliable UI-automation testing and accessibility.
- Reworded Focus completion to "1 minute"/"N minutes"; Tasks repeat chips moved to a
  full-width row so **Monthly** no longer wraps.

### Verified on a live Windows 11 session
- **Focus:** start → FOCUSING; pause froze the clock and showed Resume; resume continued;
  reset returned to READY and removed the session row. A completed 1-minute session (test
  preset, reverted afterwards) produced the toast, a `FocusSessions` row with 60/60 s and
  `IsCompleted=1`, today's bar/total and a history row tied to the linked "Weekly review"
  task. Task chips locked while running.
- **Tasks:** quick add persisted; editor saved due 14:30, category, project, notes,
  High priority and Daily recurrence (verified in SQLite and list metadata); completing it
  created tomorrow's row and Completed view retained the original; search filtered by
  notes; Today/Upcoming/Completed/All and the new full-width repeat row rendered correctly.
- **Reminders:** quick add created a +1 h reminder; editing to a near-future due fired the
  toast at the exact minute; clicking the toast opened the expanded view; snooze stored
  `SnoozeUntilUtc` (+5 min), showed "Snoozed · Today 11:44" and fired again on time;
  completion moved the row to History ("Completed Today 11:44 · Daily") and inserted the
  next daily occurrence; custom 3-day recurrence saved as `custom:3`; changing a due time
  and restarting the app still fired at the scheduled minute (startup reschedule works).
- **Notes:** body-only capture, derived title, category + pin persisted; conversions
  created Task and Reminder rows; reminder conversion showed "Reminder set for …"; search
  filtered; the 150 px editor shows Convert buttons without scrolling.
- **Integration:** modules list in Settings → Modules with descriptions and Hide links;
  dock icons open the compact popups; restart preserved tasks, reminders, notes, focus
  history and module visibility; single process; no errors in `rightdock.log`.

---

## 7. v1.5.0 — Phase 3 personal tracking modules (completed, tested)

Three modules are registered after Notes in `App.OnStartup`; they appear in the dock and
in Settings → Modules, and follow the established compact/expanded view pattern over a
store with a `Changed` event.

### Habits (`Modules/Habits`)
- Ten habits are seeded **once**, only when the `Habits` table is empty: Drink water
  (counter, 8 glasses), Exercise (duration, 30 min), Read (20 min), Meditate (10 min),
  Walk outside (20 min), Journal, No sugar, Stretch, Vitamins (boolean) and Eat
  vegetables (counter, 3 servings). User deletions are never re-seeded.
- Custom habits support boolean / counter / duration tracking with a target and unit;
  the editor disables target/unit for boolean habits and validates targets in 1…1,000,000.
- Compact: "Done today x of y", best streak, one-click round check, and a `+` step for
  counter/duration habits (duration steps by 5 minutes, everything else by 1, capped at
  the target). Completed rows show "n-day streak".
- Expanded: full habit list with inline toggles, editor (name, description, tracking
  type chips, target, unit), armed delete, and a LAST 7 DAYS matrix with a filled dot per
  completed day plus the current streak.
- `HabitStore` reads 400 days of logs, computes current streak (consecutive days ending
  today or yesterday, so an unfinished today does not break it) and best streak, and
  upserts logs on `(HabitId, LogDate)`, so toggling and `+`/`−` share one row per day.
  Deleting a habit cascades its logs.

### Medication (`Modules/Medication`)
- `Medications`, `MedicationSchedules` and `MedicationLogs` stay separate: a medication
  owns one row per dose time, with every-day/weekdays/weekends applicability.
- `EnsureDoses()` materializes pending logs for the last six days (bounded by the
  medication's creation time so a new medication does not create phantom past doses) and
  for today, then `MarkMissed()` rolls any pending/snoozed dose older than two hours
  (relative to `COALESCE(SnoozeUntilUtc, ScheduledUtc)`) to `missed`.
- Statuses: pending → taken (records `TakenUtc`), skipped, snoozed (`SnoozeUntilUtc`)
  or missed. Take/Snooze/Skip appear only for actionable (pending/snoozed) doses; the
  last-7-days view is read-only and grouped by local date descending.
- Scheduler keys `med:dose:<logId>:<ticks>` are rebuilt on activation and every mutation;
  snoozed doses are scheduled at `SnoozeUntilUtc`. Firing shows
  "Medication · <name> / <dosage> · scheduled for HH:mm." and opens the dashboard on
  click. A `med:rollover` task at tomorrow 00:00:02 materializes the next day's doses.
- The editor accepts comma/semicolon/space-separated 24-hour times (normalized to HH:mm,
  sorted, de-duplicated) and rejects empty names or invalid times with inline red text.
  No dosage recommendations are shown — dosage is free text for the user's own label.

### Health & Lifestyle (`Modules/Health`)
- Eight metrics with fixed targets/steps/colors: water 8 glasses, meals 3, sleep 8 h
  (0.5 step), activity 30 min (5 min step), vegetables 3 servings, fruits 2 servings,
  relaxation 15 min (5 min step) and weight in kg (measured value).
- Quick log rows with `−`/`+` for the seven cumulative metrics; values below zero delete
  the day's row. `HealthStore` upserts on `(LogDate, MetricType)` and computes today's
  value, 7-day average, days logged and latest weight.
- Compact: three rings (water/activity/sleep) with value and target, quick log, and the
  latest weight line. Expanded: rings, quick log for seven metrics, a weight card with
  latest/7-day average/days logged, and a LAST 7 DAYS table with per-metric day bars,
  averages and entry counts.
- `RingView` is a code-only ring: a #26FFFFFF ellipse track plus a stroked arc built with
  `StreamGeometry` (an `EllipseGeometry` at 100 %), rounded caps, centered value and
  label, and a value/target tooltip.

### Shared work
- Schema migration **v2** adds `MedicationLogs.SnoozeUntilUtc` and
  `IX_MedicationLogs_Status`; it applies to existing 1.4.0 databases in place
  (`PRAGMA user_version` 1 → 2).
- Habit logs use a unique `(HabitId, LogDate)` index and `ON CONFLICT` upserts; health
  logs use `UNIQUE(LogDate, MetricType)`.
- New editor inputs expose automation ids (`HabitName`, `HabitDescription`,
  `HabitTarget`, `HabitUnit`, `MedName`, `MedDosage`, `MedTimes`, `MedNotes`,
  `WeightInput`) and action buttons expose descriptive automation names
  ("Toggle habit X", "Increment habit X", "Take/Snooze/Skip X at HH:mm").

### Verified on a live Windows 11 session
- **Habits:** the database migrated to `user_version=2` and exactly 10 habits seeded;
  Drink water toggled to 8/8 (streak 1 day), two `+` taps took Exercise to 10/30 min and
  Journal completed → "2 of 10", "1 day"; the dashboard showed checkmarks, streak meta
  and the week matrix; a custom "Test habit" (counter, 4 cups) saved, incremented to
  1/4, survived a restart, then deleted with its log; backfilled Nov 14–15 water logs
  produced three filled dots (Sat/Sun/Mon) and a 3-day/best-streak.
- **Medication:** created "Vitamin D" 1 tablet at 12:35 and 20:00 → two pending doses;
  taking the 20:00 dose early recorded "Taken 12:32"; at 12:35 the toast read
  "Medication · Vitamin D / 1 tablet · scheduled for 12:35."; snooze 10 min stored
  `SnoozeUntilUtc` 12:46 and displayed "Snoozed · 12:46"; skip stored `skipped` and
  hid the actions; a backdated medication materialized four days of 09:00 doses, all
  marked Missed, and the week view grouped Today/Sun/Sat/Fri; deleting the medication
  removed its schedules and logs (cascade).
- **Health:** quick log produced 2/8 glasses, 1/3 meals, 5/30 min and 0.5/8 h; rings
  rendered colored arcs; weight 72.5 kg saved with "7-day average 72.5 kg · 1 of 7 days
  logged"; `abc` showed "Enter a weight in kilograms, for example 72.4." and left data
  unchanged; the last-7-days table showed today's bars, per-metric averages and
  "4 entries logged".
- **Persistence/integration:** habits, medication statuses and health values survived
  two restarts; all Phase 2 popups still open; single process; no errors logged.

---

## 8. Documentation and repository (completed)

- `PROJECT_ARCHITECTURE.md` — current architecture + target module architecture.
- `IMPLEMENTATION_PROGRESS.md` — this file.
- `CHANGELOG.md` — per-phase change log.
- Git repository initialized and pushed to `https://github.com/xs2ilyas/RightDock-.git`
  (source only; `bin`, `obj`, `publish`, `.vs` ignored).

---

## 9. Measured resource baseline

Measured on a running `publish\RightDock.exe` (Windows 11, 2560×1600 @125 %, dock
visible, all seven modules registered, no popups open, 30 s idle sample):

| Metric | Value |
| --- | --- |
| Processes | 1 |
| Working set | ≈124 MB |
| Private bytes | ≈72 MB |
| Idle CPU | 0.02 CPU seconds over 30 s → ≈0.07 % of one core |
| Timers | 110 ms hover poll; scheduler timer (idle until work is due); focus 1 s tick only while a session runs; animation timer only during slides |
| Database | one SQLite connection (WAL), no background threads |
| Network | none |

Baseline progression: 1.2.0 launcher-only ≈76 MB working set / ≈58 MB private /
≈0.22 % average CPU; 1.3.0 Phase 1 ≈99 MB / ≈59 MB / ≈0.05 %; 1.4.0 Phase 2
≈123 MB / ≈71 MB / 0 %; 1.5.0 Phase 3 ≈124 MB / ≈72 MB / ≈0.07 %. The Phase 3 step is
small because the new stores only query short date windows and the three WPF views load
on first open. Phase 6 will repeat this with profiling tools, memory-leak checks, and
idle-vs-active comparisons per module.

---

## 10. Known limitations (current release)

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
- **Recurrence is preset-based** — tasks support none/daily/weekly/monthly and reminders
  support one-time/daily/weekly/custom-N-days; there is no full RRULE engine (no "every
  weekday", "last Friday", or end-date rules yet).
- **Due dates are parsed from text** — accepted formats include `today`, `tomorrow`,
  `yyyy-MM-dd`, `HH:mm`, and combinations; there is no calendar-picker control yet.
- **Focus timing is wall-clock** — sessions keep running through sleep/idle; there is no
  idle detection or auto-pause, and pausing records segments that are finalised on
  resume/stop.
- **No drag reordering** — tasks and reminders are listed by their sort fields; manual
  ordering is not implemented.
- **Notifications are in-app cards only** — they stack above the desktop but are not sent
  to the Windows Action Center, and quiet hours/snooze are not implemented yet (reminder
  snooze is module-level).
- **Module settings page is reserved** — `CreateSettingsView()` exists on the contract but
  no generic host for module-specific settings exists yet; module options currently live
  in each expanded view.
- **Settings window is fixed-size** (500×520, five tabs); future categories will require
  a scrollable layout.
- **Habit streaks use a 400-day window** — current/best streaks are computed from logs in
  the last 400 days (enough for daily habits); longer history is not considered.
- **Habit week view covers 7 days** — the dashboard matrix shows the current week; longer
  trends arrive with Statistics in Phase 4.
- **Medication doses are materialized on refresh** — the last six days (bounded by the
  medication's creation time) plus today; doses older than two hours without action are
  marked missed. There is no adherence percentage, refill tracking, or inventory count.
- **Health metrics are fixed** — the eight metrics have preset targets, units and steps;
  user-defined metrics require the per-module settings page planned for a later phase.
- **No backup/restore/export/import** — the SQLite file can be copied manually while the
  app is closed; first-class data tools arrive in Phase 6.

---

## 11. Pending roadmap

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

### Phase 2 — Productivity modules (completed in 1.4.0)
- [x] Focus: presets, start/pause/resume/reset, session history, daily stats, task link.
- [x] Tasks: CRUD, priority, due date/time, categories, projects, recurring, search,
      Today/Upcoming/Completed views.
- [x] Reminders: one-time/daily/weekly/custom recurrence, snooze, complete, history.
- [x] Quick Ideas / Notes: capture, pin, search, convert to task/reminder.

### Phase 3 — Personal tracking (completed in 1.5.0)
- [x] Habits: boolean/counter/duration, 10 seeded habits + custom, daily/weekly history,
      streaks, one-click completion from the compact popup.
- [x] Medication: schedules vs intake logs stored separately, daily/weekly views,
      taken/skipped/snoozed/missed, no dosage recommendations.
- [x] Health & Lifestyle: water, meals, sleep, relaxation, vegetables, fruits, activity,
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

## 12. Acceptance criteria tracking

From the master specification §11:

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Existing application launching unchanged | ✔ Tested |
| 2 | Existing custom icons still work | ✔ Tested |
| 3 | Dock approximately 50 px wide | ✔ Tested |
| 4 | Productivity modules open as compact popups | ✔ Tested (all seven at 320 DIP) |
| 5 | Expanded workspace works | ✔ Tested (980×680 dashboard per module) |
| 6 | Compact and expanded views share data | ✔ Tested (both views over one store, rebuilt on change) |
| 7 | Tasks persist after restart | ✔ Tested |
| 8 | Reminders work correctly | ✔ Tested (fire, toast, snooze, complete, overdue on startup) |
| 9 | Recurring schedules work | ✔ Tested (daily/weekly/monthly tasks, daily/weekly/custom-N reminders) |
| 10 | Focus sessions record accurate durations | ✔ Tested (planned vs actual seconds, pause segments) |
| 11 | Habits retain daily/weekly history | ✔ Tested (10 seeded + custom habits, 7-day matrix, 3-day streak across restart) |
| 12 | Medication schedules and intake logs separate | ✔ Tested (`Medications`/`MedicationSchedules`/`MedicationLogs`, pending→taken/skipped/snoozed/missed) |
| 13 | Quick Ideas / Notes saves information | ✔ Tested (capture, pin, search, convert) |
| 14 | Breathing sessions function | ◻ Pending (Phase 5) |
| 15 | Screen-break reminders function | ◻ Pending (Phase 5) |
| 16 | Module settings persist | ✔ Tested |
| 17 | Disabled modules preserve data | ✔ Tested |
| 18 | Backup and restore work | ◻ Pending (Phase 6) |
| 19 | No mandatory internet access | ✔ Tested (no network code) |
| 20 | No unnecessary background processes | ✔ Tested (single process) |
| 21 | CPU and memory measured | ✔ Baseline recorded (§9); full profiling Phase 6 |
| 22 | Launcher functionality not broken | ✔ Tested |

---

## 13. Test method notes

- UI automation via PowerShell + UIAutomation. The dock is found by window title
  "Right Dock"; module popups and the Settings window appear as owned windows nested in
  the dock's UIA tree, so window-scoped searches must attach directly to the HWND
  (`AutomationElement.FromHandle`). Tab buttons and module inputs expose `AutomationId`s
  (`TabModules`, `TabNotifications`, `TaskTitle`, `TaskDue`, `TaskSearch`, `ReminderTitle`,
  `ReminderDue`, `ReminderCustomDays`, `NoteTitle`, `NoteBody`, `NoteCapture`,
  `HabitName`, `HabitDescription`, `HabitTarget`, `HabitUnit`, `MedName`, `MedDosage`,
  `MedTimes`, `MedNotes`, `WeightInput`, …); module list rows, chips and round checks
  have descriptive automation names ("Toggle habit Drink water", "Take Vitamin D at
  12:35", "Add Water"), module toggles use `TogglePattern`, links and buttons use
  `InvokePattern`.
- **`InvokePattern` on a WPF `Button` does raise `Click`** — used for row/edit/delete
  buttons because it works without scrolling the target into view; real mouse clicks are
  still used for chips, round checks and popup dismissal.
- **Context menus are separate top-level windows** — snooze menus must be clicked with
  a global search (`clickglobal`) after opening the anchor button.
- **WPF text boxes surface as `ControlType.Edit`** in UIA, not "TextBox"; filter on
  ControlType.Edit when dumping elements, and prefer `ValuePattern` for typing.
- **`TogglePattern.Toggle()` is not a real click** — it flips `IsChecked` visually but
  does not raise WPF `Click`, so handlers (priority/repeat/filter chips, round
  checkboxes) do not run. Use real mouse clicks (`SetCursorPos` + `mouse_event`) or
  `InvokePattern` where applicable.
- **The dock must be revealed before clicking** — hover the right edge, wait for the
  slide-in, then compute points from `GetWindowRect`.
- Geometry verified through Win32 (`GetWindowRect`, `SystemParameters.WorkArea`) rather
  than visual estimates. Note that non-DPI-aware processes receive virtualized
  coordinates (125 % scaling → 80 % values), so UIA queries must set `SetProcessDPIAware`
  before computing click points.
- Persistence verified by closing/restarting the app and re-reading `pins.json`,
  `settings.json`, the Run registry value, and the productivity tables (`Tasks`,
  `Reminders`, `Notes`, `FocusSessions`, `Habits`, `HabitLogs`, `Medications`,
  `MedicationSchedules`, `MedicationLogs`, `HealthLogs`) directly from `rightdock.db`
  with Python's `sqlite3` module.
- Missed-dose and streak behavior were seeded through the database (a medication row
  created three days earlier, prior-day habit logs) and then re-checked in the UI after
  a restart, exercising the same `EnsureDoses`/`MarkMissed`/streak code paths the app
  uses at runtime.
- Toast reminders for medication were captured with `tools/watch-toast.ps1` (12:35
  dose); the snooze context menu was driven through global UIA clicks.
- Notifications were verified with timed DPI-aware screen captures and a watcher that
  polls for visible topmost windows (titleless, 200–700 × 40–400 DIP) because shell
  balloon/toast notifications were suppressed by the environment; `tools/watch-toast.ps1`
  captures the card and can click it.
- Helper scripts in `tools/`: `ui.ps1` (list/shot/elements/invoke/set/click/clickxy/keys),
  `watch-toast.ps1` (toast capture/click), `close-dock.ps1` (clean shutdown between runs).
- The temporary Smoke Test module used for end-to-end validation was deleted before
  release, along with its `ModuleSettings` row.
