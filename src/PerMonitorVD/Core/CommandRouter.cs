using PerMonitorVD.Utilities;

namespace PerMonitorVD.Core;

public sealed class CommandRouter
{
    private readonly WorkspaceEngine _engine;
    private readonly MonitorResolver _monitorResolver;
    private readonly WorkspaceRuntimeState _state;

    public CommandRouter(WorkspaceEngine engine, MonitorResolver monitorResolver, WorkspaceRuntimeState state)
    {
        _engine = engine;
        _monitorResolver = monitorResolver;
        _state = state;
    }

    public event EventHandler? HomeRequested;

    public async Task<string> ExecuteAsync(WorkspaceCommand command)
    {
        try
        {
            var monitorKey = command.Type is WorkspaceCommandType.Status
                or WorkspaceCommandType.Repair
                or WorkspaceCommandType.Refresh
                or WorkspaceCommandType.ReturnActive
                or WorkspaceCommandType.RescueAll
                or WorkspaceCommandType.Diagnostics
                or WorkspaceCommandType.ShowHome
                or WorkspaceCommandType.Pause
                or WorkspaceCommandType.Resume
                ? _state.Monitors.FirstOrDefault()?.MonitorKey ?? ""
                : _monitorResolver.ResolveTargetMonitor(_state, command.MonitorTarget);

            switch (command.Type)
            {
                case WorkspaceCommandType.SwitchPrev:
                    await _engine.SwitchPrevAsync(monitorKey);
                    return "OK switch-prev";

                case WorkspaceCommandType.SwitchNext:
                    await _engine.SwitchNextAsync(monitorKey);
                    return "OK switch-next";

                case WorkspaceCommandType.SwitchToIndex:
                    if (command.WorkspaceIndex is null) return "ERR missing workspace index";
                    await _engine.SwitchWorkspaceAsync(monitorKey, Math.Max(0, command.WorkspaceIndex.Value - 1));
                    return $"OK switch {command.WorkspaceIndex.Value}";

                case WorkspaceCommandType.MoveFocusedWindowPrev:
                    await _engine.MoveForegroundWindowByDeltaAsync(-1);
                    return "OK move-window-prev";

                case WorkspaceCommandType.MoveFocusedWindowNext:
                    await _engine.MoveForegroundWindowByDeltaAsync(1);
                    return "OK move-window-next";

                case WorkspaceCommandType.MoveFocusedWindowToIndex:
                    if (command.WorkspaceIndex is null) return "ERR missing workspace index";
                    await _engine.MoveForegroundWindowToWorkspaceAsync(monitorKey, Math.Max(0, command.WorkspaceIndex.Value - 1));
                    return $"OK move-window {command.WorkspaceIndex.Value}";

                case WorkspaceCommandType.Repair:
                    await _engine.RepairAsync();
                    return "OK repair";

                case WorkspaceCommandType.Refresh:
                    await _engine.RefreshAsync();
                    return "OK refresh";

                case WorkspaceCommandType.ReturnActive:
                    await _engine.ReturnToActiveCompositeAsync();
                    return "OK return-active";

                case WorkspaceCommandType.RescueAll:
                    await _engine.RescueAllWindowsAsync();
                    return "OK rescue-all";

                case WorkspaceCommandType.Diagnostics:
                    var path = await _engine.ExportDiagnosticsAsync();
                    return "OK diagnostics " + path;

                case WorkspaceCommandType.ShowHome:
                    HomeRequested?.Invoke(this, EventArgs.Empty);
                    return "OK home";

                case WorkspaceCommandType.Pause:
                    _engine.Pause();
                    return "OK pause";

                case WorkspaceCommandType.Resume:
                    _engine.Resume();
                    return "OK resume";

                case WorkspaceCommandType.Status:
                    return _engine.GetStatusText();

                default:
                    return "ERR unknown command";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Command failed: {command.Type}");
            return "ERR " + ex.Message;
        }
    }

}
