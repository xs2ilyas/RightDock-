# RightDock — Implementation Progress

> Release **1.11.1** — launcher + Phase 1 infrastructure + Phase 2 productivity modules
> (Focus, Tasks, Reminders, Notes) + Phase 3 personal tracking (Habits, Medication,
> Health & Lifestyle) + Phase 4 planning and insight (Dashboard, Notes manager, Calendar,
> Statistics) + Phase 5 quick tools and learning (Quick Tools, Learning) + Phase 6
> hardening (profiling, backup/restore, migration and OS-event tests, installer assets) +
> Phase 7 bell sounds (ten synthesized choices, triple focus ring, single reminder ring) +
> Phase 8 focus mini timer (draggable pill with pin and close) + Phase 9 system tray
> icon (open, minimize, restart, exit) + fullscreen recovery (dock returns after videos)
> complete.
> Next up: post-1.11 maintenance (see §17 known limitations).
> Last updated: 2026-11-18.

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
| v1.6.0 | Phase 4: Dashboard, Daily Overview, Notes manager, Calendar planner, Statistics | ✔ Done ✔ Tested |
| v1.7.0 | Phase 5: Quick Tools (notes, breathing, screen breaks) + Learning tracker | ✔ Done ✔ Tested |
| v1.8.0 | Phase 6: profiling/leak checks, migration and backup/restore tests, sleep/display handling, installer assets | ✔ Done ✔ Tested |
| v1.9.0 | Phase 7: ten synthesized bell sounds, three-ring focus completion, one-ring reminders, sound pickers in Settings | ✔ Done ✔ Tested |
| v1.10.0 | Phase 8: draggable focus mini timer with pin, close, persisted placement | ✔ Done ✔ Tested |
| v1.11.0 | Phase 9: system tray icon with open, minimize, restart and exit | ✔ Done ✔ Tested |
| v1.11.1 | Fullscreen recovery: reliable return after fullscreen apps, no video overlap | ✔ Done ✔ Tested |
| Docs | Architecture, progress, changelog, README, GitHub repository | ✔ Done |

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

## 8. v1.6.0 — Phase 4 planning and insight (completed, tested)

Phase 4 adds three new modules plus a Notes manager upgrade. No schema migration was
needed: Dashboard, Calendar and Statistics aggregate the existing tables read-only, and
the Notes manager uses the existing `Notes.IsArchived` column.

### Dashboard (`Modules/Dashboard`)
- `DashboardModule` (dock icon `lucide:layout-dashboard`, registered first so it heads the
  dock) builds a `DashboardSnapshot` from the Focus, Task, Habit, Medication, Reminder,
  Note and Health stores: focus today plus seven daily totals, task due/overdue/completed
  today, habit done/total and best streak, medication taken/planned and next actionable
  dose, active/overdue reminders and next reminder, note/pin/archive counts, health
  today values and latest weight.
- Compact "DAILY OVERVIEW" shows focus, tasks, habits, medication, next up and a health
  snapshot, with **Open dashboard** and **Refresh**.
- Expanded view: "Today at a glance" summary, four quick actions (Focus timer, Tasks,
  Plan day → Calendar, Statistics) and a 2×3 widget grid (Focus 7-day bars, Tasks,
  Habits, Medication, Health, Reminders & Notes). Every widget navigates to its owning
  module through `ModuleContext.OpenExpanded`; no editing is duplicated.
- A refresh timer exists only while a view is loaded: 60 s compact, 30 s expanded.

### Daily Overview
- Included in the Dashboard compact (values and next-up) and expanded summary. Note counts
  are singularised ("1 note") and health values read "2 glasses of 8 glasses".

### Notes manager (`Modules/Notes`)
- Category filter chips are built from distinct stored categories; **All categories**
  clears the filter. New notes prefill the active category.
- Archive/restore is a soft hide: `SetArchived` updates `IsArchived` + `UpdatedUtc`, the
  Archived chip switches the list, and the editor button toggles Archive/Restore with
  status feedback. The list count and empty state reflect the archive view.
- Pinning remains available per row and in the editor; row meta shows archived/pinned
  state and the active category.

### Calendar planner (`Modules/Calendar`)
- `CalendarModule` (dock icon `lucide:calendar-days`, `PLANNER` compact) aggregates tasks
  by due date, reminders by effective due (`COALESCE(SnoozeUntilUtc, DueUtc)`), planned
  medication occurrences from schedules (with existing log status) and ended focus
  sessions, sorted by local time.
- Expanded view: Monday-start 6-week month grid with weekday header, prev/Today/next
  navigation, per-day counts, two titles and "+N more"; selected day highlighted, today
  outlined, adjacent-month days dimmed; colored legend for Task/Reminder/Medication/Focus.
- Selected-day agenda shows time, colored stripe, title, type/status detail; done items
  are struck through, overdue items red. Quick add validates a title and `h:mm` time
  (default 09:00), inserts a task or reminder through the owning store, refreshes that
  module and selects the day.

### Statistics (`Modules/Statistics`)
- `StatisticsModule` (dock icon `lucide:chart-column`) buckets the last 7 days, 8 Monday
  weeks or 6 calendar months. Focus sums ended-session minutes; tasks count `CompletedUtc`;
  habit rate is completed check-ins ÷ (active habits × days); medication adherence is
  taken ÷ (taken + missed + skipped); health averages come from `HealthStore.Averages`.
- Shared `StatsChart` builds uniform bar charts for four metrics (focus minutes, tasks,
  habit %, medication %) with metric colors, value labels and tooltips; rate metrics use
  a fixed 0–100 % scale.
- Expanded view: Daily/Weekly/Monthly chips, four summary cards, metric chips, chart with
  explanatory caption, and a scrollable breakdown table (period, focus, tasks, habits,
  medication). Compact view mirrors the four totals and a mini chart.
- Both views refresh on a 60 s timer while loaded; granularity and metric are retained by
  the module for the session.

### Verified on a live Windows 11 session
- **Dock/registration:** all ten modules appear in order Dashboard, Focus, Tasks,
  Reminders, Notes, Habits, Medication, Health & Lifestyle, Calendar, Statistics after a
  restart; existing pins and Add/Settings/Close controls unchanged.
- **Dashboard:** compact showed "15 min / 2 sessions", "0 due", "2 of 10", "1 of 2",
  "Drink water · Tomorrow 11:44" and the health snapshot (2/8 glasses, 5/30 min, 0.5/8 h,
  72.5 kg); expanded showed the 7-day focus chart with today's 15 min bar and all six
  widgets populated from the same data. After quick-adding entries from the planner, the
  dashboard update showed "1 due", "3 active" and "1 note".
