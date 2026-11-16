# Changelog

All notable changes to RightDock are recorded here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions use
semantic versioning. Development is organized in phases (see
`IMPLEMENTATION_PROGRESS.md`).

## [Unreleased]

### Planned
- Phase 4 expanded workspace: dashboard, daily overview, notes manager, calendar/planner,
  statistics.

Earlier releases (1.0.0–1.2.0: dock shell, pins, auto-hide, 181-icon line set, icon
picker, settings popup) are described in `IMPLEMENTATION_PROGRESS.md`.

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
