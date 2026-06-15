namespace PerMonitorVD.Utilities;

public static class AppPaths
{
    public static string BaseDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PerMonitorVD");

    public static string ConfigPath => Path.Combine(BaseDirectory, "config.json");
    public static string StatePath => Path.Combine(BaseDirectory, "state.json");
    public static string LogPath => Path.Combine(BaseDirectory, "PerMonitorVD.log");
    public static string ReportsDirectory => Path.Combine(BaseDirectory, "reports");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(ReportsDirectory);
    }

    public static string GetTimestampedReportPath()
    {
        EnsureDirectories();
        return Path.Combine(ReportsDirectory, $"diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
    }
}