- **Calendar:** November 2026 grid showed 70 aggregated entries and per-day counts; today
  listed 8 agenda items (reminders, medication, focus, tasks) with done items struck
  through; quick add "Phase 4 quick add" at 09:00 created a task visible in Tasks, and
  "Phase 4 reminder" at 15:30 created a reminder shown in the agenda and counted by the
  dashboard.
- **Statistics:** Daily showed 15 min/2 sessions, 1 task, 6 % habit rate (4 check-ins),
  50 % medication adherence (1 of 2), chart title/caption per metric and matching table
  rows; Weekly switched to "Last 8 weeks" with week labels and 1 % habit rate; Monthly
  switched to "Last 6 months" (Jun–Nov) and the habit chart/table; metric chips re-rendered
  the chart and caption for Focus/Tasks/Habits/Medication.
- **Notes:** category chip "Planning" filtered to the single note; editing and clicking
  Archive hid the note from the active list, showed "1 archived note"/"1 note in
  archive", switched the editor to Restore and reported "Note archived"; Restore returned
  it with "Note restored"; the row pin toggle removed and restored "Pinned" meta.
- **Residual checks:** all Phase 2/3 compact views still open with their data; single
  process; after opening every Phase 4 view once the process idled at 145.7 MB working
  set, 94.3 MB private and 0.078 CPU seconds over 30 s.

---

## 9. v1.7.0 — Phase 5 quick tools and learning (completed, tested)

Phase 5 adds two modules. Quick Tools is deliberately limited to exactly three tools:
quick note capture, guided breathing and screen-break reminders. Learning adds goals,
courses/reading targets, sessions and progress. No stopwatch, calculator, clipboard
manager, file manager or journal was added.

### Quick Tools (`Modules/QuickTools`, dock icon `lucide:sparkles`)
- `QuickToolsModule` composes the existing Notes store with a new `BreathingStore` and
  module config. The one 1 s `DispatcherTimer` exists only while a breathing session runs.
- **Quick note** writes through `NoteStore.Add` with category `Quick` and calls
  `ModuleContext.RefreshModule("notes")`, so the note appears immediately in the Notes
  module and Dashboard. Compact capture supports Ctrl+Enter; the expanded view shows the
  last notes with a shortcut to the Notes module.
- **Breathing** offers Box (4-4-4-4), 4-7-8, Calm (4 in · 6 out) and Coherent (5 in ·
  5 out). The module tracks phase, countdown, cycles and elapsed seconds; start 3/5/8
  cycles, pause/resume, stop. The expanded view animates a circle (ScaleTransform,
  0.9 s per tick, 0.74–1.0). Completed or stopped sessions with cycles > 0 are saved to
  `BreathingSessions` (pattern key/name, cycles, duration, UTC start/end).
- **Screen break** settings (`ScreenBreakEnabled`, `ScreenBreakMinutes` 1–240,
  `LastBreakDate`, `BreaksToday`, `BreathPattern`) persist in
  `ModuleSettings.ConfigJson`. Enabling registers the fixed-interval scheduler key
  `quicktools:screenbreak`; firing shows the "Screen break" toast with 20-20-20 guidance
  and opens the expanded view on click. "Remind now" fires on demand; reset restarts the
  interval; disabling or deactivating cancels the scheduler.
- Compact sections: QUICK NOTE, BREATHE (pattern chips, countdown, phase bar, cycle
  presets, start/pause/stop) and SCREEN BREAK (toggle, next reminder, breaks today,
  15/30/45/60 chips, Remind now).

### Learning (`Modules/Learning`, dock icon `lucide:graduation-cap`)
- `LearningStore` over the existing `LearningGoals` and `LearningSessions` tables with no
  migration: goals store title, description, category, target/progress values, unit, due
  date (`yyyy-MM-dd`), completed flag and timestamps; sessions store start, duration,
  notes and an optional goal (`ON DELETE SET NULL`).
- Goal categories are Course/Reading/Skill/Other; units minutes/hours/pages/chapters/
  lessons/sessions. Compact shows active goals, this-week minutes and up to four goals
  with progress bars.
- Expanded view: search over title/description/category, All/Active/Completed chips,
  dynamic category chips, scrollable goal list with selection, goal editor (title,
  category, unit, target, progress, due date, description), complete/reopen, armed
  delete, and a log-session form with minutes/notes and the three most recent sessions.
- Logging a session on a minutes or hours goal adds the minutes to `ProgressValue`
  automatically (hours converted, two-decimal rounding); other units advance only through
  the progress field. Week statistics start Monday.

### Verified on a live Windows 11 session
- **Dock/registration:** all twelve modules appear in order Dashboard, Focus, Tasks,
  Reminders, Notes, Habits, Medication, Health & Lifestyle, Calendar, Statistics,
  Quick Tools, Learning; launch, settings and existing pins unchanged.
- **Quick note:** saved "Phase 5 quick note" from the compact with Ctrl+Enter; Notes count
  went 1 → 2, the note appeared in Notes under category Quick, and the input cleared with
  the status message.
- **Breathing:** start/pause/resume/stop saved 1 cycle and 19 s; a full 5-cycle Box
  session completed after 80 s with "Complete · 5 cycles · 1 min"; `BreathingSessions`
  rows were present; the expanded view showed the animated circle and session history.
- **Screen break:** enabling with 15 min showed "Next HH:mm"; Remind now produced the
  toast, incremented Breaks today and persisted
  `{"ScreenBreakEnabled":true,"ScreenBreakMinutes":15,…}`; a restart restored On/15 min/
  1 break/Box/2 notes; a custom 1-minute interval fired another toast automatically;
  settings were reset to off/30 min.
- **Learning:** created "Spanish Course" (Course, hours, target 10, due 2026-12-31),
  logged 30 min → progress "0.5 / 10 hours" plus a session row; completed and reopened;
  created "Read Atomic Habits" (Reading, pages, 240, progress 50); search and category
  filters returned the correct rows; editing the target retained progress; armed delete
  removed the goal while retaining its session; after restart one active goal remained
  with 30 min this week.
- **Residual checks:** Dashboard and earlier modules unchanged; single process; no errors
  in `%APPDATA%\RightDock\logs`; idle after Phase 5 measured at 126.8 MB working set,
  71.5 MB private and 0.047 CPU seconds over 30 s. Phase 5 test data was removed from the
  live database before release.

---

## 10. v1.8.0 — Phase 6 hardening (completed, tested)

Phase 6 adds no productivity module. It hardens data safety and OS integration, and adds
the release infrastructure.

### Database diagnostics (`Data/Database.cs`)
- `SchemaVersion` (`PRAGMA user_version`), `SizeBytes` (database + WAL + SHM),
  `IntegrityCheck()`, `CountRows(table)` behind an explicit `KnownTables` whitelist, and
  `BackupTo(path)` using `SqliteConnection.BackupDatabase`.
