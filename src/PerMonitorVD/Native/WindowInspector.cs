using System.Diagnostics;
using System.Text;
using PerMonitorVD.Configuration;

namespace PerMonitorVD.Native;

public sealed class WindowInspector
{
    private readonly AppConfig _config;
    private readonly int _currentProcessId = Environment.ProcessId;

    public WindowInspector(AppConfig config)
    {
        _config = config;
    }

    public WindowInfo? Inspect(IntPtr hwnd)
    {
        if (!IsPotentiallyManageable(hwnd))
            return null;

        var processName = GetProcessName(hwnd);
        var className = GetClassName(hwnd);
        var title = GetWindowTitle(hwnd);

        if (string.IsNullOrWhiteSpace(processName))
            return null;

        var mode = ResolveRuleMode(processName, className, title);
        if (mode == WindowRuleMode.Ignore)
        {
            return new WindowInfo(hwnd, processName, className, title, IsSticky: false, IsIgnored: true);
        }

        return new WindowInfo(hwnd, processName, className, title, IsSticky: mode == WindowRuleMode.Sticky, IsIgnored: false);
    }

    public bool IsPotentiallyManageable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (!Win32.IsWindow(hwnd)) return false;
        if (!Win32.IsWindowVisible(hwnd)) return false;
        if (Win32.GetAncestor(hwnd, Win32.GA_ROOT) != hwnd) return false;

        Win32.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0 || processId == _currentProcessId) return false;

        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE).ToInt64();
        if ((style & Win32.WS_CHILD) != 0) return false;
        if (_config.StrictWindowFiltering && (style & Win32.WS_DISABLED) != 0) return false;

        var exStyle = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
        if ((exStyle & Win32.WS_EX_TOOLWINDOW) != 0) return false;
        if (_config.StrictWindowFiltering && (exStyle & Win32.WS_EX_NOACTIVATE) != 0 && (exStyle & Win32.WS_EX_APPWINDOW) == 0) return false;

        var owner = Win32.GetWindow(hwnd, Win32.GW_OWNER);
        if (owner != IntPtr.Zero && (exStyle & Win32.WS_EX_APPWINDOW) == 0) return false;

        if (_config.StrictWindowFiltering && Win32.GetWindowRect(hwnd, out var rect))
        {
            if (rect.Width <= 1 || rect.Height <= 1) return false;
        }

        if (_config.IgnoreTitlelessNonAppWindows && (exStyle & Win32.WS_EX_APPWINDOW) == 0)
        {
            var titleLength = Win32.GetWindowTextLength(hwnd);
            if (titleLength <= 0) return false;
        }

        if (IsDwmCloaked(hwnd)) return false;

        return true;
    }

    public bool IsAlive(IntPtr hwnd) => hwnd != IntPtr.Zero && Win32.IsWindow(hwnd);

    public string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        Win32.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public string GetWindowTitle(IntPtr hwnd)
    {
        var length = Win32.GetWindowTextLength(hwnd);
        var sb = new StringBuilder(Math.Max(length + 1, 256));
        Win32.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public string GetProcessName(IntPtr hwnd)
    {
        try
        {
            Win32.GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0) return "";
            using var process = Process.GetProcessById((int)processId);
            var name = process.ProcessName;
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
        }
        catch
        {
            return "";
        }
    }

    private WindowRuleMode ResolveRuleMode(string processName, string className, string title)
    {
        foreach (var rule in _config.Rules)
        {
            if (rule.Matches(processName, className, title))
                return rule.Mode;
        }

        return WindowRuleMode.Manage;
    }

    private static bool IsDwmCloaked(IntPtr hwnd)
    {
        try
        {
            var hr = Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_CLOAKED, out var cloaked, sizeof(int));
            return hr == 0 && cloaked != 0;
        }
        catch
        {
            return false;
        }
    }
}
