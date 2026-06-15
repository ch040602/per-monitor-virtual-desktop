using PerMonitorVD.Core;
using PerMonitorVD.Utilities;
using WindowsDesktop;

namespace PerMonitorVD.Native;

public sealed class SlionsVirtualDesktopBridge : IVirtualDesktopBridge
{
    public NativeDesktopInfo GetCurrentDesktop()
    {
        var desktop = VirtualDesktop.Current;
        return new NativeDesktopInfo(desktop.Id, desktop.Name ?? "");
    }

    public IReadOnlyList<NativeDesktopInfo> GetDesktops()
    {
        return VirtualDesktop.GetDesktops()
            .Select(d => new NativeDesktopInfo(d.Id, d.Name ?? ""))
            .ToArray();
    }

    public NativeDesktopInfo CreateDesktop(string name)
    {
        var desktop = VirtualDesktop.Create();
        TrySetName(desktop, name);
        return new NativeDesktopInfo(desktop.Id, desktop.Name ?? name);
    }

    public void RenameDesktop(Guid desktopId, string name)
    {
        var desktop = GetDesktopOrThrow(desktopId);
        TrySetName(desktop, name);
    }

    public void SwitchToDesktop(Guid desktopId)
    {
        var desktop = GetDesktopOrThrow(desktopId);
        desktop.Switch();
    }

    public Guid GetWindowDesktopId(IntPtr hwnd)
    {
        try
        {
            return VirtualDesktop.FromHwnd(hwnd)?.Id ?? Guid.Empty;
        }
        catch (Exception ex)
        {
            Log.Warn($"GetWindowDesktopId failed for {HwndUtil.Format(hwnd)}: {ex.Message}");
            return Guid.Empty;
        }
    }

    public bool MoveWindowToDesktop(IntPtr hwnd, Guid desktopId)
    {
        try
        {
            var desktop = GetDesktopOrThrow(desktopId);
            VirtualDesktop.MoveToDesktop(hwnd, desktop);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"MoveWindowToDesktop failed for {HwndUtil.Format(hwnd)} -> {desktopId}: {ex.Message}");
            return false;
        }
    }

    private static VirtualDesktop GetDesktopOrThrow(Guid id)
    {
        return VirtualDesktop.FromId(id) ?? throw new InvalidOperationException($"Virtual desktop not found: {id}");
    }

    private static void TrySetName(VirtualDesktop desktop, string name)
    {
        try
        {
            desktop.Name = name;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not set virtual desktop name '{name}': {ex.Message}");
        }
    }
}
