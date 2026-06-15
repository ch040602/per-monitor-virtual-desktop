using Microsoft.Win32;
using PerMonitorVD.Native;

namespace PerMonitorVD.Utilities;

public static class TaskbarSettingsService
{
    private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string VirtualDesktopTaskbarFilter = "VirtualDesktopTaskbarFilter";

    public static void EnsureAllDesktopAppsVisible(bool enabled)
    {
        if (!enabled)
            return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ExplorerAdvancedKey);
            key?.SetValue(VirtualDesktopTaskbarFilter, 0, RegistryValueKind.DWord);
            Win32.SendMessageTimeout(
                Win32.HWND_BROADCAST,
                Win32.WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "TraySettings",
                Win32.SMTO_ABORTIFHUNG,
                1000,
                out _);
            Log.Info("Ensured Windows taskbar shows windows from all virtual desktops.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not update Windows taskbar virtual desktop setting: {ex.Message}");
        }
    }
}
