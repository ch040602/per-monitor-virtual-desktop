using PerMonitorVD.Configuration;
using PerMonitorVD.Native;

namespace PerMonitorVD.Core;

public sealed class MonitorResolver
{
    private readonly AppConfig _config;

    public MonitorResolver(AppConfig config)
    {
        _config = config;
    }

    public IReadOnlyList<MonitorDescriptor> GetCurrentMonitors()
    {
        return Screen.AllScreens
            .OrderBy(s => s.Bounds.Left)
            .ThenBy(s => s.Bounds.Top)
            .Select((s, index) => new MonitorDescriptor(
                Key: NormalizeDeviceName(s.DeviceName),
                DisplayName: $"Monitor {index + 1} ({s.DeviceName})",
                OrderIndex: index,
                Bounds: s.Bounds))
            .ToArray();
    }

    public string GetMonitorSignature()
    {
        return string.Join("|", GetCurrentMonitors().Select(m => m.SignaturePart));
    }

    public string ResolveTargetMonitor(WorkspaceRuntimeState state, string? explicitTarget = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitTarget) && explicitTarget != "mouse" && explicitTarget != "foreground")
            return explicitTarget;

        var policy = explicitTarget ?? _config.ActiveMonitorPolicy;

        if (string.Equals(policy, "foreground", StringComparison.OrdinalIgnoreCase))
        {
            var hwnd = Win32.GetForegroundWindow();
            var key = ResolveWindowMonitor(hwnd);
            if (state.Monitors.Any(m => string.Equals(m.MonitorKey, key, StringComparison.OrdinalIgnoreCase)))
                return key;
        }

        var cursorScreen = Screen.FromPoint(Cursor.Position);
        var cursorKey = NormalizeDeviceName(cursorScreen.DeviceName);
        if (state.Monitors.Any(m => string.Equals(m.MonitorKey, cursorKey, StringComparison.OrdinalIgnoreCase)))
            return cursorKey;

        return state.Monitors.FirstOrDefault()?.MonitorKey ?? cursorKey;
    }

    public string ResolveWindowMonitor(IntPtr hwnd)
    {
        try
        {
            var screen = Screen.FromHandle(hwnd);
            return NormalizeDeviceName(screen.DeviceName);
        }
        catch
        {
            return NormalizeDeviceName(Screen.PrimaryScreen?.DeviceName ?? "DISPLAY1");
        }
    }

    public static string NormalizeDeviceName(string value)
    {
        return value.Replace("\\\\.\\", "", StringComparison.OrdinalIgnoreCase).Trim();
    }
}
