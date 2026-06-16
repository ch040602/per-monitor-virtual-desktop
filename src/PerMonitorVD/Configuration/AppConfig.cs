using System.Text.Json.Serialization;

namespace PerMonitorVD.Configuration;

public sealed class AppConfig
{
    public int ConfigVersion { get; set; } = 3;

    public int WorkspaceCountPerMonitor { get; set; } = 3;

    /// <summary>
    /// mouse | foreground | explicit monitor key.
    /// </summary>
    public string ActiveMonitorPolicy { get; set; } = "mouse";

    /// <summary>
    /// nativeParking | logicalHideShow.
    /// nativeParking uses real Windows virtual desktops as storage.
    /// logicalHideShow keeps windows on the active desktop and hides inactive workspaces.
    /// </summary>
    public string SwitchingStrategy { get; set; } = "nativeParking";

    /// <summary>
    /// If native VD parking fails for an individual window, hide/show that window instead of dropping it.
    /// </summary>
    public bool FallbackToLogicalHideShow { get; set; } = true;

    public bool EnableWinCtrlOverride { get; set; } = false;

    public bool SwitchBackToActiveOnStartup { get; set; } = true;

    public bool RenameManagedDesktops { get; set; } = true;

    public bool ShowOverlay { get; set; } = true;

    public bool StartPaused { get; set; } = false;

    /// <summary>
    /// Register PerMonitorVD under HKCU Run so it starts with Windows and remains a tray app.
    /// </summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>
    /// Best-effort Windows setting: show taskbar windows from all native virtual desktops.
    /// </summary>
    public bool EnsureTaskbarShowsAllDesktopWindows { get; set; } = true;

    /// <summary>
    /// Install WinEvent hooks for window show/foreground/move/close changes.
    /// </summary>
    public bool EnableWinEventHooks { get; set; } = true;

    /// <summary>
    /// Coalesces rapid WinEvent bursts before rescanning windows.
    /// </summary>
    public int WinEventDebounceMs { get; set; } = 160;

    /// <summary>
    /// Reconcile monitor/workspace state when display topology changes.
    /// </summary>
    public bool AutoRepairOnMonitorChange { get; set; } = true;

    /// <summary>
    /// Enables additional filters for shell surfaces, zero-sized windows, and transient owned popups.
    /// </summary>
    public bool StrictWindowFiltering { get; set; } = true;

    /// <summary>
    /// Do not track titleless windows unless WS_EX_APPWINDOW is set. Reduces shell/tool noise.
    /// </summary>
    public bool IgnoreTitlelessNonAppWindows { get; set; } = true;

    /// <summary>
    /// Per-monitor PMVD desktop counts. Missing monitors use WorkspaceCountPerMonitor.
    /// </summary>
    public List<MonitorDesktopCount> MonitorDesktopCounts { get; set; } = [];

    public HotkeyConfig Hotkeys { get; set; } = new();

    public List<WindowRule> Rules { get; set; } = DefaultRules();