- A backup copy is switched from WAL to DELETE journal mode after copying, so archives
  contain one self-contained `rightdock.db` and never a stale `-wal`/`-shm` pair.

### Backup and restore (`Data/DataBackupService.cs`)
- **Format:** ZIP with `rightdock.db`, `settings.json`, `pins.json`, optional `icons/` and
  `backup.json` (app version, UTC time, schema version, per-table counts).
- **Create:** consistent online copy, integrity and schema validation, then compress to
  the user-selected path (default `RightDock-backup-yyyyMMdd-HHmmss.zip`). The settings
  window reports `Backup created · schema v3 · 212.0 KB · 44 rows · <file>`.
- **Inspect:** extracts the database to a temp folder, validates it and returns the
  summary shown in the confirmation dialog.
- **Stage:** extracts the archive with a path-traversal guard, validates the staged
  database and JSON files, and writes `restore.pending`.
- **Apply (next start, before SQLite opens):** validates again, copies the current DB,
  settings and pins to `backups\pre-restore-yyyyMMdd-HHmmss\`, replaces them from staging
  (including WAL/SHM when present), removes the marker and logs the restored summary. On
  failure it rolls the database back from the safety copy and reports the error.

### Settings: Data tab
- Database path, `SQLite · schema v3 · <size> · <rows>`, Open data folder, Run integrity
  check, Create backup…, Restore backup…, Copy diagnostics and the About row
  (`RightDock 1.8.0 · .NET 5 · schema v3`).
- Restore shows a warning with the archive summary; confirming starts
  `RightDock.exe --restart` and shuts down. The new process waits up to 10 s on the
  instance mutex, applies the staged restore, shows a "Backup restored" toast and keeps a
  single process.
- "Copy diagnostics" puts a plain-text report (version, OS, runtime, DB path, schema,
  integrity, size, row totals, modules and table counts) on the clipboard.

### OS-event hardening
- `WM_POWERBROADCAST` resume (`PBT_APMRESUMEAUTOMATIC`/`PBT_APMRESUMESUSPEND`) and
  `WM_TIMECHANGE` call `Scheduler.Reconcile()`; resume and `WM_DISPLAYCHANGE` call
  `RefreshGeometry()`, which re-reads the monitor and re-reserves the AppBar.

### Release assets and tooling
- `Resources/RightDock.ico` — deterministic multi-size icon (16–256 px PNG entries) drawn
  by `tools/make-icon.ps1`; the csproj embeds it and sets Version 1.8.0, CompanyName,
  ProductName and description.
- `installer/RightDock.iss` — Inno Setup 6 per-user installer (x64, Windows 10 1809+,
  Start Menu/optional desktop icon, AppMutex).
- `tools/publish.ps1` — publish + portable ZIP (`dist\RightDock-1.8.0-win-x64.zip`), with
  `-Installer` to compile the setup when Inno Setup is installed.
- `tools/measure.ps1` — CPU/RAM/handle/thread sampling; `tools/stress-ui.ps1` — repeated
  module popup open/close with private/working/handle/GDI/USER metrics.
- `THIRD-PARTY-NOTICES.md` — Lucide ISC, Feather MIT, Microsoft.Data.Sqlite MIT, SQLite
  public domain.

### Verified on a live Windows 11 session
- **Data page:** rendered with six tabs at 560×560; showed
  `C:\Users\DELL\AppData\Roaming\RightDock\rightdock.db`,
  `SQLite · schema v3 · 260.1 KB · 44 rows`, and "Run integrity check" reported
  `Integrity check passed · schema v3 · 260.1 KB · 44 rows`.
- **Backup:** created through the Save dialog; the archive contained `backup.json`,
  `pins.json`, `settings.json`, `rightdock.db` and no WAL/SHM files; the embedded database
  reported `journal_mode=delete`, `user_version=3`, `integrity_check=ok` and 44 rows.
- **Restore:** added a "Phase 6 restore probe" note (1 → 2 notes), restored the archive
  through the UI (confirmation showed `schema v3 · 212.0 KB · 44 rows`), the app
  restart-cycled from PID 12380 to 24800, notes returned to 1 ("Weekly review only"),
  and `backups\pre-restore-20261116-141505\` held a valid database containing both
  pre-restore notes plus settings and pins. The log recorded the staged restore, the
  `(restart)` start and "Backup restored".
- **Single instance:** launching `RightDock.exe` while running left one process;
  launching with `--restart` waited out the 10 s window and exited without creating a
  duplicate.
- **Migration:** a v1 fixture (schema v1, 4 tasks, 2 medication logs, 1 note) was placed
  at the live path; startup logged `Applied database migration v2`/`v3`, and the resulting
  database had `user_version=3`, `BreathingSessions`, `MedicationLogs.SnoozeUntilUtc`,
  integrity `ok` and all rows retained. The exact pre-test database was restored
  afterwards.
- **OS events:** posted `WM_POWERBROADCAST 0x12`, `WM_TIMECHANGE` and `WM_DISPLAYCHANGE`
  to the dock window; the log recorded "System resumed; reconciling scheduled work." and
  "System clock changed; reconciling scheduled work." and the reserved rect stayed
  `2498,0 62×1540`.
- **Profiling/leak check:** `tools/measure.ps1` idle sample: 150.4 MB working set,
  98.9 MB private, 0.016 CPU seconds over 10 s, 476 handles, 9 threads.
  `tools/stress-ui.ps1`: 27 cycles × 12 modules × open+close (~650 popups); private
  memory rose from 56.5 MB to ≈111 MB and plateaued there for the last 15 cycles while
  handles (483–492), GDI (38) and USER (31) stayed flat — no leak; the initial rise was
  WPF caching plus uncollected short-lived objects.
- **Regression:** all twelve modules opened after migration, restart and stress; Notes
  showed "Weekly review", Tasks loaded its list; no errors or exceptions in the log.
- **Installer readiness:** `publish\RightDock.exe` reports FileVersion 1.8.0.0,
  ProductVersion 1.8.0, CompanyName RightDock and product "Right Dock" with the embedded
  icon; `dist\RightDock-1.8.0-win-x64.zip` contains 37 files. Inno Setup 6 is not
  installed on this machine, so `RightDock-1.8.0-setup.exe` was not compiled; the `.iss`
  and `-Installer` path are ready for a build machine.

---

## 11. v1.9.0 — Phase 7 bell sounds (completed, tested)

### Sound synthesis (`Services/BellSynth.cs`)

Ten built-in voices, each described by fundamental frequency, duration, decay, attack,
partial ratios/gains and optional beating: Classic Bell (880 Hz), Soft Chime (660),
Glass Bell (1320), Marimba (523.25), Temple Bowl (220 with beating), Digital Chime
(1046.5), Harp Pluck (784), Music Box (1568), Deep Resonant (440 with beating) and Bright
Bell (1174.66). `CreateWav(key)` mixes the decaying partials, applies short attack and
release fades, normalizes to a 0.55 peak, and writes a 44.1 kHz PCM16 mono WAV to memory.
No WAV files, audio codecs or downloads are involved. `DefaultFocus = "classic"` and
`DefaultReminder = "soft"`; `IsKnown(key)` validates persisted choices.

### Playback (`Services/SoundService.cs`)

One cached `SoundPlayer` per bell key (streams and players disposed on shutdown).

- `PlayFocusComplete()` — three rings 850 ms apart using `FocusSound`.
- `PlayReminder()` — one ring using `ReminderSound`.
- `PreviewFocus(key)` / `PreviewReminder(key)` — same patterns for the Settings pickers,
  bypassing the notifications master toggle so auditioning always works.
- Starting a new sequence cancels the remaining timer; live alerts respect
  `NotificationsEnabled` both when scheduled and on each tick, and every sequence logs
  `Bells: <label> using "<key>" x<n>.`

`DockServices` creates the service and passes it through `ModuleManager` to
`ModuleContext.Sounds`; `FocusModule.Complete()` and `RemindersModule.Fire()` ring
immediately before showing their card. `AppSettings.FocusSound`/`ReminderSound` are
validated on load and saved whenever a picker changes.

### Settings

The Notifications page (now inside a scroll viewer) ends with a **SOUNDS** section: a
ten-item picker, a Preview link and a description for focus completion and for reminders,
plus a shared status line and the note that tones are generated on the device. Picking a
sound saves it, plays a preview and updates the status.

### Verified on a live Windows 11 session

- **Synthesis:** a linked console harness rendered all ten bells; durations 0.85–2.6 s,
  files 75–229 KB, identical normalized peak (18,022) and ten distinct RMS values — no
  duplicate timbres; `IsKnown("bogus")` returned false.
- **Pickers:** each combo listed the ten names; choosing Glass Bell and Music Box wrote
  `"FocusSound": "glass"` / `"ReminderSound": "musicbox"` to `settings.json` and logged
  `Bells: focus preview using "glass" x3.` / `Bells: reminder preview using "musicbox" x1.`
- **Notifications off:** Preview still played and the status read "Playing Glass Bell ·
  three rings. Notifications are off, so live alerts stay silent."
- **Real reminder:** a reminder due 14:39 rang once
  (`Bells: reminder using "musicbox" x1.`); a second reminder that became due while
  notifications were off added no bell log entry.
- **Focus completion:** a 15-minute session started from the Focus popup rang three times
  at 14:51:32 (`Bells: focus session complete using "glass" x3.`) and the
  "Focus session complete — You finished 15 minutes of focused work." card was captured
  at the same second (420×113 at 2066,1415).
- **Persistence:** after a restart the pickers still showed Glass Bell and Music Box and
  overdue reminders rang with the saved tone; the test choices were returned to the
  defaults during cleanup.
- **Release:** published as 1.9.0 (FileVersion 1.9.0.0, ProductVersion 1.9.0);
  `dist\RightDock-1.9.0-win-x64.zip` holds 37 entries; the published pickers showed the
  defaults and previewed correctly. A fresh published process idles at 109.0 MB working
  set, 62.2 MB private, 0.016 CPU seconds over 10 s, 463 handles and 17 threads; no errors
  in the log.
- **Cleanup:** the two test reminders and the 15-minute focus row were deleted from the
  live database, and the test sound choices were reset to `classic`/`soft`.

---

## 12. v1.10.0 — Phase 8 focus mini timer (completed, tested)

### Window (`Modules/Focus/FocusMiniWindow.xaml`)
- A 176×64 DIP borderless, transparent `Window` (`ShowInTaskbar=False`,
  `ShowActivated=False`, manual placement, topmost by default) containing a 15 px-radius
  dark navy pill with a soft shadow. Contents are the 26 px tabular mm:ss clock and two
  24 px buttons: pin and close. There is no title bar, resize grip or timer icon.
- `FocusModule` creates it lazily on the first Start and calls `Present()`. Placement
  restores `MiniTimerLeft`/`MiniTimerTop` from `settings.json` (clamped to the virtual
  screen); without saved values it opens 18 DIP from the work area's right and bottom
  edges, which parks it just left of the dock above the taskbar.
- Dragging uses `DragMove()` on the pill background; the resulting position is saved after
  the drag. The clock turns amber while paused and white while running, and the pin glyph
  is an accent pushpin on a tinted background when topmost, gray otherwise.
- The close button calls `Hide()` so the session keeps running; the pill reappears on the
  next Start. Reset, completion, module deactivation and app shutdown close it.

### Lifecycle
- `Start()` shows the pill before notifying views; `Pause()` leaves it visible in the
  paused color; `Resume()` returns it to the running color; `Changed` drives per-second
  refresh. Showing it does not activate it, so starting from the Focus popup keeps the
  popup focused.
- `App.xaml` sets `ShutdownMode="OnMainWindowClose"`, so closing the dock exits the process
  even when the pill is still open.

### Settings
- `AppSettings` adds `MiniTimerPinned` (bool, default `true`) and nullable
  `MiniTimerLeft`/`MiniTimerTop`. No Settings page is needed because the controls live on
  the pill; existing `settings.json` files load unchanged.

### Verified on a live Windows 11 session
- Starting a 25-minute session created the "Focus timer" window at 176×64 DIP in the
  default bottom-right position, ticking `24:59` while the compact view reported
  `FOCUSING`; the dock remained the active reservation owner.
- A drag moved the pill and wrote `MiniTimerLeft: 1780` / `MiniTimerTop: 1016.8`; the next
  Start reopened at the saved position, and the default position returned after the saved
  values were removed.
- The pin button set `MiniTimerPinned: false`, cleared `WS_EX_TOPMOST` and switched the
  glyph to gray; relaunching the app restored the saved pin state.
- Pause turned the digits amber, resume turned them white, and closing the pill hid it
  while the compact view still showed the paused session; Reset hid it permanently,
  and the following Start recreated it.
- Closing the dock with the pill open exited the process cleanly (previously the pill
  kept the application alive).
- Test sessions were deleted from the live database and the test position/pin values were
  reset, leaving the user's existing sessions untouched.

---

## 13. v1.11.0 — Phase 9 system tray icon (completed, tested)

### Icon and menu (`TrayIcon.cs`)
- Created after the dock window is shown: a WinForms `NotifyIcon` carrying the
  executable's own icon (`ExtractAssociatedIcon`), the tooltip "Right Dock", and a
  `ContextMenuStrip` rendered with a dark color table (`#1C1C1C` background, translucent
  hover, matching border and separators) so it matches the dock surface.
