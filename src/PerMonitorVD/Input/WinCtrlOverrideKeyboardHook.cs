using System.Runtime.InteropServices;
using PerMonitorVD.Core;
using PerMonitorVD.Native;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Input;

public sealed class WinCtrlOverrideKeyboardHook : IDisposable
{
    private readonly Win32.LowLevelKeyboardProc _proc;
    private readonly Action<WorkspaceCommand> _dispatch;
    private IntPtr _hook;

    public WinCtrlOverrideKeyboardHook(Action<WorkspaceCommand> dispatch)
    {
        _dispatch = dispatch;
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, Win32.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            Log.Warn("Could not install Win+Ctrl override keyboard hook.");
        else
            Log.Info("Installed Win+Ctrl override keyboard hook.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam.ToInt32() == Win32.WM_KEYDOWN || wParam.ToInt32() == Win32.WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
            var key = (Keys)data.vkCode;

            if (IsControlDown() && IsWinDown() && key == Keys.Left)
            {
                ThreadPool.QueueUserWorkItem(_ => _dispatch(new WorkspaceCommand { Type = WorkspaceCommandType.SwitchPrev }));
                return new IntPtr(1);
            }

            if (IsControlDown() && IsWinDown() && key == Keys.Right)
            {
                ThreadPool.QueueUserWorkItem(_ => _dispatch(new WorkspaceCommand { Type = WorkspaceCommandType.SwitchNext }));
                return new IntPtr(1);
            }
        }

        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool IsControlDown() => (Win32.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;

    private static bool IsWinDown() =>
        (Win32.GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0 ||
        (Win32.GetAsyncKeyState((int)Keys.RWin) & 0x8000) != 0;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
