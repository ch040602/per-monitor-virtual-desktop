using PerMonitorVD.Core;

namespace PerMonitorVD.Native;

public sealed class WindowPlacementStore
{
    public WindowPlacementSnapshot? Capture(IntPtr hwnd)
    {
        var placement = new Win32.WINDOWPLACEMENT
        {
            length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.WINDOWPLACEMENT>()
        };

        if (!Win32.GetWindowPlacement(hwnd, ref placement))
            return null;

        return new WindowPlacementSnapshot
        {
            Length = placement.length,
            Flags = placement.flags,
            ShowCmd = placement.showCmd,
            MinPosition = new PointSnapshot { X = placement.ptMinPosition.X, Y = placement.ptMinPosition.Y },
            MaxPosition = new PointSnapshot { X = placement.ptMaxPosition.X, Y = placement.ptMaxPosition.Y },
            NormalPosition = new RectSnapshot
            {
                Left = placement.rcNormalPosition.Left,
                Top = placement.rcNormalPosition.Top,
                Right = placement.rcNormalPosition.Right,
                Bottom = placement.rcNormalPosition.Bottom
            }
        };
    }

    public void Restore(IntPtr hwnd, WindowPlacementSnapshot snapshot)
    {
        var placement = new Win32.WINDOWPLACEMENT
        {
            length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.WINDOWPLACEMENT>(),
            flags = snapshot.Flags,
            showCmd = snapshot.ShowCmd,
            ptMinPosition = new Win32.POINT { X = snapshot.MinPosition.X, Y = snapshot.MinPosition.Y },
            ptMaxPosition = new Win32.POINT { X = snapshot.MaxPosition.X, Y = snapshot.MaxPosition.Y },
            rcNormalPosition = new Win32.RECT
            {
                Left = snapshot.NormalPosition.Left,
                Top = snapshot.NormalPosition.Top,
                Right = snapshot.NormalPosition.Right,
                Bottom = snapshot.NormalPosition.Bottom
            }
        };

        Win32.SetWindowPlacement(hwnd, ref placement);
    }
}