- Left-clicking the icon restores the dock. The menu holds **Open Right Dock**,
  **Minimize to tray**, a separator, **Restart Right Dock** and **Exit Right Dock**.
- Enabled state is recomputed whenever the menu opens: Open is disabled while the dock is
  visible; Minimize is disabled while it is already in the tray or a modal dialog is open.
- The icon runs on the WPF UI thread. `UseWindowsForms` was already enabled in the
  project, so no new dependency was added; the rest of the app remains WPF.

### Dock integration (`MainWindow`)
- `MinimizeToTray()` sets a hidden flag, closes module popups, unregisters the AppBar
  (maximized windows regain the full width), hides the window and stops edge reveal while
  the flag is set.
- `RestoreFromTray()` clears the flag, shows the window if needed and re-reserves the
  AppBar through `ShowDock()`, giving a 1.6 s peek when auto-hide is enabled.
- Restart starts the executable with `--restart` and lets the existing single-instance
  mutex handoff take over; Exit calls `Application.Shutdown()`. `App.OnExit` disposes the
  tray icon before the services.

### Verification tooling
- `tools/ui.ps1` gained `rightclickxy`, and `clickglobal`/`invokeglobal` now work without
  a `-Window` argument, which the tray menu checks rely on.

### Verified on a live Windows 11 session
- The published build showed the app icon in the notification overflow with the
  "Right Dock" tooltip; the icon artwork matched the executable's icon.
