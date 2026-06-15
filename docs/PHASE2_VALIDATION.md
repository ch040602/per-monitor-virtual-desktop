# Phase 2 validation notes

This document records the current Windows validation state for Phase 2.

## Automated/source checks

- Source tree normalized from the Phase 1 zip.
- Added debounced WinEvent listener and verified references to its Win32 constants.
- Added display-change refresh path through `SystemEvents.DisplaySettingsChanged`.
- Added native parking fallback to logical hide/show and verified `HiddenByPmvd` state references.
- Added diagnostics/rescue commands and verified command enum references.
- Added config editor form and tray menu integration.
- Validated `examples/config.json` with Python JSON parser.
- Ran a source brace-balance check across C# files.

## Windows validation performed on 2026-06-15

Environment:

- Windows `10.0.26200`
- .NET SDK `9.0.203`
- .NET 8 runtime and Windows Desktop runtime installed

Commands:

```powershell
dotnet restore .\PerMonitorVD.sln
dotnet build .\PerMonitorVD.sln -c Release
.\src\PerMonitorVD\bin\x64\Release\net8.0-windows10.0.19041.0\PerMonitorVD.exe
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe status
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe diagnostics
```

Results:

- `dotnet restore` passed after adding repo-local `NuGet.config`.
- `dotnet build .\PerMonitorVD.sln -c Release --no-restore` passed with 0 warnings and 0 errors.
- `PerMonitorVD.exe` launched and stayed running after fixing `AsyncDebouncer` CTS disposal.
- `pvdctl status` returned full multi-line status.
- `pvdctl diagnostics` returned `OK diagnostics C:\Users\hcslab_523\AppData\Local\PerMonitorVD\reports\diagnostics-20260615-160212.txt`.
- The diagnostics report listed `[PMVD] ACTIVE` and parking desktops for DISPLAY1, DISPLAY2, and DISPLAY3.
- `pvdctl switch --monitor DISPLAY2 --next` changed only Monitor 1 from `DISPLAY2-W1` to `DISPLAY2-W2`; Monitor 2 and Monitor 3 remained on their current workspaces.
- `pvdctl switch --monitor DISPLAY2 --prev` returned Monitor 1 to `DISPLAY2-W1`.

Collect with every validation run:

- The `OK diagnostics <path>` report returned by `pvdctl diagnostics`.
- `%LOCALAPPDATA%\PerMonitorVD\PerMonitorVD.log`, because diagnostics records the log path but does not embed log contents.

## Recommended manual smoke test

1. Run the tray app.
2. Use Task View to confirm `[PMVD] ACTIVE` and `[PMVD] DISPLAY*-W*` parking desktops exist.
3. Open Notepad on monitor 1 and a browser on monitor 2.
4. Put the pointer on monitor 1 and press `Ctrl+Alt+Shift+Right`.
5. Confirm monitor 1 changes workspace while monitor 2 remains unchanged.
6. Press `Ctrl+Alt+Shift+Left` and confirm monitor 1 returns.
7. Run `pvdctl diagnostics` and preserve the returned report path.
8. Configure Logitech gestures to send:
   - Gesture left -> `Ctrl+Alt+Shift+Left`
   - Gesture right -> `Ctrl+Alt+Shift+Right`
   - Gesture up -> `Ctrl+Alt+Shift+Up`
   - Gesture down -> `Ctrl+Alt+Shift+Down`
9. Drag a visible window to the other monitor and confirm it joins that monitor's current workspace.
10. Run `pvdctl rescue-all` if any window disappears during testing.
11. If native VD movement fails after a Windows update, set `SwitchingStrategy` to `logicalHideShow` and retest.
