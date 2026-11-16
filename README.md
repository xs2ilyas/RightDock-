# Right Dock

A native, single-process productivity dock for the right edge of Windows. Right Dock reserves a slim strip of the desktop next to the taskbar area, keeps your files and twelve built-in productivity modules one click away, and gets out of the way with auto-hide.

Built with WPF on .NET 5 — no WebView, no Electron, no background services, no network calls.

> Current release: **1.9.0** — dock shell, twelve modules, local SQLite storage, toggleable notifications, and ten synthesized bell sounds.

## Highlights

- **True AppBar reservation** — the dock registers with the Windows shell (`ABM_NEW` / `QUERYPOS` / `SETPOS`), so maximized windows stop at the dock edge. When auto-hide slides the dock out, the reserved area is released.
- **Always-on-top, non-activating** — `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`; the dock never steals focus and has no taskbar button.
- **Auto-hide with edge reveal** — hover the rightmost 5 px of the screen to slide the dock in; it slides away after the cursor leaves. Fullscreen apps hide it automatically, menus and dialogs pause it.
- **Pins with icons** — add `.exe`, `.lnk`, `.bat`, `.cmd`, `.msc` and `.appref-ms` files (multi-select), launch with a left click, remove from the right-click menu. Choose from **181 built-in Lucide outline glyphs** or drop your own `*.svg` into `%APPDATA%\RightDock\icons`.
- **Twelve productivity modules** — focus sessions, tasks, reminders, notes, habits, medication, health, dashboard, calendar, statistics, quick tools and learning. Compact views sit beside the dock; every module opens a full expanded window.
- **In-app toasts** — focus completion and reminders raise a clickable toast that opens the dashboard. No Windows notification registration is required.
- **Ten bell sounds** — focus completion rings three times and reminders ring once, each using its own selectable tone. All ten WAVs are synthesized on the device at runtime; no audio files or downloads.
- **Local-first data** — SQLite with WAL and versioned migrations, backup/restore as a zip archive, and a restart-safe staged restore. Nothing leaves the machine.
- **Single instance** — a second launch exits immediately; the restore flow relaunches the app with `--restart`.

## Modules

| Module | Icon | What it does |
| --- | --- | --- |
| **Dashboard** | `layout-dashboard` | Daily overview built from all stores: focus, tasks, habits, medication, next reminder, notes and health, plus a 2×3 widget grid and quick actions. |
| **Focus** | `timer` | 15/25/45/60-minute sessions with pause/resume, task linking, 7-day history, and a triple bell + toast on completion. |
| **Tasks** | `clipboard-list` | Due dates, categories, projects, priorities, and once/daily/weekly/monthly recurrence with Today/Upcoming/Completed/All views and search. |
| **Reminders** | `bell` | One-time, daily, weekly and custom-day reminders with notes, snooze (5/10/30/60 min), history, and a single bell + toast when due. |
| **Notes** | `notebook-pen` | Quick capture, pinning, categories, search, archive/restore, and one-click conversion into a task or reminder. |
| **Habits** | `target` | Ten seeded starter habits, custom boolean/counter/duration habits, streaks, and a last-7-days matrix. |
| **Medication** | `pill` | Medications with multiple dose times and weekday/weekend rules, pending/taken/skipped/snoozed/missed states, and scheduled dose reminders. |
| **Health & Lifestyle** | `heart-pulse` | Eight daily metrics (water, meals, sleep, activity, vegetables, fruits, relaxation, weight) with ring views and 7-day trends. |
| **Calendar** | `calendar-days` | Monday-start month planner that aggregates tasks, reminders, medication doses and focus sessions, with a per-day agenda and quick add. |
| **Statistics** | `chart-column` | Daily/weekly/monthly charts for focus minutes, completed tasks, habit rate and medication adherence. |
| **Quick Tools** | `sparkles` | Deliberately exactly three tools: quick note, guided breathing (Box, 4-7-8, Calm, Coherent) and scheduled screen breaks. |
| **Learning** | `graduation-cap` | Goals with categories, units and due dates, progress tracking, and logged sessions that advance minute/hour goals automatically. |

Every module can be hidden from the dock or disabled in Settings without deleting its data.

## Requirements

