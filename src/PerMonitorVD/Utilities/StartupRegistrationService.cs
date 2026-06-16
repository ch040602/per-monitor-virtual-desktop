using Microsoft.Win32;

namespace PerMonitorVD.Utilities;

public static class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PerMonitorVD";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
                return;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Log.Info("Disabled PerMonitorVD startup registration.");
                return;
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                return;

            key.SetValue(ValueName, Quote(executablePath), RegistryValueKind.String);
            Log.Info($"Enabled PerMonitorVD startup registration: {executablePath}");
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not update PerMonitorVD startup registration: {ex.Message}");
        }
    }

    private static string Quote(string value)
    {
        return value.Contains('"') ? value : $"\"{value}\"";
    }
}
