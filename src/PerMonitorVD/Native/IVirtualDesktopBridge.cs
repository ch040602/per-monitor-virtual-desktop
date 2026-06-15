using PerMonitorVD.Core;

namespace PerMonitorVD.Native;

public interface IVirtualDesktopBridge
{
    NativeDesktopInfo GetCurrentDesktop();
    IReadOnlyList<NativeDesktopInfo> GetDesktops();
    NativeDesktopInfo CreateDesktop(string name);
    void RenameDesktop(Guid desktopId, string name);
    void SwitchToDesktop(Guid desktopId);
    Guid GetWindowDesktopId(IntPtr hwnd);
    bool MoveWindowToDesktop(IntPtr hwnd, Guid desktopId);
}