- Windows 10 version 1809 (build 17763) or newer, or Windows 11, 64-bit.
- [.NET 5 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/5.0) for the published build (the default publish is framework-dependent).
- **To build:** .NET 5 SDK (5.0.4xx or newer).
- **Optional:** [Inno Setup 6](https://jrsoftware.org/isinfo.php) for building the setup executable.

## Build and run

```powershell
git clone https://github.com/xs2ilyas/RightDock-.git
cd RightDock-
dotnet build -c Release
dotnet run -c Release
```

Produce a portable, ready-to-run folder and zip (close Right Dock first — the published files are locked while it runs):

```powershell
powershell -ExecutionPolicy Bypass -File tools\publish.ps1
publish\RightDock.exe
```

The script writes `publish\` and `dist\RightDock-<version>-win-x64.zip`. Add `-SelfContained` to bundle the runtime, or `-Installer` to compile the Inno Setup per-user installer into `dist\RightDock-<version>-setup.exe`.

## Using the dock

- **Add a pin** — click `+` at the bottom of the dock, pick one or more executables or shortcuts. Right-click a pin for **Remove from dock**, or click the pencil badge to change its icon.
- **Change the icon** — the picker offers 181 categories icons, live search by name or category, a **This app** reset, and any SVGs you place in `%APPDATA%\RightDock\icons`.
- **Open a module** — left-click its icon; the compact view opens beside the dock, and the expand button opens the full window. Toasts open the dashboard when clicked.
- **Auto-hide** — hover the right edge of the screen to reveal the dock. Turn it off in Settings → General if you prefer it always visible.
- **Settings** — click the gear. Six pages: **General** (auto-hide), **Appearance** (icon color presets, rainbow swatch, native color picker), **Modules** (enable/hide), **Notifications** (alerts plus focus/reminder bell pickers), **Data** (database info, backup, restore, integrity check) and **Startup** (run at Windows sign-in).

## Focus and reminder bells

Open **Settings → Notifications → Sounds**:

- **Focus completion** rings **three times** using the selected focus bell (default **Classic**).
- **Reminders** ring **once** using the selected reminder bell (default **Soft**).
- Each picker offers ten tones: Classic, Soft, Glass, Marimba, Temple, Digital, Harp, Music Box, Resonant and Bright, with a **Preview** button for every tone.
- Previews play even when notifications are off; live bells follow the notifications toggle.
- The WAVs are generated in memory (44.1 kHz PCM16) on first use and cached, so an idle session holds no audio buffers.
- Bells play on the Windows default output device.

## Data and privacy

All data stays under `%APPDATA%\RightDock`:

| Path | Contents |
| --- | --- |
| `rightdock.db` | SQLite database (WAL mode) with all module data |
| `pins.json` | Pinned shortcuts and their icons |
| `settings.json` | Auto-hide, icon color, notifications, focus/reminder sounds |
| `icons\` | Optional custom SVG icons |
| `backups\` | Zip backups and pre-restore safety copies |
| `logs\` | Append-only UTC logs, rotated at 1 MB |
| `restore-staging\`, `restore.pending` | Short-lived restore state |

There is no telemetry, no account, and no network access anywhere in the application.

**Backup** creates one zip with the database, pins, settings and custom icons. **Restore** validates the archive, keeps a safety copy of the current database, stages the files, and restarts Right Dock with `--restart` to apply them. Safely stop the app from Settings instead of killing the process.

## Project structure

```
RightDock/
├─ App.xaml(.cs)              Application bootstrap, single instance, --restart
├─ MainWindow.xaml(.cs)       Dock surface, pins, auto-hide, popup positioning
├─ AppBarDock.cs              AppBar registration and work-area reservation
├─ AppSettings.cs             settings.json load/save with validation
├─ PinStore.cs                pins.json load/save
├─ IconCatalog.cs             Built-in Lucide geometry catalog
├─ IconRepository.cs          Built-in + custom SVG geometry resolution
├─ IconPickerWindow.xaml(.cs) 440×640 icon picker
├─ SettingsWindow.xaml(.cs)   Six-page settings popup
├─ StartupRegistration.cs     HKCU Run entry for run-at-startup
├─ DockServices.cs            Composition root (settings, data, scheduler, …)
├─ Data/                      SQLite database, stores, backup/restore service
├─ Services/                  Scheduler, notification service, logging, bell synthesis/playback
├─ Modules/                   Module manager, popup host, shared theme, twelve modules
├─ Resources/                 App icon and shared assets
├─ installer/RightDock.iss    Inno Setup 6 per-user installer script
└─ tools/                     Publish, UIA testing, profiling, stress, icon helpers
```

## Architecture

- **One process, one window hierarchy.** `App` builds `DockServices`, which owns settings, the SQLite database, the scheduler, notifications, the sound service and the module manager. Everything is disposed on exit.
- **Modules are plugins in-process.** `IProductivityModule` / `ProductivityModule` define identity and lifecycle (`Initialize`, `Activate`, `Deactivate`) plus factories for the compact view, expanded view and settings view. Modules are registered in `ModuleManager` and lazily initialized when enabled or opened.
- **Scheduling is timer-based and idle-friendly.** The scheduler holds one key per pending item and does no work when nothing is due. Module timers only exist while a view is loaded or a session is running.
- **Storage is SQLite with migrations.** `PRAGMA user_version` drives schema upgrades; `PRAGMA foreign_keys=ON`, WAL and a busy timeout keep concurrent module access safe.
- **No placeholder controls.** Every button in the app performs a real, tested action.

More detail:

- [PROJECT_ARCHITECTURE.md](PROJECT_ARCHITECTURE.md) — layering, data model, OS-event handling, resource baselines and module contracts.
- [IMPLEMENTATION_PROGRESS.md](IMPLEMENTATION_PROGRESS.md) — phase-by-phase implementation notes, verification evidence and limitations.
- [CHANGELOG.md](CHANGELOG.md) — per-release Added/Changed/Fixed/Verified entries.

## Performance

Right Dock is designed to stay small while idle. Measured on Windows 11 (2560×1600 @125 %):

| Build | Working set | Private | CPU | Handles / threads |
| --- | --- | --- | --- | --- |
| 1.4.0 (four modules) | ~123 MB | ~71 MB | 0 % over 30 s | — |
| 1.8.0 (stress run) | 150.4 MB | 98.9 MB | 0.016 s over 10 s | 476 / 9 |
| 1.9.0 (twelve modules, idle) | 109.0 MB | 62.2 MB | 0.016 s over 10 s | 463 / 17 |

A 27-cycle UI stress run across all modules plateaued at roughly 111 MB private / 163 MB working set with flat handle, GDI and USER counts, confirming popups are reclaimed.

## Development tools

| Script | Purpose |
| --- | --- |
| `tools\publish.ps1` | Publish win-x64, create the portable zip, optionally build the installer. |
| `tools\ui.ps1` | UI Automation driver: list windows, screenshot, dump elements, invoke/click/set/select, send keys. |
| `tools\measure.ps1` | Sample CPU, working set, private bytes, handles and threads. |
| `tools\stress-ui.ps1` | Repeated open/close cycles across modules for leak checks. |
| `tools\watch-toast.ps1` | Wait for a toast window, capture it, optionally click it. |
| `tools\close-dock.ps1` | Gracefully close a running instance. |
| `tools\make-icon.ps1`, `tools\gen-icons.js` | Regenerate the application icon and the built-in icon catalog. |

## Version history

| Version | Scope |
| --- | --- |
| 1.0.0 | Dock shell, AppBar reservation, pins |
| 1.1.0 | Auto-hide and edge reveal, 181-icon set, icon picker |
| 1.2.0 | Settings popup (auto-hide, icon color, startup) |
| 1.3.0 | Modules, SQLite, scheduler, notifications |
| 1.4.0 | Focus, Tasks, Reminders, Notes |
| 1.5.0 | Habits, Medication, Health & Lifestyle |
| 1.6.0 | Dashboard, Calendar, Statistics, Notes manager |
| 1.7.0 | Quick Tools, Learning |
| 1.8.0 | Hardening: backup/restore, OS-event handling, installer, profiling |
| 1.9.0 | Bell sounds for focus completion and reminders |

## Known limitations

- Bells play through the default output device only; the app does not choose a device.
- Focus, reminder and medication alerts appear as in-app toasts while Right Dock is running; there is no Windows toast/notification fallback when the app is closed.
- The dock targets the primary monitor on the right edge; it is not a multi-monitor-per-dock app.
- Published builds are win-x64 only.
- The installer requires Inno Setup 6 to build; it is not committed as a binary.

## Third-party notices

Bundled icons and dependencies are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): Lucide (ISC), Feather (MIT), Microsoft.Data.Sqlite (MIT) and SQLite (public domain). The ten bell tones are synthesized by the application itself.

## License

No license file is currently included in this repository. Until one is added, the source is provided for viewing and personal use only; third-party assets remain under their own licenses listed above.