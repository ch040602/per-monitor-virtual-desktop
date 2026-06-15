using PerMonitorVD.Utilities;

namespace PerMonitorVD.Native;

public sealed class WindowEventListener : IDisposable
{
    private readonly Win32.WinEventProc _proc;
    private readonly List<IntPtr> _hooks = [];

    public WindowEventListener()
    {
        _proc = EventCallback;
    }

    public event EventHandler<WindowEventArgs>? WindowEvent;

    public void Install()
    {
        if (_hooks.Count > 0)
            return;

        AddHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND);
        AddHook(Win32.EVENT_SYSTEM_MOVESIZEEND, Win32.EVENT_SYSTEM_MOVESIZEEND);
        AddHook(Win32.EVENT_SYSTEM_MINIMIZESTART, Win32.EVENT_SYSTEM_MINIMIZEEND);
        AddHook(Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_HIDE);
        AddHook(Win32.EVENT_OBJECT_LOCATIONCHANGE, Win32.EVENT_OBJECT_LOCATIONCHANGE);
        AddHook(Win32.EVENT_OBJECT_CLOAKED, Win32.EVENT_OBJECT_UNCLOAKED);

        Log.Info($"Installed WinEvent listener: {_hooks.Count} hooks.");
    }

    private void AddHook(uint eventMin, uint eventMax)
    {
        var hook = Win32.SetWinEventHook(
            eventMin,
            eventMax,
            IntPtr.Zero,
            _proc,
            idProcess: 0,
            idThread: 0,
            dwFlags: Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);

        if (hook == IntPtr.Zero)
        {
            Log.Warn($"Could not install WinEvent hook {eventMin:X}-{eventMax:X}.");
            return;
        }

        _hooks.Add(hook);
    }

    private void EventCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero)
            return;

        if (idObject != Win32.OBJID_WINDOW && eventType != Win32.EVENT_SYSTEM_FOREGROUND)
            return;

        WindowEvent?.Invoke(this, new WindowEventArgs(eventType, hwnd));
    }

    public void Dispose()
    {
        foreach (var hook in _hooks.ToArray())
        {
            try { Win32.UnhookWinEvent(hook); }
            catch { }
        }

        _hooks.Clear();
    }
}

public sealed class WindowEventArgs : EventArgs
{
    public WindowEventArgs(uint eventType, IntPtr hwnd)
    {
        EventType = eventType;
        Hwnd = hwnd;
    }

    public uint EventType { get; }
    public IntPtr Hwnd { get; }
}
