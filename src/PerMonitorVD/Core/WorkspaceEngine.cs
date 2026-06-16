using System.Text.Json;
using PerMonitorVD.Configuration;
using PerMonitorVD.Native;
using PerMonitorVD.Overlay;
using PerMonitorVD.Persistence;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Core;

public sealed class WorkspaceEngine
{
    private const string DesktopPrefix = "[PMVD]";

    private readonly AppConfig _config;
    private readonly WorkspaceRuntimeState _state;
    private readonly StateStore _stateStore;
    private readonly MonitorResolver _monitorResolver;
    private readonly IVirtualDesktopBridge _desktopBridge;
    private readonly WindowTracker _windowTracker;
    private readonly OverlayService _overlay;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WorkspaceEngine(
        AppConfig config,
        WorkspaceRuntimeState state,
        StateStore stateStore,
        MonitorResolver monitorResolver,
        IVirtualDesktopBridge desktopBridge,
        WindowTracker windowTracker,
        OverlayService overlay)
    {
        _config = config;
        _state = state;
        _stateStore = stateStore;
        _monitorResolver = monitorResolver;
        _desktopBridge = desktopBridge;
        _windowTracker = windowTracker;
        _overlay = overlay;
        IsPaused = config.StartPaused;
    }

    public bool IsPaused { get; private set; }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _state.StateVersion = Math.Max(_state.StateVersion, 2);
            PrepareActiveCompositeDesktop();
            PrepareMonitorWorkspaces();
            EnsureOnActiveCompositeDesktop();
            _windowTracker.RefreshVisibleWindows();
            RestoreCurrentWorkspaceLogicalWindows();
            _stateStore.Save(_state);
            Log.Info("WorkspaceEngine initialized.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SwitchWorkspaceAsync(string monitorKey, int targetIndex)
    {
        await _gate.WaitAsync();
        try
        {
            await SwitchWorkspaceCoreAsync(monitorKey, targetIndex);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"SwitchWorkspace failed: monitor={monitorKey}, target={targetIndex}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SwitchNextAsync(string monitorKey)
    {
        await SwitchWorkspaceByDeltaAsync(monitorKey, 1);
    }

    public async Task SwitchPrevAsync(string monitorKey)
    {
        await SwitchWorkspaceByDeltaAsync(monitorKey, -1);
    }

    public async Task MoveForegroundWindowToWorkspaceAsync(string monitorKey, int targetIndex)
    {
        await _gate.WaitAsync();
        try
        {
            if (IsPaused) return;
            EnsureOnActiveCompositeDesktop();
            _windowTracker.RefreshVisibleWindows();

            var record = _windowTracker.GetForegroundManageableWindow();
            if (record is null)
                return;

            var monitor = GetMonitorOrThrow(monitorKey);
            if (targetIndex < 0 || targetIndex >= monitor.Workspaces.Count)
                return;

            await MoveWindowRecordToWorkspaceAsync(record, monitor, targetIndex);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MoveForegroundWindowToWorkspace failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MoveForegroundWindowByDeltaAsync(int delta)
    {
        await _gate.WaitAsync();
        try
        {
            if (IsPaused) return;
            EnsureOnActiveCompositeDesktop();
            _windowTracker.RefreshVisibleWindows();

            var record = _windowTracker.GetForegroundManageableWindow();
            if (record is null)
                return;

            var monitorKey = string.IsNullOrWhiteSpace(record.MonitorKey)
                ? _monitorResolver.ResolveWindowMonitor(record.HwndPtr)
                : record.MonitorKey;
            var monitor = GetMonitorOrThrow(monitorKey);
            if (monitor.Workspaces.Count == 0)
                return;

            var target = monitor.CurrentWorkspaceIndex + delta;
            if (target < 0) target = monitor.Workspaces.Count - 1;
            if (target >= monitor.Workspaces.Count) target = 0;

            await MoveWindowRecordToWorkspaceAsync(record, monitor, target);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MoveForegroundWindowByDelta failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RepairAsync()
    {
        await _gate.WaitAsync();
        try
        {
            PrepareActiveCompositeDesktop();
            PrepareMonitorWorkspaces();
            EnsureOnActiveCompositeDesktop();
            _windowTracker.RemoveDeadWindows();
            _windowTracker.RefreshVisibleWindows();
            RestoreCurrentWorkspaceLogicalWindows();
            _state.LastRepairSummary = $"Repair completed at {DateTimeOffset.Now:O}. monitors={_state.Monitors.Count}, windows={_state.Windows.Count}";
            _stateStore.Save(_state);
            _overlay.ShowText("PMVD repaired", _config.ShowOverlay);
            Log.Info(_state.LastRepairSummary);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Repair failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task RefreshAsync() => RefreshAsync("command");

    public async Task RefreshAsync(string reason)
    {
        await _gate.WaitAsync();
        try
        {
            if (IsPaused) return;
            PrepareMonitorWorkspaces();
            _windowTracker.RefreshVisibleWindows();
            RestoreCurrentWorkspaceLogicalWindows();
            _stateStore.Save(_state);
            Log.Info($"Refresh completed. reason={reason}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Refresh failed. reason={reason}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReconfigureWorkspacesAsync(string reason)
    {
        await _gate.WaitAsync();
        try
        {
            PrepareMonitorWorkspaces();
            _windowTracker.RemoveDeadWindows();
            _windowTracker.RefreshVisibleWindows();
            RestoreCurrentWorkspaceLogicalWindows();
            _stateStore.Save(_state);
            Log.Info($"Workspace configuration refreshed. reason={reason}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Workspace configuration refresh failed. reason={reason}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ReturnToActiveCompositeAsync() => ReturnToActiveCompositeDesktopAsync();

    public async Task ReturnToActiveCompositeDesktopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            PrepareActiveCompositeDesktop();
            _desktopBridge.SwitchToDesktop(_state.ActiveCompositeDesktopId);
            Thread.Sleep(60);
            _windowTracker.RefreshVisibleWindows();
            RestoreCurrentWorkspaceLogicalWindows();
            _stateStore.Save(_state);
            _overlay.ShowText("PMVD active", _config.ShowOverlay);
            Log.Info("Returned to active composite desktop.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ReturnToActiveComposite failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RescueAllWindowsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            PrepareActiveCompositeDesktop();
            _desktopBridge.SwitchToDesktop(_state.ActiveCompositeDesktopId);
            Thread.Sleep(60);

            foreach (var window in _state.Windows.Values.Where(_windowTracker.IsAlive).ToList())
            {
                TryMoveNative(window, _state.ActiveCompositeDesktopId, "rescue");
                ShowLogicalWindow(window);
                if (window.LastPlacement is not null)
                    _windowTracker.RestorePlacement(window.HwndPtr, window.LastPlacement);
            }

            _windowTracker.RefreshVisibleWindows();
            _stateStore.Save(_state);
            _overlay.ShowText("PMVD rescue complete", _config.ShowOverlay);
            Log.Info("RescueAllWindows completed.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RescueAllWindows failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> ExportDiagnosticsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var path = AppPaths.GetTimestampedReportPath();
            var desktops = Array.Empty<NativeDesktopInfo>();
            try { desktops = _desktopBridge.GetDesktops().ToArray(); } catch { }

            var lines = new List<string>
            {
                "PerMonitorVD diagnostics",
                "========================",
                "",
                GetStatusText(),
                "",
                "Native desktops:",
            };

            lines.AddRange(desktops.Select(d => $"  {d.Id}  {d.Name}"));
            lines.Add("");
            lines.Add("Tracked windows:");
            foreach (var w in _state.Windows.Values.OrderBy(w => w.MonitorKey).ThenBy(w => w.WorkspaceId).ThenBy(w => w.ProcessName))
            {
                lines.Add($"  {w.Hwnd} monitor={w.MonitorKey} workspace={w.WorkspaceId} native={w.CurrentNativeDesktopId} hidden={w.HiddenByPmvd} fail={w.NativeMoveFailureCount} process={w.ProcessName} class={w.ClassName} title={w.Title}");
            }

            lines.Add("");
            lines.Add("Config path: " + AppPaths.ConfigPath);
            lines.Add("State path: " + AppPaths.StatePath);
            lines.Add("Log path: " + AppPaths.LogPath);
            lines.Add("");
            lines.Add("State JSON:");
            lines.Add(JsonSerializer.Serialize(_state, StateStore.JsonOptions));

            File.WriteAllLines(path, lines);
            return path;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Pause()
    {
        IsPaused = true;
        _overlay.ShowText("PMVD paused", _config.ShowOverlay);
        Log.Info("Paused.");
    }

    public void Resume()
    {
        IsPaused = false;
        _overlay.ShowText("PMVD resumed", _config.ShowOverlay);
        Log.Info("Resumed.");
    }

    public string GetStatusText()
    {
        var hidden = _state.Windows.Values.Count(w => w.HiddenByPmvd);
        var failures = _state.Windows.Values.Sum(w => w.NativeMoveFailureCount);
        var lines = new List<string>
        {
            $"StateVersion: {_state.StateVersion}",
            $"Paused: {IsPaused}",
            $"ActiveCompositeDesktopId: {_state.ActiveCompositeDesktopId}",
            $"Windows tracked: {_state.Windows.Count}",
            $"HiddenByPmvd: {hidden}",
            $"NativeMoveFailureCount total: {failures}",
            $"LastMonitorSignature: {_state.LastMonitorSignature}",
            $"LastRepairSummary: {_state.LastRepairSummary}"
        };

        foreach (var monitor in _state.Monitors)
        {
            var slot = monitor.Workspaces.ElementAtOrDefault(monitor.CurrentWorkspaceIndex);
            lines.Add($"{monitor.DisplayName}: {(slot?.Label ?? "?")} / {slot?.WorkspaceId ?? "?"}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public async Task<HomeSnapshot> GetHomeSnapshotAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _windowTracker.RemoveDeadWindows();

            var workspaces = _state.Monitors
                .SelectMany(m => m.Workspaces.Select((slot, index) => new WorkspaceHomeItem(
                    m.MonitorKey,
                    m.DisplayName,
                    slot.WorkspaceId,
                    slot.Label,
                    slot.ParkingDesktopName,
                    index == m.CurrentWorkspaceIndex,
                    _state.Windows.Values.Count(w => string.Equals(w.WorkspaceId, slot.WorkspaceId, StringComparison.OrdinalIgnoreCase) && !w.Ignored))))
                .ToArray();

            var monitorItems = _state.Monitors
                .Select(m => new MonitorHomeItem(
                    m.MonitorKey,
                    m.DisplayName,
                    _state.Windows.Values.Count(w => !w.Ignored && string.Equals(w.MonitorKey, m.MonitorKey, StringComparison.OrdinalIgnoreCase)),
                    m.Workspaces.Count))
                .ToArray();

            var labelByWorkspace = workspaces.ToDictionary(w => w.WorkspaceId, w => w.WorkspaceLabel, StringComparer.OrdinalIgnoreCase);
            var windows = _state.Windows.Values
                .Where(_windowTracker.IsAlive)
                .OrderBy(w => w.MonitorKey)
                .ThenBy(w => w.WorkspaceId)
                .ThenBy(w => w.ProcessName)
                .ThenBy(w => w.Title)
                .Select(w => new WindowHomeItem(
                    w.Hwnd,
                    w.ProcessName,
                    w.Title,
                    w.MonitorKey,
                    w.WorkspaceId,
                    labelByWorkspace.TryGetValue(w.WorkspaceId, out var label) ? label : "?",
                    w.HiddenByPmvd,
                    w.Sticky,
                    w.Ignored))
                .ToArray();

            return new HomeSnapshot(monitorItems, workspaces, windows);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MoveWindowToWorkspaceAsync(string hwnd, string targetWorkspaceId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_state.Windows.TryGetValue(hwnd, out var record))
                return;

            var targetMonitor = _state.Monitors.FirstOrDefault(m => m.Workspaces.Any(w => string.Equals(w.WorkspaceId, targetWorkspaceId, StringComparison.OrdinalIgnoreCase)));
            if (targetMonitor is null)
                return;

            var targetIndex = targetMonitor.Workspaces.FindIndex(w => string.Equals(w.WorkspaceId, targetWorkspaceId, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
                return;

            await MoveWindowRecordToWorkspaceAsync(record, targetMonitor, targetIndex);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"MoveWindowToWorkspace failed: hwnd={hwnd}, target={targetWorkspaceId}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ActivateWindowAsync(string hwnd)
    {
        await _gate.WaitAsync();
        try
        {
            if (IsPaused) return;

            if (!_state.Windows.TryGetValue(hwnd, out var record) || !_windowTracker.IsAlive(record))
                return;

            var monitor = _state.Monitors.FirstOrDefault(m => string.Equals(m.MonitorKey, record.MonitorKey, StringComparison.OrdinalIgnoreCase));
            if (monitor is null)
                return;

            var targetIndex = monitor.Workspaces.FindIndex(w => string.Equals(w.WorkspaceId, record.WorkspaceId, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
                return;

            EnsureOnActiveCompositeDesktop();
            if (monitor.CurrentWorkspaceIndex != targetIndex)
                await SwitchWorkspaceCoreAsync(monitor.MonitorKey, targetIndex);

            await RestoreWindowAsync(record);
            Win32.ShowWindowAsync(record.HwndPtr, Win32.SW_RESTORE);
            await Task.Delay(40);
            Win32.SetForegroundWindow(record.HwndPtr);
            _overlay.ShowWorkspace(monitor.MonitorKey, $"App -> {monitor.Workspaces[targetIndex].Label}", _config.ShowOverlay);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"ActivateWindow failed: hwnd={hwnd}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MergeWorkspaceAsync(string sourceWorkspaceId, string targetWorkspaceId)
    {
        if (string.Equals(sourceWorkspaceId, targetWorkspaceId, StringComparison.OrdinalIgnoreCase))
            return;

        await _gate.WaitAsync();
        try
        {
            var targetMonitor = _state.Monitors.FirstOrDefault(m => m.Workspaces.Any(w => string.Equals(w.WorkspaceId, targetWorkspaceId, StringComparison.OrdinalIgnoreCase)));
            if (targetMonitor is null)
                return;

            var targetIndex = targetMonitor.Workspaces.FindIndex(w => string.Equals(w.WorkspaceId, targetWorkspaceId, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
                return;

            foreach (var record in GetSwitchableWindows(sourceWorkspaceId, "").Where(w => !w.Sticky && !w.Ignored).ToList())
            {
                await MoveWindowRecordToWorkspaceAsync(record, targetMonitor, targetIndex);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"MergeWorkspace failed: source={sourceWorkspaceId}, target={targetWorkspaceId}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SwitchWorkspaceByDeltaAsync(string monitorKey, int delta)
    {
        await _gate.WaitAsync();
        try
        {
            var monitor = GetMonitorOrThrow(monitorKey);
            if (monitor.Workspaces.Count == 0)
                return;

            var target = monitor.CurrentWorkspaceIndex + delta;
            if (target < 0) target = monitor.Workspaces.Count - 1;
            if (target >= monitor.Workspaces.Count) target = 0;

            await SwitchWorkspaceCoreAsync(monitorKey, target);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"SwitchWorkspaceByDelta failed: monitor={monitorKey}, delta={delta}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SwitchWorkspaceCoreAsync(string monitorKey, int targetIndex)
    {
        if (IsPaused) return;
        EnsureOnActiveCompositeDesktop();
        _windowTracker.RefreshVisibleWindows();

        var monitor = GetMonitorOrThrow(monitorKey);
        if (targetIndex < 0 || targetIndex >= monitor.Workspaces.Count)
            return;

        var oldIndex = monitor.CurrentWorkspaceIndex;
        if (oldIndex == targetIndex)
            return;

        var oldSlot = monitor.Workspaces[oldIndex];
        var newSlot = monitor.Workspaces[targetIndex];

        foreach (var window in GetSwitchableWindows(oldSlot.WorkspaceId, monitor.MonitorKey).ToList())
        {
            ParkWindow(window, oldSlot);
        }

        monitor.CurrentWorkspaceIndex = targetIndex;

        foreach (var window in GetSwitchableWindows(newSlot.WorkspaceId, monitor.MonitorKey).ToList())
        {
            await RestoreWindowAsync(window);
        }

        _windowTracker.RefreshVisibleWindows();
        _stateStore.Save(_state);
        _overlay.ShowWorkspace(monitor.MonitorKey, newSlot.Label, _config.ShowOverlay);
    }

    private async Task MoveWindowRecordToWorkspaceAsync(WindowRecord record, MonitorState monitor, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= monitor.Workspaces.Count)
            return;

        var targetSlot = monitor.Workspaces[targetIndex];
        record.MonitorKey = monitor.MonitorKey;
        record.WorkspaceId = targetSlot.WorkspaceId;
        record.LastPlacement = _windowTracker.CapturePlacement(record.HwndPtr);

        if (targetIndex == monitor.CurrentWorkspaceIndex)
            await RestoreWindowAsync(record);
        else
            ParkWindow(record, targetSlot);

        _stateStore.Save(_state);
        _overlay.ShowWorkspace(monitor.MonitorKey, $"Move -> {targetSlot.Label}", _config.ShowOverlay);
    }

    private IEnumerable<WindowRecord> GetSwitchableWindows(string workspaceId, string monitorKey)
    {
        return _windowTracker
            .GetWindowsForWorkspace(workspaceId)
            .Where(w => string.IsNullOrWhiteSpace(monitorKey) || string.Equals(w.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase))
            .Where(w => !w.Sticky && !w.Ignored)
            .Where(w => _windowTracker.IsAlive(w));
    }

    private void ParkWindow(WindowRecord window, WorkspaceSlot parkingSlot)
    {
        var hwnd = window.HwndPtr;
        window.LastPlacement = _windowTracker.CapturePlacement(hwnd);
        window.WorkspaceId = parkingSlot.WorkspaceId;

        if (UseLogicalHideShowOnly())
        {
            HideLogicalWindow(window, "logical strategy");
            return;
        }

        if (TryMoveNative(window, parkingSlot.ParkingDesktopId, $"park:{parkingSlot.WorkspaceId}"))
        {
            window.HiddenByPmvd = false;
            Log.Info($"Park {window.Hwnd} {window.ProcessName} -> {parkingSlot.WorkspaceId}");
            return;
        }

        if (_config.FallbackToLogicalHideShow)
            HideLogicalWindow(window, "native fallback");
    }

    private async Task RestoreWindowAsync(WindowRecord window)
    {
        if (window.HiddenByPmvd)
        {
            ShowLogicalWindow(window);
            window.CurrentNativeDesktopId = _state.ActiveCompositeDesktopId;
        }
        else if (!UseLogicalHideShowOnly())
        {
            if (!TryMoveNative(window, _state.ActiveCompositeDesktopId, "restore:active"))
                return;
        }

        if (window.LastPlacement is not null)
        {
            await Task.Delay(25);
            _windowTracker.RestorePlacement(window.HwndPtr, window.LastPlacement);
        }

        Log.Info($"Restore {window.Hwnd} {window.ProcessName} -> active composite");
    }

    private bool TryMoveNative(WindowRecord window, Guid desktopId, string context)
    {
        if (_desktopBridge.MoveWindowToDesktop(window.HwndPtr, desktopId))
        {
            window.CurrentNativeDesktopId = desktopId;
            return true;
        }

        window.NativeMoveFailureCount++;
        Log.Warn($"Native VD move failed for {window.Hwnd} context={context} desktop={desktopId}");
        return false;
    }

    private void HideLogicalWindow(WindowRecord window, string reason)
    {
        try
        {
            Win32.ShowWindowAsync(window.HwndPtr, Win32.SW_HIDE);
            window.HiddenByPmvd = true;
            window.CurrentNativeDesktopId = _state.ActiveCompositeDesktopId;
            Log.Info($"Logical hide {window.Hwnd} {window.ProcessName}. reason={reason}");
        }
        catch (Exception ex)
        {
            window.NativeMoveFailureCount++;
            Log.Error(ex, $"Logical hide failed for {window.Hwnd}");
        }
    }

    private void ShowLogicalWindow(WindowRecord window)
    {
        try
        {
            Win32.ShowWindowAsync(window.HwndPtr, Win32.SW_SHOWNOACTIVATE);
            window.HiddenByPmvd = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Logical show failed for {window.Hwnd}");
        }
    }

    private void RestoreCurrentWorkspaceLogicalWindows()
    {
        var currentWorkspaceIds = _state.Monitors
            .Select(m => m.Workspaces.ElementAtOrDefault(m.CurrentWorkspaceIndex)?.WorkspaceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var window in _state.Windows.Values.Where(w => w.HiddenByPmvd && currentWorkspaceIds.Contains(w.WorkspaceId)).ToList())
        {
            if (!_windowTracker.IsAlive(window))
                continue;

            ShowLogicalWindow(window);
            if (window.LastPlacement is not null)
                _windowTracker.RestorePlacement(window.HwndPtr, window.LastPlacement);
        }
    }

    private bool UseLogicalHideShowOnly()
    {
        return string.Equals(_config.SwitchingStrategy, "logicalHideShow", StringComparison.OrdinalIgnoreCase);
    }

    private void PrepareActiveCompositeDesktop()
    {
        var desktops = _desktopBridge.GetDesktops();

        if (_state.ActiveCompositeDesktopId != Guid.Empty && desktops.Any(d => d.Id == _state.ActiveCompositeDesktopId))
        {
            if (_config.RenameManagedDesktops)
                _desktopBridge.RenameDesktop(_state.ActiveCompositeDesktopId, $"{DesktopPrefix} ACTIVE");
            return;
        }

        var current = _desktopBridge.GetCurrentDesktop();
        _state.ActiveCompositeDesktopId = current.Id;

        if (_config.RenameManagedDesktops)
            _desktopBridge.RenameDesktop(current.Id, $"{DesktopPrefix} ACTIVE");
    }

    private void PrepareMonitorWorkspaces()
    {
        var desktops = _desktopBridge.GetDesktops();
        var currentMonitors = _monitorResolver.GetCurrentMonitors();
        var oldMonitors = _state.Monitors.ToList();
        var existingMonitors = oldMonitors
            .Where(m => !string.IsNullOrWhiteSpace(m.MonitorKey))
            .GroupBy(m => m.MonitorKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var rebuilt = new List<MonitorState>();
        var consumed = new HashSet<MonitorState>();

        var globalLabel = 1;
        foreach (var monitorInfo in currentMonitors)
        {
            MonitorState? monitor = null;
            if (existingMonitors.TryGetValue(monitorInfo.Key, out var exact) && !consumed.Contains(exact))
            {
                monitor = exact;
            }
            else if (_config.AutoRepairOnMonitorChange)
            {
                monitor = oldMonitors.FirstOrDefault(m => m.OrderIndex == monitorInfo.OrderIndex && !consumed.Contains(m));
                if (monitor is not null)
                    MigrateMonitorIdentity(monitor, monitorInfo);
            }

            if (monitor is null)
            {
                monitor = new MonitorState
                {
                    MonitorKey = monitorInfo.Key,
                    DisplayName = monitorInfo.DisplayName,
                    OrderIndex = monitorInfo.OrderIndex,
                    CurrentWorkspaceIndex = 0
                };
            }

            monitor.MonitorKey = monitorInfo.Key;
            monitor.DisplayName = monitorInfo.DisplayName;
            monitor.OrderIndex = monitorInfo.OrderIndex;
            monitor.BoundsLeft = monitorInfo.Bounds.Left;
            monitor.BoundsTop = monitorInfo.Bounds.Top;
            monitor.BoundsWidth = monitorInfo.Bounds.Width;
            monitor.BoundsHeight = monitorInfo.Bounds.Height;
            var workspaceCount = _config.GetDesktopCount(monitorInfo.Key);
            monitor.Workspaces = EnsureWorkspaceSlots(monitor, monitorIndexBaseLabel: globalLabel, desktops, workspaceCount);
            if (monitor.CurrentWorkspaceIndex < 0 || monitor.CurrentWorkspaceIndex >= monitor.Workspaces.Count)
                monitor.CurrentWorkspaceIndex = Math.Max(0, monitor.Workspaces.Count - 1);
            ReassignWindowsFromRemovedWorkspaces(monitor);

            rebuilt.Add(monitor);
            consumed.Add(monitor);
            globalLabel += workspaceCount;
        }

        _state.Monitors = rebuilt;
        _state.LastMonitorSignature = _monitorResolver.GetMonitorSignature();
    }

    private void MigrateMonitorIdentity(MonitorState monitor, MonitorDescriptor monitorInfo)
    {
        var oldKey = monitor.MonitorKey;
        if (string.Equals(oldKey, monitorInfo.Key, StringComparison.OrdinalIgnoreCase))
            return;

        var oldWorkspaceIds = monitor.Workspaces.Select(w => w.WorkspaceId).ToArray();
        monitor.MonitorKey = monitorInfo.Key;

        for (var i = 0; i < monitor.Workspaces.Count; i++)
        {
            var oldWorkspaceId = oldWorkspaceIds.ElementAtOrDefault(i);
            var newWorkspaceId = $"{monitorInfo.Key}-W{i + 1}";
            monitor.Workspaces[i].WorkspaceId = newWorkspaceId;

            foreach (var window in _state.Windows.Values)
            {
                if (string.Equals(window.MonitorKey, oldKey, StringComparison.OrdinalIgnoreCase))
                    window.MonitorKey = monitorInfo.Key;

                if (!string.IsNullOrWhiteSpace(oldWorkspaceId) && string.Equals(window.WorkspaceId, oldWorkspaceId, StringComparison.OrdinalIgnoreCase))
                    window.WorkspaceId = newWorkspaceId;
            }
        }

        Log.Info($"Migrated monitor identity {oldKey} -> {monitorInfo.Key}");
    }

    private List<WorkspaceSlot> EnsureWorkspaceSlots(MonitorState monitor, int monitorIndexBaseLabel, IReadOnlyList<NativeDesktopInfo> knownDesktops, int workspaceCount)
    {
        var slots = new List<WorkspaceSlot>();
        var byId = monitor.Workspaces.ToDictionary(w => w.WorkspaceId, StringComparer.OrdinalIgnoreCase);
        var desktops = knownDesktops.ToList();

        for (var i = 0; i < workspaceCount; i++)
        {
            var label = (monitorIndexBaseLabel + i).ToString();
            var workspaceId = $"{monitor.MonitorKey}-W{i + 1}";
            var name = $"{DesktopPrefix} {workspaceId} ({label})";

            if (!byId.TryGetValue(workspaceId, out var slot))
            {
                slot = new WorkspaceSlot
                {
                    WorkspaceId = workspaceId,
                    Label = label,
                    ParkingDesktopName = name
                };
            }

            slot.WorkspaceId = workspaceId;
            slot.Label = label;
            slot.ParkingDesktopName = name;

            var exists = slot.ParkingDesktopId != Guid.Empty && desktops.Any(d => d.Id == slot.ParkingDesktopId);
            if (!exists)
            {
                var named = desktops.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
                if (named is not null)
                {
                    slot.ParkingDesktopId = named.Id;
                }
                else
                {
                    var created = _desktopBridge.CreateDesktop(name);
                    slot.ParkingDesktopId = created.Id;
                    desktops.Add(created);
                    Log.Info($"Created parking desktop {name} {created.Id}");
                }
            }
            else if (_config.RenameManagedDesktops)
            {
                _desktopBridge.RenameDesktop(slot.ParkingDesktopId, name);
            }

            slots.Add(slot);
        }

        return slots;
    }

    private void ReassignWindowsFromRemovedWorkspaces(MonitorState monitor)
    {
        if (monitor.Workspaces.Count == 0)
            return;

        var retainedWorkspaceIds = monitor.Workspaces.Select(w => w.WorkspaceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fallbackSlot = monitor.Workspaces[Math.Clamp(monitor.CurrentWorkspaceIndex, 0, monitor.Workspaces.Count - 1)];

        foreach (var window in _state.Windows.Values.Where(w =>
                     !w.Ignored &&
                     string.Equals(w.MonitorKey, monitor.MonitorKey, StringComparison.OrdinalIgnoreCase) &&
                     !retainedWorkspaceIds.Contains(w.WorkspaceId)))
        {
            Log.Info($"Reassigned {window.Hwnd} {window.ProcessName} from removed workspace {window.WorkspaceId} to {fallbackSlot.WorkspaceId}.");
            window.WorkspaceId = fallbackSlot.WorkspaceId;
        }
    }

    private void EnsureOnActiveCompositeDesktop()
    {
        var current = _desktopBridge.GetCurrentDesktop();
        if (current.Id == _state.ActiveCompositeDesktopId)
            return;

        if (_config.SwitchBackToActiveOnStartup)
        {
            _desktopBridge.SwitchToDesktop(_state.ActiveCompositeDesktopId);
            Thread.Sleep(50);
        }
    }

    private MonitorState GetMonitorOrThrow(string monitorKey)
    {
        return _state.Monitors.FirstOrDefault(m => string.Equals(m.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"Monitor not found: {monitorKey}");
    }
}
