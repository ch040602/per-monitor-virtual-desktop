using PerMonitorVD.Core;
using PerMonitorVD.Utilities;
using PerMonitorVD.Configuration;

namespace PerMonitorVD.Native;

public sealed class WindowTracker
{
    private readonly WindowInspector _inspector;
    private readonly WindowPlacementStore _placementStore;
    private readonly MonitorResolver _monitorResolver;
    private readonly IVirtualDesktopBridge _desktopBridge;
    private readonly WorkspaceRuntimeState _state;
    private readonly AppConfig _config;

    public WindowTracker(
        WindowInspector inspector,
        WindowPlacementStore placementStore,
        MonitorResolver monitorResolver,
        IVirtualDesktopBridge desktopBridge,
        WorkspaceRuntimeState state,
        AppConfig config)
    {
        _inspector = inspector;
        _placementStore = placementStore;
        _monitorResolver = monitorResolver;
        _desktopBridge = desktopBridge;
        _state = state;
        _config = config;
    }

    public IReadOnlyList<IntPtr> EnumTopLevelWindows()
    {
        var windows = new List<IntPtr>();
        Win32.EnumWindows((hwnd, _) =>
        {
            windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public void RefreshVisibleWindows()
    {
        foreach (var hwnd in EnumTopLevelWindows())
            RefreshSingleWindow(hwnd, allowCreate: true);

        RemoveDeadWindows();
    }

    public void HandleEventHint(WindowEventHint hint)
    {
        var key = HwndUtil.Format(hint.Hwnd);

        if (hint.EventId == Win32.EVENT_OBJECT_DESTROY)
        {
            if (_state.Windows.Remove(key))
                Log.Info($"Event removed destroyed window {key}");
            return;
        }

        RefreshSingleWindow(hint.Hwnd, allowCreate: true);
    }

    public WindowRecord? RefreshSingleWindow(IntPtr hwnd, bool allowCreate)
    {
        var info = _inspector.Inspect(hwnd);
        if (info is null || info.IsIgnored)
            return null;

        var hwndKey = HwndUtil.Format(hwnd);

        var nativeDesktopId = _desktopBridge.GetWindowDesktopId(hwnd);
        if (nativeDesktopId != Guid.Empty && _state.ActiveCompositeDesktopId != Guid.Empty && nativeDesktopId != _state.ActiveCompositeDesktopId)
        {
            if (_state.Windows.TryGetValue(hwndKey, out var parked))
                parked.CurrentNativeDesktopId = nativeDesktopId;
            return null;
        }

        var monitorKey = _monitorResolver.ResolveWindowMonitor(hwnd);
        var monitor = _state.Monitors.FirstOrDefault(m => string.Equals(m.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase))
                      ?? _state.Monitors.FirstOrDefault();
        if (monitor is null)
            return null;

        var currentSlot = monitor.Workspaces.ElementAtOrDefault(monitor.CurrentWorkspaceIndex);
        if (currentSlot is null)
            return null;

        if (!_state.Windows.TryGetValue(hwndKey, out var record))
        {
            if (!allowCreate)
                return null;

            if (IsMonitorAtWindowLimit(monitor.MonitorKey))
            {
                Log.Warn($"Skip tracking {hwndKey} {info.ProcessName}; monitor {monitor.MonitorKey} reached MaxManagedWindows={_config.GetMaxManagedWindows(monitor.MonitorKey)}.");
                return null;
            }

            record = new WindowRecord
            {
                Hwnd = hwndKey,
                WorkspaceId = currentSlot.WorkspaceId,
                MonitorKey = monitor.MonitorKey,
                LastPlacement = _placementStore.Capture(hwnd),
                CurrentNativeDesktopId = _state.ActiveCompositeDesktopId,
                HiddenByPmvd = false
            };
            _state.Windows[hwndKey] = record;
            Log.Info($"Track new window {hwndKey} {info.ProcessName} on {currentSlot.WorkspaceId}");
        }

        record.ProcessName = info.ProcessName;
        record.ClassName = info.ClassName;
        record.Title = info.Title;
        record.Sticky = info.IsSticky;
        record.Ignored = info.IsIgnored;
        record.LastSeen = DateTimeOffset.Now;
        record.CurrentNativeDesktopId = nativeDesktopId == Guid.Empty ? _state.ActiveCompositeDesktopId : nativeDesktopId;
        record.HiddenByPmvd = false;

        if (!record.Sticky)
        {
            // If the user drags a visible managed window to another monitor, it joins that monitor's current workspace.
            if (!string.Equals(record.MonitorKey, monitor.MonitorKey, StringComparison.OrdinalIgnoreCase))
            {
                var oldWorkspace = record.WorkspaceId;
                record.MonitorKey = monitor.MonitorKey;
                record.WorkspaceId = currentSlot.WorkspaceId;
                record.LastPlacement = _placementStore.Capture(hwnd);
                Log.Info($"Window {hwndKey} moved from {oldWorkspace} to monitor {monitor.MonitorKey}, workspace {currentSlot.WorkspaceId}");
            }
        }

        return record;
    }

    public WindowRecord? GetForegroundManageableWindow()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        var info = _inspector.Inspect(hwnd);
        if (info is null || info.IsIgnored)
            return null;

        var key = HwndUtil.Format(hwnd);
        if (_state.Windows.TryGetValue(key, out var existing))
            return existing;

        return RefreshSingleWindow(hwnd, allowCreate: true);
    }

    public bool IsAlive(WindowRecord record) => _inspector.IsAlive(record.HwndPtr);

    public WindowPlacementSnapshot? CapturePlacement(IntPtr hwnd) => _placementStore.Capture(hwnd);

    public void RestorePlacement(IntPtr hwnd, WindowPlacementSnapshot snapshot) => _placementStore.Restore(hwnd, snapshot);

    public IEnumerable<WindowRecord> GetWindowsForWorkspace(string workspaceId)
    {
        return _state.Windows.Values.Where(w => string.Equals(w.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase));
    }

    public void RemoveDeadWindows()
    {
        var deadKeys = _state.Windows
            .Where(kv => !_inspector.IsAlive(kv.Value.HwndPtr))
            .Select(kv => kv.Key)
            .ToArray();

        foreach (var key in deadKeys)
        {
            _state.Windows.Remove(key);
            Log.Info($"Removed dead window record {key}");
        }
    }

    private bool IsMonitorAtWindowLimit(string monitorKey)
    {
        var limit = _config.GetMaxManagedWindows(monitorKey);
        if (limit <= 0)
            return false;

        var current = _state.Windows.Values.Count(w =>
            !w.Ignored &&
            string.Equals(w.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase) &&
            _inspector.IsAlive(w.HwndPtr));
        return current >= limit;
    }
}
