# Architecture

## Main components

```text
Input
  GlobalHotkeyService
  WinCtrlOverrideKeyboardHook
  PipeCommandServer

Core
  CommandRouter
  WorkspaceEngine
  MonitorResolver
  WorkspaceRuntimeState

Native
  SlionsVirtualDesktopBridge
  WindowTracker
  WindowInspector
  WindowPlacementStore
  WindowEventListener

Configuration
  ConfigStore
  ConfigEditorForm

Overlay
  TrayIconService
  OverlayService

Utilities
  AsyncDebouncer
  AppPaths
  Log
  SingleInstanceGuard
```

## Switch transaction

```text
SwitchWorkspace(monitor, target)
  1. Ensure current native desktop is [PMVD] ACTIVE.
  2. Refresh visible top-level windows.
  3. Park old monitor/workspace windows into the old slot's native parking VD.
     - If native parking fails and FallbackToLogicalHideShow is true, hide the window.
     - If SwitchingStrategy is logicalHideShow, skip native parking and hide directly.
  4. Update the monitor's current workspace index.
  5. Restore target slot windows from target parking VD into [PMVD] ACTIVE.
     - If a window was hidden by PMVD, show it instead.
  6. Restore placement.
  7. Save state and show overlay.
```

## Native VD abstraction

`IVirtualDesktopBridge` is intentionally small:

```csharp
NativeDesktopInfo GetCurrentDesktop();
IReadOnlyList<NativeDesktopInfo> GetDesktops();
NativeDesktopInfo CreateDesktop(string name);
void RenameDesktop(Guid desktopId, string name);
void SwitchToDesktop(Guid desktopId);
Guid GetWindowDesktopId(IntPtr hwnd);
bool MoveWindowToDesktop(IntPtr hwnd, Guid desktopId);
```

The current implementation uses `WindowsDesktop.VirtualDesktop` from `Slions.VirtualDesktop`.
If that package breaks on a Windows build, replace only `SlionsVirtualDesktopBridge` or temporarily set:

```json
{
  "SwitchingStrategy": "logicalHideShow"
}
```

## WinEvent refresh path

```text
Windows event
  -> WindowEventListener
  -> AsyncDebouncer
  -> WorkspaceEngine.RefreshAsync("window-event")
  -> WindowTracker.RefreshVisibleWindows()
```

This avoids doing expensive scans inside the WinEvent callback itself.

## Display topology path

```text
SystemEvents.DisplaySettingsChanged
  -> WorkspaceEngine.RefreshAsync("display-change")
  -> PrepareMonitorWorkspaces()
  -> optional monitor identity migration by OrderIndex
```

When a monitor device name changes but its order remains stable, PerMonitorVD migrates the previous `MonitorState` and rewrites workspace IDs/window records.

## State rules

- Runtime desktop identity is GUID-based, not index-based.
- Window identity is HWND-based and cleaned by `repair` if dead.
- Newly visible windows are assigned to the current workspace of their current monitor.
- Dragging a visible window to another monitor moves it to that monitor's current workspace.
- `HiddenByPmvd` means native parking failed or `logicalHideShow` intentionally hid the window.
- `rescue-all` tries to move all tracked windows back to `[PMVD] ACTIVE` and shows any logically hidden windows.
