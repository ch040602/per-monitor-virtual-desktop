# Roadmap

## Phase 1 - Native parking MVP

Implemented:

- Tray app with global hotkeys.
- Named-pipe CLI client.
- Monitor resolver using mouse/foreground policy.
- Runtime state persisted in `%LOCALAPPDATA%\PerMonitorVD`.
- Parking native virtual desktops created per monitor/workspace.
- Top-level window tracking and filtering.
- Workspace switch transaction using native VD parking.
- Move focused window to workspace.
- Repair, pause, resume, status.
- Optional `Win+Ctrl+Left/Right` override hook.

## Phase 2 - Stability pass

Implemented in this package:

- WinEvent hooks for foreground, show/hide, move/resize, minimize, and cloak/uncloak refresh triggers.
- Debounced refresh to avoid heavy work inside WinEvent callbacks.
- Monitor topology refresh and workspace-state migration when device names/order change.
- Native parking fallback to logical hide/show for windows that cannot be moved to a native VD.
- Stricter taskbar, shell, owned-popup, tool-window, cloaked-window, and zero-size filtering.
- Tray config editor for live JSON validation/save/reload.
- Recovery commands: `refresh`, `return-active`, `rescue-all`, `diagnostics`.
- Diagnostics report export.

## Phase 3 - Power user features

- Per-workspace wallpaper/overlay naming.
- FancyZones layout restore integration.
- Per-app default workspace rules.
- Per-monitor workspace overview.
- Installer, auto-start, code signing.
- Bridge adapters for Windows build-specific native VD interface changes.
