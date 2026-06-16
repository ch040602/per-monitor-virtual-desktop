# Git upload metadata

## Repository description

Per-monitor virtual desktop control for Windows. PMVD switches workspaces per display, parks inactive windows on native virtual desktops, includes a Task View-like Home overlay, and adds a tray jump menu for apps on other PMVD desktops.

## Suggested topics

```text
windows
virtual-desktop
per-monitor
winforms
dotnet
productivity
window-manager
logitech
taskbar
native-windows
```

## Suggested release tag

```text
v0.2.2
```

## Suggested release title

```text
PerMonitorVD v0.2.2 - system tray startup
```

## Suggested commit message

```text
Add system tray startup registration

- add Ctrl+Alt+Shift+Home and tray Home entry
- show each monitor's PMVD desktops and active apps
- move apps by dragging app chips onto desktop cards
- jump to apps on other PMVD desktops from the tray menu
- add pvdctl activate-window
- run as a tray resident app without duplicate-instance popups
- register the current exe under HKCU Run when StartWithWindows is enabled
- add Task View-like monitor desktop cards
- add per-monitor maximum managed-window limits
- keep taskbar configured for all virtual desktop windows
- add Korean and Simplified Chinese README files
- add GitHub upload metadata
```

## Release notes

```markdown
## Highlights

- Per-monitor workspace switching using native virtual desktop parking.
- PMVD Home overlay for viewing each monitor's desktops and active apps.
- Drag-and-drop app movement between desktop cards.
- Tray `Other desktop apps` menu that jumps to an app's PMVD desktop.
- `pvdctl activate-window --hwnd <HWND>` for scriptable app jumps.
- System tray resident behavior with `Start with Windows` toggle.
- Current-user Windows startup registration via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Task View-like monitor lanes with desktop cards.
- Per-monitor maximum managed-window limits from Home.
- `Ctrl+Alt+Shift+Home`, tray menu Home entry, and `pvdctl home`.
- Self-contained Windows x64 release zip with `PerMonitorVD.exe` and `pvdctl.exe`.
- Diagnostics export with report path output.
- Best-effort taskbar configuration so Windows shows apps from all native virtual desktops.
- English default README plus Korean and Simplified Chinese translations.

## Validation

- `dotnet restore .\PerMonitorVD.sln`
- `dotnet build .\PerMonitorVD.sln -c Release`
- `pvdctl status`
- `pvdctl diagnostics`
```