- Right-clicking opened the dark four-item menu. With the dock visible, **Open** was
  disabled and **Minimize** enabled.
- **Minimize to tray** removed the "Right Dock" window from the visible-window list and
  the work area widened from 2498 to 2560 px. Hovering the right screen edge for two
  seconds did not reveal the dock. A left-click on the tray icon restored it and the work
  area returned to 2498 px.
- **Restart Right Dock** ended the old process and started a new one with the dock
  visible and the work area reserved; only one process remained.
- **Exit Right Dock** terminated the process, removed the icon and restored the full work
  area of 2560 px. Closing the dock normally while the icon was active also exited.
- Published 1.11.0 reports FileVersion 1.11.0.0; `dist\RightDock-1.11.0-win-x64.zip`
  holds 37 entries. Settled idle baseline: 119.8 MB working set, 69.9 MB private,
  0.016 CPU seconds over 10 s, 409 handles, 13 threads.
- No test data was written to the database; only window and work-area state changed.

### Baseline impact
- About 11 MB more working set and 9 MB more private bytes than 1.10.0, because the
  WinForms notification-area stack loads on demand; CPU, handle and thread counts stay
  effectively flat.

---

## 14. v1.11.1 — Fullscreen recovery fix (completed, tested)

### Problem
Windows sends `ABN_FULLSCREENAPP` (`lParam = 1/0`) when a fullscreen window appears or
ends, but browsers and video players often exit fullscreen without the matching "ended"
notification. `_suppressedByFullscreen` then stayed set: the dock stayed off-screen, and
restoring it from the tray showed it for a moment before the next hover tick hid it
again ("comes and goes"). Only a restart recovered.

### Fix (`MainWindow`, `NativeMethods`)
- `OnFullscreenChanged` records `_fullscreenSince` and clears `_keepShownUntilHover`.
  While suppression is set, each 110 ms hover tick waits 700 ms for the shell to settle,
  then calls `IsFullscreenOnDockMonitor()`. If no fullscreen app owns the dock's monitor,
  the flag is cleared ("Fullscreen app ended; releasing dock suppression.") and the
  normal shown/hidden logic resumes; with auto-hide off the dock re-appears immediately.
- `IsFullscreenOnDockMonitor()` checks the real foreground window: it skips the dock's
  own process and the shell classes (`Progman`, `WorkerW`, `Shell_TrayWnd`,
  `Shell_SecondaryTrayWnd`), ignores windows that still carry `WS_CAPTION` (maximized
  windows), requires the window to be on the same monitor as the dock, and requires its
  rectangle to cover `rcMonitor` within a 2 px tolerance — the same rule the taskbar uses.
- `RestoreFromTray()` skips while a fullscreen app is genuinely active (the dock would
  flash under the video) and clears a stale suppression flag otherwise.
- A tray-restored dock sets `_keepShownUntilHover`; with auto-hide enabled it stays
  visible with no hide deadline until the pointer actually reaches the reveal zone or the
  dock, so a restore can no longer vanish before the click.
- `NativeMethods` gained `GetForegroundWindow`, `GetWindowRect`, `GetWindowThreadProcessId`,
  `GetClassName`, `GWL_STYLE` and `WS_CAPTION`.

### Verification tooling
- `tools/simulate-fullscreen.ps1` posts the registered AppBar callback with `lParam`
  1 (`-State on`) or 0 (`-State off`) to the dock HWND, so suppression can be driven
  without a player. Real tests used a borderless 2560×1600 WinForms window and Edge F11.

### Verified on a live Windows 11 session
- Simulated `on` with no fullscreen app: dock hid, then the tick released suppression at
  +0.7 s and the work area returned to 2498 px with no user action.
- Borderless topmost and non-topmost 2560×1600 windows: work area stayed at 2560 px and
  no release was logged while the window was open; closing it returned the dock in every
  run (12:24:26, 12:26:21 and 12:28:11 in the session log).
- Edge F11 on a local page: window rect 0,0 2560×1600, class `Chrome_WidgetWin_1`, no
  caption, work area 2560 px; leaving fullscreen released suppression ~3 s later and the
  work area returned to 2498 px — the reported YouTube-style scenario, no restart.
- Temporarily enabling auto-hide, then **Open Right Dock** with the pointer away from the
  edge: the dock stayed visible for the whole check; hovering it and moving away hid it
  after the 650 ms delay. The original `settings.json` was restored afterwards.
- Notes popup smoke test passed; the tray icon and tray menu from 1.11.0 still worked.
- Note: during a real fullscreen app the shell auto-hides the taskbar, so the tray icon is
  not reachable until fullscreen ends; the guard covers the case where it is invoked.

---

## 15. Documentation and repository (completed)

- `PROJECT_ARCHITECTURE.md` — current architecture + target module architecture.
- `IMPLEMENTATION_PROGRESS.md` — this file.
- `CHANGELOG.md` — per-phase change log.
- Git repository initialized and pushed to `https://github.com/xs2ilyas/RightDock-.git`
  (source only; `bin`, `obj`, `publish`, `.vs` ignored).

---

## 16. Measured resource baseline

