# SteamWatch WinUI SPEC

## 1. Product Goal
SteamWatch WinUI is a Windows-native Steam playtime monitor and limiter for personal self-control and time statistics. The app runs mainly from the system tray, monitors Steam games locally, records playtime, warns near limits, and can optionally force-close games after configured limits are exceeded.

## 2. Target Platform
- Windows 10/11 x64.
- C#/.NET with WinUI 3 and Windows App SDK for the app shell.
- ZIP portable distribution is the primary release form.
- Data compatibility with the old Python version is not required.

## 3. Core Features
- Detect Steam installation path from Windows registry and common install locations.
- Read Steam local cache files for user/game metadata and local icons.
- Monitor Steam and Steam-launched game processes.
- Track playtime incrementally while games are running.
- Support per-game daily limits and per-game weekly limits.
- Support global daily total limits and global weekly total limits.
- Weekly limits use Monday through Sunday as the natural week.
- Reminder escalation thresholds: 70%, 85%, 95%, and 100%.
- Enforcement mode is configurable per game: NotifyOnly or ForceClose.
- Global limit enforcement is configurable and defaults to NotifyOnly.
- ForceClose uses a warning countdown before closing, default 60 seconds.
- Closing the main window is configurable: minimize to tray or exit app. Default is minimize to tray.
- Startup with Windows is configurable on/off.
- Manual export supports JSON for backup/restore-style full data and CSV for spreadsheet analysis.

## 4. Data Rules
- Playtime is stored as daily per-game minute records.
- Weekly totals are derived from daily records instead of being stored as independent mutable state.
- A week starts on Monday and ends on Sunday.
- Limit rules include scope, period, minutes, and enforcement mode.
- Runtime data is stored under the app data directory for the portable app.

## 5. Safety Rules
- ForceClose must save the active session before attempting process termination.
- ForceClose first attempts graceful close, then terminates the process tree if still running.
- All detection, reminder, save, export, startup, and force-close failures must be logged.
- ForceClose must be user-configurable and not silently enabled for every game.

## 6. UX Requirements
- First screen is the application itself, not a landing page.
- Main views: Games, Statistics, Settings, Activity Log.
- Tray menu: Open SteamWatch, Pause Monitoring, Settings, Exit.
- Settings must expose close behavior, startup with Windows, reminders, sound, and global limits.
