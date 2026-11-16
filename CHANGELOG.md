# Changelog

All notable changes to RightDock are recorded here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions use
semantic versioning. Development is organized in phases (see
`IMPLEMENTATION_PROGRESS.md`).

## [Unreleased]

### Added
- `PROJECT_ARCHITECTURE.md`, `IMPLEMENTATION_PROGRESS.md`, `CHANGELOG.md`.
- Git repository with `.gitignore` (build outputs excluded) published to
  `https://github.com/xs2ilyas/RightDock-.git`.

### Planned
- Phase 1 core infrastructure: module manager, popup host, SQLite data service,
  centralized scheduler, notification service.

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