Measured on a running `publish\RightDock.exe` (Windows 11, 2560×1600 @125 %, dock
visible, all twelve modules registered, no popups open, 10 s idle sample with
`tools/measure.ps1`, fresh 1.11.1 process):

| Metric | Value |
| --- | --- |
| Processes | 1 |
| Working set | 117.0 MB |
| Private bytes | 63.4 MB |
| Idle CPU | 0.016 CPU seconds over 10 s (0.16 % of one core) |
| Handles / threads | 436 / 10 |
| Timers | 110 ms hover poll; scheduler timer (idle until work is due); focus 1 s tick only while a session runs; Quick Tools 1 s breathing tick only while a session runs; view refresh timers only while Dashboard/Calendar/Statistics are open; animation timer only during slides; bell ring timer only while a multi-ring sequence is playing; mini timer redraws on the focus tick and exists only during a session; the tray icon has no timer and reacts to shell messages |
| Audio | WAV rendered and cached on the first ring; no audio buffers while idle |
| Database | one SQLite connection (WAL), no background threads |
| Network | none |

Stress test (`tools/stress-ui.ps1`, 1.8.0 build, 27 cycles × 12 modules × open+close
≈ 650 popups): private memory rose from 56.5 MB to ≈111 MB, then stayed between 107.7 and
111.8 MB for the final 15 cycles while working set settled at ≈163 MB, handles stayed at
483–492, GDI at 38 and USER at 31. The initial growth is WPF view caching and short-lived
objects awaiting GC; steady-state counts are flat, so no leak was found.

Baseline progression: 1.2.0 launcher-only ≈76 MB working set / ≈58 MB private /
≈0.22 % average CPU; 1.3.0 Phase 1 ≈99 MB / ≈59 MB / ≈0.05 %; 1.4.0 Phase 2
≈123 MB / ≈71 MB / 0 %; 1.5.0 Phase 3 ≈124 MB / ≈72 MB / ≈0.07 %; 1.6.0 Phase 4 after
opening all views ≈146 MB / ≈94 MB / ≈0.26 %; 1.7.0 Phase 5 idle 126.8 MB / 71.5 MB /
0.047 CPU seconds over 30 s; 1.8.0 post-stress 150.4 MB / 98.9 MB / 0.016 CPU seconds
over 10 s; 1.9.0 fresh 109.0 MB / 62.2 MB / 0.016 CPU seconds over 10 s; 1.10.0 fresh
108.6 MB / 61.0 MB / 0.016 CPU seconds over 10 s; 1.11.0 fresh 119.8 MB / 69.9 MB /
0.016 CPU seconds over 10 s; 1.11.1 fresh 117.0 MB / 63.4 MB / 0.016 CPU seconds over
10 s. The 1.8.0 sample was taken after the stress run, so WPF's retained views are
included; 1.9.0–1.11.1 were measured on freshly started published processes.

---

## 17. Known limitations (current release)

- **Primary monitor only** — the monitor containing (0,0) is used; per-monitor dock
  placement is not implemented.
- **Fullscreen detection is foreground-window based** — only a borderless window that
  covers the whole monitor hosting the dock suppresses it; a maximized player or a
  fullscreen app on another monitor does not. While a fullscreen app is active Windows
  auto-hides the taskbar, so the tray icon cannot be clicked until fullscreen ends; the
  dock then returns on its own.
- **Hover polling** — 110 ms `GetCursorPos` instead of event hooks; kept after the Phase 6
  review because CPU cost measured at 0.016 CPU seconds over 10 s idle.
- **No pin reordering** — order is insertion order; drag-to-reorder is pending.
- **Fixed pin icon size** (26 px) — icon-size setting is pending.
- **Missing dock settings** — width, position, always-on-top and tooltip toggles from the
  design mockup are not implemented yet; auto-hide is the only behavior toggle.
- **Global icon color only** — per-icon colors are not supported; shell icons keep their
  original colors.
- **SVG parser** is intentionally minimal (no transforms, styles, gradients, arcs
  outside path data).
- **Startup entry is static** — if the exe is moved, the Run value must be re-toggled.
- **Installer is unsigned and unbuilt here** — `installer/RightDock.iss` and
  `tools/publish.ps1 -Installer` are ready, but Inno Setup 6 was not installed on the test
  machine, so only the portable ZIP was produced; there is no code signing certificate.
