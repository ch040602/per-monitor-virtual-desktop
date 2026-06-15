namespace PerMonitorVD.Core;

public enum WorkspaceCommandType
{
    SwitchPrev,
    SwitchNext,
    SwitchToIndex,
    MoveFocusedWindowPrev,
    MoveFocusedWindowNext,
    MoveFocusedWindowToIndex,
    Repair,
    Refresh,
    ReturnActive,
    RescueAll,
    Diagnostics,
    ShowHome,
    Pause,
    Resume,
    Status
}

public sealed class WorkspaceCommand
{
    public WorkspaceCommandType Type { get; init; }
    public string MonitorTarget { get; init; } = "mouse";
    public int? WorkspaceIndex { get; init; }
}
