namespace PerMonitorVD.Utilities;

public static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(Exception exception, string message) => Write("ERROR", $"{message}\r\n{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureDirectories();
            lock (Gate)
            {
                File.AppendAllText(
                    AppPaths.LogPath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {level} {message}\r\n");
            }
        }
        catch
        {
            // Avoid recursive failures in global hooks / tray callbacks.
        }
    }
}