- **Recurrence is preset-based** — tasks support none/daily/weekly/monthly and reminders
  support one-time/daily/weekly/custom-N-days; there is no full RRULE engine (no "every
  weekday", "last Friday", or end-date rules yet).
- **Due dates are parsed from text** — accepted formats include `today`, `tomorrow`,
  `yyyy-MM-dd`, `HH:mm`, and combinations; there is no calendar-picker control yet. The
  Calendar planner shows due items on a month grid but only supports quick-adding new
  tasks/reminders, not moving or editing existing entries inline.
- **Calendar is Monday-start and single timezone** — weeks always start Monday and all
  aggregation uses the current local timezone; there is no configurable week start.
- **Dashboard and Statistics are read-only** — widgets navigate to the owning module for
  edits; lifetime/all-time statistics, custom date ranges and CSV export are not
  implemented yet.
- **Focus timing is wall-clock** — sessions keep running through sleep/idle; there is no
  idle detection or auto-pause, and pausing records segments that are finalised on
  resume/stop.
- **No drag reordering** — tasks and reminders are listed by their sort fields; manual
  ordering is not implemented.
- **Notifications are in-app cards only** — they stack above the desktop but are not sent
  to the Windows Action Center, and quiet hours/snooze are not implemented yet (reminder
  snooze is module-level).
- **Bells play on the default output device** — `SoundPlayer` uses the current system
  volume; there is no per-alert volume or mute, no output-device selection, and Windows
  sound-scheme sounds are not used. Ten timbres are synthesized locally; custom audio
  files are not supported.
- **Module settings page is reserved** — `CreateSettingsView()` exists on the contract but
  no generic host for module-specific settings exists yet; module options currently live
  in each expanded view.
- **Settings window is fixed-size** (560×560, six tabs); later categories still require a
  scrollable layout.
- **Habit streaks use a 400-day window** — current/best streaks are computed from logs in
  the last 400 days (enough for daily habits); longer history is not considered.
- **Habit week view covers 7 days** — the habit dashboard matrix shows the current week;
  longer habit trends live in Statistics (up to 6 months).
- **Medication doses are materialized on refresh** — the last six days (bounded by the
  medication's creation time) plus today; doses older than two hours without action are
  marked missed. Statistics computes adherence from logged statuses, but there is no
  refill tracking or inventory count. Future planner days show planned occurrences
  computed from schedules rather than persisted rows.
- **Health metrics are fixed** — the eight metrics have preset targets, units and steps;
  user-defined metrics require the per-module settings page planned for a later phase.
- **Backups are manual archives** — create/restore one ZIP at a time from Settings; there
  is no scheduled backup, no cloud sync, no automatic rotation and no export to CSV/JSON.
  Log files and the `backups\` folder itself are not included in archives. Restoring
  always restarts the app, and a backup made by a newer schema opens read-only validation
  only up to the current schema version.
- **Breathing is foreground-only** — the session runs on the UI thread and pauses when
  the process is closed; there is no lock-screen or background continuation.
- **Screen-break interval is wall-clock** — reminders fire on the scheduler interval
  regardless of typing/activity; there is no idle detection, pause-for-fullscreen or
  per-app suppression yet.
- **Learning progress auto-updates only in minutes/hours** — pages, chapters, lessons and
  sessions advance solely from the progress field; there are no goal reminders or due-date
  notifications.
- **Quick Tools stays at three tools** — additional utilities are intentionally excluded
  from this release.

---

## 18. Pending roadmap

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

### Phase 4 — Expanded workspace (completed in 1.6.0)
- [x] Dashboard window with focus/task/habit overview and widget grid.
- [x] Daily Overview compact + detailed summary.
- [x] Notes manager (categories, archive, pinning).
- [x] Calendar / daily planner (internal calendar; tasks, reminders, medication, focus).
- [x] Statistics (daily/weekly/monthly, native simple charts, tables).

### Phase 5 — Quick Tools and learning (completed in 1.7.0)
- [x] Quick Ideas/Notes tool, Breathing tool, Screen-break reminder — exactly three.
- [x] Learning tracker: goals, courses, reading goals, sessions, progress.
- [x] Enforce the "do not implement" list (no stopwatch, calculator, clipboard manager,
      file manager, journal, etc.).

### Phase 6 — Hardening (completed in 1.8.0)
- [x] CPU/RAM profiling per module; memory-leak checks.
- [x] Database migration, backup/restore/export-import tests (export/import as one ZIP).
- [x] Startup, sleep/resume, clock-change, display-change, high-DPI tests.
- [x] Installer/upgrade readiness (icon, metadata, portable ZIP, Inno Setup script).

### Phase 7 — Bell sounds (completed in 1.9.0)
- [x] Ten synthesized bell timbres (no bundled audio files or downloads).
- [x] Focus completion rings three times; reminder rings once.
- [x] Separate ten-sound pickers in Settings → Notifications, each with Preview.
- [x] Choices persist in `settings.json`, are validated on load, and real alerts stay
      silent while notifications are off.

### Phase 8 — Focus mini timer (completed in 1.10.0)
- [x] A small draggable timer pill appears automatically when a focus session starts.
- [x] Pin toggles always-on-top and persists; close hides it for the session.
- [x] Position persists, and reset/completion/deactivation hide it.

### Phase 9 — System tray icon (completed in 1.11.0)
- [x] Right Dock icon in the notification area with the app's own artwork.
- [x] Left-click restores the dock; menu minimizes to tray, restarts and exits.
- [x] Minimizing releases the AppBar reservation and suppresses edge reveal until restored.

---

## 19. Acceptance criteria tracking

From the master specification §11 (items 23–25 track the Phase 4 scope, 26–27 Phase 5,
28 Phase 7, 29 Phase 8, 30 Phase 9 and 31 the 1.11.1 fullscreen fix):

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Existing application launching unchanged | ✔ Tested |
| 2 | Existing custom icons still work | ✔ Tested |
| 3 | Dock approximately 50 px wide | ✔ Tested |
| 4 | Productivity modules open as compact popups | ✔ Tested (all twelve at 320 DIP) |
| 5 | Expanded workspace works | ✔ Tested (980×680 expanded views, widget grid, month planner, statistics, breathing circle) |
| 6 | Compact and expanded views share data | ✔ Tested (both views over one store, rebuilt on change) |
| 7 | Tasks persist after restart | ✔ Tested |
| 8 | Reminders work correctly | ✔ Tested (fire, toast, snooze, complete, overdue on startup) |
| 9 | Recurring schedules work | ✔ Tested (daily/weekly/monthly tasks, daily/weekly/custom-N reminders) |
| 10 | Focus sessions record accurate durations | ✔ Tested (planned vs actual seconds, pause segments) |
| 11 | Habits retain daily/weekly history | ✔ Tested (10 seeded + custom habits, 7-day matrix, 3-day streak across restart) |
| 12 | Medication schedules and intake logs separate | ✔ Tested (`Medications`/`MedicationSchedules`/`MedicationLogs`, pending→taken/skipped/snoozed/missed) |
| 13 | Quick Ideas / Notes saves information | ✔ Tested (Quick capture writes to Notes category Quick, pin, search, categories, archive/restore, convert) |
| 14 | Breathing sessions function | ✔ Tested (4 patterns, pause/resume, cycle completion, persisted sessions and stats) |
| 15 | Screen-break reminders function | ✔ Tested (interval scheduler, toast, Remind now, persisted settings, restart) |
| 16 | Module settings persist | ✔ Tested (module state plus `ConfigJson` screen-break settings across restart) |
| 17 | Disabled modules preserve data | ✔ Tested |
| 18 | Backup and restore work | ✔ Tested (ZIP create/inspect/stage, restart apply, safety copy, rollback path) |
| 19 | No mandatory internet access | ✔ Tested (no network code) |
| 20 | No unnecessary background processes | ✔ Tested (single process) |
| 21 | CPU and memory measured | ✔ Tested (idle 0.016 CPU s / 98.9 MB private; ~650-popup stress plateau with flat handles/GDI) |
| 22 | Launcher functionality not broken | ✔ Tested |
| 23 | Dashboard aggregates daily focus, tasks, habits, medication, health | ✔ Tested (snapshot + six widgets after restart) |
| 24 | Calendar aggregates tasks, reminders, medication and focus | ✔ Tested (month grid, agenda, quick add) |
| 25 | Statistics show daily/weekly/monthly charts and tables | ✔ Tested (three granularities, four metrics) |
| 26 | Learning tracker supports goals, sessions and progress | ✔ Tested (goal editor, session logging, auto progress, filters, restart) |
| 27 | Quick Tools contains exactly three tools | ✔ Tested (note capture, breathing, screen break only) |
| 28 | Focus and reminder bells differ, ring three times / once, and offer ten choices each | ✔ Tested (ten pickers with previews, `glass` focus ×3 and `musicbox` reminder ×1 in the log, persistence across restart) |
| 29 | Focus shows a small draggable timer with pin and close while a session runs | ✔ Tested (auto-show, drag with saved position, pin/topmost, paused color, hide on close/reset, clean dock exit) |
| 30 | Tray icon can minimize, reopen, restart and exit the app cleanly | ✔ Tested (overflow icon, dark menu, work-area release on minimize, single-instance restart, icon removal on exit) |
| 31 | Dock returns after fullscreen videos without a restart and never covers them | ✔ Tested (watchdog release at +0.7 s, hidden for borderless 2560×1600 windows and Edge F11, work area 2560→2498 px automatically, tray restore guard) |

---

## 20. Test method notes

- UI automation via PowerShell + UIAutomation. The dock is found by window title
  "Right Dock"; module popups and the Settings window appear as owned windows nested in
  the dock's UIA tree, so window-scoped searches must attach directly to the HWND
  (`AutomationElement.FromHandle`). Tab buttons and module inputs expose `AutomationId`s
  (`TabModules`, `TabNotifications`, `TaskTitle`, `TaskDue`, `TaskSearch`, `ReminderTitle`,
  `ReminderDue`, `ReminderCustomDays`, `NoteTitle`, `NoteBody`, `NoteCapture`,
  `HabitName`, `HabitDescription`, `HabitTarget`, `HabitUnit`, `MedName`, `MedDosage`,
  `MedTimes`, `MedNotes`, `WeightInput`, `QuickNoteCapture`, `BreatheStartButton`,
  `ScreenBreakToggle`, `ScreenBreakRemindButton`, `LearningSearch`, `GoalTitle`,
  `SessionLogButton`, …); module list rows, chips and round checks
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
- **ComboBoxes expose names and `ExpandCollapsePattern`** — `ui.ps1` now has `expand`,
  `collapse`, `select` and `selected` actions. Dropdown items live in a separate titleless
  popup HWND and do **not** expose `SelectionItemPattern` in this WPF version, so a real
  mouse click at the item's `BoundingRectangle` is used to choose a value; the closed
  combo's `SelectionPattern` reports the resulting selection.
- **`TogglePattern.Toggle()` is not a real click** — it flips `IsChecked` visually but
  does not raise WPF `Click`, so handlers (priority/repeat/filter chips, round
  checkboxes) do not run. Use real mouse clicks (`SetCursorPos` + `mouse_event`) or
  `InvokePattern` where applicable.
- **The tray icon is outside the dock's UIA tree** — it lives in the notification area
  (Windows 11 puts new icons in the overflow). Open it with
  `clickglobal -Name "Show Hidden Icons"`; the icon's button is named "Right Dock" with
  class `SystemTray.NormalButton`. The WinForms menu is a separate titleless top-level
  HWND: `rightclickxy` opens it and its items are located by name globally. Work-area
  changes are read from `Screen.PrimaryScreen.WorkingArea` after `SetProcessDPIAware`.
- **Fullscreen suppression is tested with `tools/simulate-fullscreen.ps1`** — it posts the
  registered `RightDock_AppBarCallback` message with `lParam = 1` (on) or `0` (off) to the
  dock HWND. A genuine fullscreen check needs a borderless window covering the monitor
  (a WinForms form with `FormBorderStyle=None` at 0,0 2560×1600, or Edge/Chrome F11).
  While such a window is up the shell hides the taskbar, so tray clicks are impossible;
  send F11 again or close the window and watch `rightdock.log` for "Fullscreen app ended;
  releasing dock suppression." A maximized window has `WS_CAPTION`, so it must not
  suppress the dock.
- **The focus mini timer is its own top-level window** titled "Focus timer", outside the
  dock's UIA tree, with automation IDs `MiniTimerTime`, `MiniTimerPinButton` and
  `MiniTimerCloseButton`. Drag checks use `SetCursorPos` + `mouse_event` while
  `DragMove` runs, then read `settings.json`; pin state is confirmed through
  `WS_EX_TOPMOST` (`GetWindowLong` -20, `0x8`) and the saved `MiniTimerPinned` value.
- **The dock must be revealed before clicking** — hover the right edge, wait for the
  slide-in, then compute points from `GetWindowRect`.
- Geometry verified through Win32 (`GetWindowRect`, `SystemParameters.WorkArea`) rather
  than visual estimates. Note that non-DPI-aware processes receive virtualized
  coordinates (125 % scaling → 80 % values), so UIA queries must set `SetProcessDPIAware`
  before computing click points.
- Persistence verified by closing/restarting the app and re-reading `pins.json`,
  `settings.json`, the Run registry value, and the productivity tables (`Tasks`,
  `Reminders`, `Notes`, `FocusSessions`, `Habits`, `HabitLogs`, `Medications`,
  `MedicationSchedules`, `MedicationLogs`, `HealthLogs`, `BreathingSessions`,
  `LearningGoals`, `LearningSessions`) directly from `rightdock.db`
  with Python's `sqlite3` module.
- Screen-break scheduling was exercised with both the persisted interval and a temporary
  1-minute interval, capturing the toast with `tools/watch-toast.ps1`; module config was
  inspected in `ModuleSettings.ConfigJson` before and after restart.
- Phase 5 test data (quick note, learning goals/sessions, breathing sessions and the
  quicktools module state row) was deleted from the live database before release.
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
- Helper scripts in `tools/`: `ui.ps1`
  (list/shot/elements/invoke/set/click/clickxy/keys/expand/collapse/select/selected),
  `watch-toast.ps1` (toast capture/click), `close-dock.ps1` (clean shutdown between runs),
  `measure.ps1` (CPU/RAM/handle sample), `stress-ui.ps1` (repeat open/close with leak
  metrics), `simulate-fullscreen.ps1` (AppBar fullscreen callback on/off),
  `publish.ps1` (release/portable ZIP/installer) and `make-icon.ps1`.
- Phase 6 backup/restore tests used the real dialogs: `CreateBackupButton` →
  `SendKeys` path + Enter, `RestoreBackupButton` → path + Enter → message-box Enter. The
  archive was re-opened with Python `zipfile`/`sqlite3` to check entries, journal mode,
  `user_version`, `integrity_check` and row counts. Restore was proven by adding a note,
  restoring, and confirming the post-restart process ID, note count, safety copy folder
  and log lines.
- Migration was tested with a real v1 fixture (current database with `BreathingSessions`
  dropped, `SnoozeUntilUtc` removed and `user_version=1`), placed at the live path while
  the app was stopped; the log and resulting schema were checked after startup and the
  original database restored afterwards.
- Sleep/resume, clock and display changes were simulated by `PostMessage` to the dock
  HWND (`0x0218/0x0012`, `0x001E`, `0x007E`) and verified through log lines and an
  unchanged `GetWindowRect` reservation.
- The temporary Smoke Test module used for end-to-end validation was deleted before
  release, along with its `ModuleSettings` row.
