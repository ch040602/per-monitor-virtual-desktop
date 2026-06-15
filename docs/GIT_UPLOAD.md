# Git upload metadata

## Repository description

Per-monitor virtual desktop control for Windows. PMVD switches workspaces per display, parks inactive windows on native virtual desktops, and includes a Task View-like Home overlay for drag-and-drop app movement and per-monitor window caps.

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
v0.2.0-phase2
```

## Suggested release title

```text
PerMonitorVD Phase 2 - per-monitor switching, diagnostics, and drag-and-drop Home
```

## Suggested commit message

```text
Add drag-and-drop PMVD Home and multilingual docs

- add Ctrl+Alt+Shift+Home and tray Home entry
- show each monitor's PMVD desktops and active apps
- move apps by dragging app chips onto desktop cards
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
- Task View-like monitor lanes with desktop cards.
- Per-monitor maximum managed-window limits from Home.
- `Ctrl+Alt+Shift+Home`, tray menu Home entry, and `pvdctl home`.
- Diagnostics export with report path output.
- Best-effort taskbar configuration so Windows shows apps from all native virtual desktops.
- English default README plus Korean and Simplified Chinese translations.

## Validation

- `dotnet restore .\PerMonitorVD.sln`
- `dotnet build .\PerMonitorVD.sln -c Release`
- `pvdctl status`
- `pvdctl diagnostics`
```