    public static List<WindowRule> DefaultRules() =>
    [
        new WindowRule { Process = "explorer.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "ShellExperienceHost.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "StartMenuExperienceHost.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "SearchHost.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "TextInputHost.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "WidgetBoard.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "Widgets.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "GameBar.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "GameBarFTServer.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "XboxGameBarWidgets.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "logioptionsplus.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "LogiOptionsMgr.exe", Mode = WindowRuleMode.Ignore },
        new WindowRule { ClassName = "Shell_TrayWnd", Mode = WindowRuleMode.Ignore },
        new WindowRule { ClassName = "Shell_SecondaryTrayWnd", Mode = WindowRuleMode.Ignore },
        new WindowRule { ClassName = "NotifyIconOverflowWindow", Mode = WindowRuleMode.Ignore },
        new WindowRule { ClassName = "Progman", Mode = WindowRuleMode.Ignore },
        new WindowRule { ClassName = "WorkerW", Mode = WindowRuleMode.Ignore },
        new WindowRule { ClassName = "DV2ControlHost", Mode = WindowRuleMode.Ignore },
        new WindowRule { ClassName = "Windows.UI.Core.CoreWindow", TitleContains = "Start", Mode = WindowRuleMode.Ignore },
        new WindowRule { Process = "PowerToys.exe", Mode = WindowRuleMode.Sticky }
    ];

    public void CopyFrom(AppConfig other)
    {
        ConfigVersion = other.ConfigVersion;
        WorkspaceCountPerMonitor = other.WorkspaceCountPerMonitor;
        ActiveMonitorPolicy = other.ActiveMonitorPolicy;
        SwitchingStrategy = other.SwitchingStrategy;
        FallbackToLogicalHideShow = other.FallbackToLogicalHideShow;
        EnableWinCtrlOverride = other.EnableWinCtrlOverride;
        SwitchBackToActiveOnStartup = other.SwitchBackToActiveOnStartup;
        RenameManagedDesktops = other.RenameManagedDesktops;
        ShowOverlay = other.ShowOverlay;
        StartPaused = other.StartPaused;
        StartWithWindows = other.StartWithWindows;
        EnsureTaskbarShowsAllDesktopWindows = other.EnsureTaskbarShowsAllDesktopWindows;
        EnableWinEventHooks = other.EnableWinEventHooks;
        WinEventDebounceMs = other.WinEventDebounceMs;
        AutoRepairOnMonitorChange = other.AutoRepairOnMonitorChange;
        StrictWindowFiltering = other.StrictWindowFiltering;
        IgnoreTitlelessNonAppWindows = other.IgnoreTitlelessNonAppWindows;
        MonitorDesktopCounts = other.MonitorDesktopCounts;
        Hotkeys = other.Hotkeys;
        Rules = other.Rules;
    }

    public int GetDesktopCount(string monitorKey)
    {
        var count = MonitorDesktopCounts.FirstOrDefault(l => string.Equals(l.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase));
        return Math.Clamp(count?.DesktopCount ?? WorkspaceCountPerMonitor, 1, 12);
    }

    public void SetDesktopCount(string monitorKey, int desktopCount)
    {
        var normalized = Math.Clamp(desktopCount, 1, 12);
        var count = MonitorDesktopCounts.FirstOrDefault(l => string.Equals(l.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase));
        if (count is null)
        {
            MonitorDesktopCounts.Add(new MonitorDesktopCount { MonitorKey = monitorKey, DesktopCount = normalized });
            return;
        }

        count.DesktopCount = normalized;
    }
}

public sealed class MonitorDesktopCount
{
    public string MonitorKey { get; set; } = "";
    public int DesktopCount { get; set; }
}

public sealed class HotkeyConfig
{
    public string SwitchPrev { get; set; } = "Ctrl+Alt+Shift+Left";
    public string SwitchNext { get; set; } = "Ctrl+Alt+Shift+Right";
    public string Switch1 { get; set; } = "Ctrl+Alt+Shift+1";
    public string Switch2 { get; set; } = "Ctrl+Alt+Shift+2";
    public string Switch3 { get; set; } = "Ctrl+Alt+Shift+3";

    public string MoveWindowPrev { get; set; } = "Ctrl+Alt+Shift+Up";
    public string MoveWindowNext { get; set; } = "Ctrl+Alt+Shift+Down";
    public string Home { get; set; } = "Ctrl+Alt+Shift+Home";
    public string Repair { get; set; } = "Ctrl+Alt+Shift+R";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WindowRuleMode
{
    Ignore,
    Sticky,
    Manage
}

public sealed class WindowRule
{
    public string? Process { get; set; }
    public string? ClassName { get; set; }
    public string? TitleContains { get; set; }
    public WindowRuleMode Mode { get; set; } = WindowRuleMode.Manage;

    public bool Matches(string processName, string className, string title)
    {
        if (!string.IsNullOrWhiteSpace(Process) &&
            !string.Equals(Process, processName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ClassName) &&
            !string.Equals(ClassName, className, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(TitleContains) &&
            !title.Contains(TitleContains, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
