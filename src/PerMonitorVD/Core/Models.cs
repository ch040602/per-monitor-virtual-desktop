using System.Text.Json.Serialization;

namespace PerMonitorVD.Core;

public sealed class WorkspaceRuntimeState
{
    public int StateVersion { get; set; } = 2;

    public Guid ActiveCompositeDesktopId { get; set; }

    public List<MonitorState> Monitors { get; set; } = [];

    /// <summary>
    /// Key: HWND formatted as hexadecimal, e.g. 0x00000000000A1234.
    /// </summary>
    public Dictionary<string, WindowRecord> Windows { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string LastMonitorSignature { get; set; } = "";

    public string LastRepairSummary { get; set; } = "";

    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.Now;
}

public sealed class MonitorState
{
    public string MonitorKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int OrderIndex { get; set; }
    public int BoundsLeft { get; set; }
    public int BoundsTop { get; set; }
    public int BoundsWidth { get; set; }
    public int BoundsHeight { get; set; }
    public int CurrentWorkspaceIndex { get; set; }
    public List<WorkspaceSlot> Workspaces { get; set; } = [];
}

public sealed class WorkspaceSlot
{
    public string WorkspaceId { get; set; } = "";
    public string Label { get; set; } = "";
    public Guid ParkingDesktopId { get; set; }
    public string ParkingDesktopName { get; set; } = "";
}

public sealed class WindowRecord
{
    public string Hwnd { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Title { get; set; } = "";

    public string MonitorKey { get; set; } = "";
    public string WorkspaceId { get; set; } = "";

    public Guid CurrentNativeDesktopId { get; set; }
    public WindowPlacementSnapshot? LastPlacement { get; set; }

    public bool Sticky { get; set; }
    public bool Ignored { get; set; }

    /// <summary>
    /// True when native VD parking failed or logicalHideShow mode intentionally hid the window.
    /// </summary>
    public bool HiddenByPmvd { get; set; }

    public int NativeMoveFailureCount { get; set; }

    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.Now;

    [JsonIgnore]
    public IntPtr HwndPtr => HwndUtil.Parse(Hwnd);
}

public sealed class WindowPlacementSnapshot
{
    public uint Length { get; set; }
    public uint Flags { get; set; }
    public uint ShowCmd { get; set; }
    public PointSnapshot MinPosition { get; set; } = new();
    public PointSnapshot MaxPosition { get; set; } = new();
    public RectSnapshot NormalPosition { get; set; } = new();
}

public sealed class PointSnapshot
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class RectSnapshot
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
}

public static class HwndUtil
{
    public static string Format(IntPtr hwnd) => $"0x{hwnd.ToInt64():X16}";

    public static IntPtr Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return IntPtr.Zero;
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return new IntPtr(unchecked((long)Convert.ToUInt64(text, 16)));
    }
}

public sealed record NativeDesktopInfo(Guid Id, string Name);

public sealed record WorkspaceHomeItem(
    string MonitorKey,
    string MonitorName,
    string WorkspaceId,
    string WorkspaceLabel,
    string ParkingDesktopName,
    bool IsCurrent,
    int WindowCount);

public sealed record MonitorHomeItem(
    string MonitorKey,
    string MonitorName,
    int WindowCount,
    int MaxManagedWindows);

public sealed record WindowHomeItem(
    string Hwnd,
    string ProcessName,
    string Title,
    string MonitorKey,
    string WorkspaceId,
    string WorkspaceLabel,
    bool HiddenByPmvd,
    bool Sticky,
    bool Ignored);

public sealed record HomeSnapshot(
    IReadOnlyList<MonitorHomeItem> Monitors,
    IReadOnlyList<WorkspaceHomeItem> Workspaces,
    IReadOnlyList<WindowHomeItem> Windows);

public sealed record MonitorDescriptor(
    string Key,
    string DisplayName,
    int OrderIndex,
    Rectangle Bounds)
{
    public string SignaturePart => $"{OrderIndex}:{Key}:{Bounds.Left},{Bounds.Top},{Bounds.Width},{Bounds.Height}";
}

public sealed record WindowEventHint(
    int EventId,
    IntPtr Hwnd,
    string EventName,
    DateTimeOffset Timestamp);
