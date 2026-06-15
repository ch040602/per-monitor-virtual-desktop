namespace PerMonitorVD.Native;

public sealed record WindowInfo(
    IntPtr Hwnd,
    string ProcessName,
    string ClassName,
    string Title,
    bool IsSticky,
    bool IsIgnored);
