# per-monitor-virtual-desktop

Windows per-monitor virtual desktop control. The app is named **PerMonitorVD** and the CLI is **pvdctl**.

[한국어](README.ko.md) | [简体中文](README.zh-CN.md)

PerMonitorVD is a Windows prototype for **per-monitor virtual desktop UX** using a **native virtual desktop parking** strategy.

It does **not** make Windows Shell expose multiple simultaneously active native virtual desktops. Instead, it keeps the user on one active composite Windows virtual desktop and moves inactive monitor-workspace windows into real Windows virtual desktops used as parking storage.

## Runtime model

```text
[PMVD] ACTIVE
  Monitor 1 current workspace windows
  Monitor 2 current workspace windows

[PMVD] DISPLAY1-W1 (1)
  parked windows for Monitor 1 workspace 1, when inactive

[PMVD] DISPLAY1-W2 (2)
  parked windows for Monitor 1 workspace 2, when inactive

[PMVD] DISPLAY2-W1 (4)
  parked windows for Monitor 2 workspace 1, when inactive
```

## Features

- WinEvent-based refresh for foreground, show/hide, move/resize, minimize and cloak/uncloak events.
- Display topology refresh when monitors are attached, detached, or rearranged.
- Monitor identity migration by order and bounds when Windows device names change.
- Logical hide/show fallback when native virtual desktop parking fails for a window.
- Stricter shell/tool/zero-size window filtering.
- Tray config editor for `%LOCALAPPDATA%\PerMonitorVD\config.json`.
- Recovery commands: `return-active`, `rescue-all`, `diagnostics`, `refresh`.
- Diagnostics report export under `%LOCALAPPDATA%\PerMonitorVD\reports`.
- Home overlay for viewing each monitor's PMVD desktops and active apps.
- Drag app chips directly onto a desktop card to move that app to another PMVD workspace.
- Task View-like monitor lanes with workspace cards and per-monitor maximum managed-window limits.
- Best-effort Windows taskbar setting so the taskbar shows apps from all native virtual desktops.

## Projects

```text
src/PerMonitorVD
  Tray app, hotkey listener, window tracker, native VD parking engine.

src/pvdctl
  CLI client. Sends commands to the tray app through a named pipe.
```

## Requirements

- Windows 10 20H1 build 19041 or later, preferably Windows 11.
- .NET 8 SDK.
- x64 build.
- NuGet package: `Slions.VirtualDesktop` 6.9.2.

`Slions.VirtualDesktop` uses undocumented Windows virtual desktop interfaces, so Windows build changes can break the native VD bridge. The code isolates this behind `IVirtualDesktopBridge` so a future bridge can replace it.

## Build

```powershell
cd PerMonitorVD

dotnet restore .\PerMonitorVD.sln
dotnet build .\PerMonitorVD.sln -c Release
```

The repo includes `NuGet.config` so restore uses nuget.org even when the machine-level NuGet sources only contain Visual Studio offline packages.

Run the tray app:

```powershell
.\src\PerMonitorVD\bin\x64\Release\net8.0-windows10.0.19041.0\PerMonitorVD.exe
```

Run CLI commands:

```powershell
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe status
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe switch --monitor mouse --next
```

## Default hotkeys

```text
Ctrl + Alt + Shift + Left    current mouse monitor: previous workspace
Ctrl + Alt + Shift + Right   current mouse monitor: next workspace
Ctrl + Alt + Shift + 1/2/3   current mouse monitor: switch to workspace 1/2/3
Ctrl + Alt + Shift + Up      move focused window to previous workspace
Ctrl + Alt + Shift + Down    move focused window to next workspace
Ctrl + Alt + Shift + Home    open PMVD Home
Ctrl + Alt + Shift + R       repair state
```

Recommended Logitech mapping:

```text
Gesture left   -> Ctrl + Alt + Shift + Left
Gesture right  -> Ctrl + Alt + Shift + Right
Gesture up     -> Ctrl + Alt + Shift + Up
Gesture down   -> Ctrl + Alt + Shift + Down
```

## CLI commands

```powershell
pvdctl switch --monitor mouse --next
pvdctl switch --monitor mouse --prev
pvdctl switch --monitor mouse --workspace 2
pvdctl move-window --monitor mouse --workspace 2
pvdctl move-window --monitor mouse --next
pvdctl refresh
pvdctl repair
pvdctl return-active
pvdctl rescue-all
pvdctl diagnostics
pvdctl home
pvdctl pause
pvdctl resume
pvdctl status
```

`pvdctl diagnostics` prints `OK diagnostics <path>`. Keep that returned report path with `%LOCALAPPDATA%\PerMonitorVD\PerMonitorVD.log` when recording a test result.

## Config/state/report location

```text
%LOCALAPPDATA%\PerMonitorVD\config.json
%LOCALAPPDATA%\PerMonitorVD\state.json
%LOCALAPPDATA%\PerMonitorVD\PerMonitorVD.log
%LOCALAPPDATA%\PerMonitorVD\reports\diagnostics-*.txt
```

## Important limitations

- Do not use Windows Task View directly for the `[PMVD] ...` parking desktops during normal use.
- `Win+Ctrl+Left/Right` override is disabled by default. Enable it in `config.json` only after validating the normal custom hotkeys.
- PerMonitorVD sets Windows' taskbar virtual desktop filter to show windows from all desktops on a best-effort basis. If Windows resets it, set Settings -> Personalization -> Taskbar -> Taskbar behaviors -> "On the taskbar, show all open windows" to "On all desktops".
- Administrator-elevated windows usually require the tray app to run elevated too.
- Some UWP/WinUI, full-screen game, overlay, or tool windows may need `ignore` or `sticky` rules.
- If windows disappear during testing, use tray menu `Return to active desktop`, then `Rescue all windows`, then `Repair state`.

## Home window

Open PMVD Home with `Ctrl+Alt+Shift+Home`, `pvdctl home`, or the tray menu. It shows each monitor, its PMVD desktops, and the apps assigned to each desktop.

From Home you can:

- View monitor lanes and desktop cards in a layout similar to Windows Task View.
- Drag an app chip onto another desktop card to move that app immediately.
- Set each monitor's maximum managed-window count. `0` means unlimited.
- Refresh the view after moving windows manually.

When a monitor reaches its maximum managed-window count, PMVD stops automatically tracking newly discovered windows on that monitor. Explicit moves into a full monitor are skipped and logged.

## Development sequence

1. Build and run `PerMonitorVD.exe`.
2. Confirm that `[PMVD] ACTIVE` and parking desktops are created.
3. Open Notepad on monitor 1 and a browser on monitor 2.
4. Put the mouse on monitor 1 and press `Ctrl+Alt+Shift+Right`.
5. Confirm only monitor 1 changes workspace and monitor 2 remains unchanged.
6. Press `Ctrl+Alt+Shift+Left` and confirm monitor 1 returns.
7. Run `pvdctl diagnostics` and save the returned report path.
8. Open PMVD Home with `Ctrl+Alt+Shift+Home` and verify that app chips can be dragged onto another desktop card.
9. Configure Logitech gestures to send the default Left/Right/Up/Down hotkeys.
10. Open tray menu -> `Edit config` and tune window rules if needed.
11. Enable `EnableWinCtrlOverride` only after the normal custom-hotkey path is stable.
