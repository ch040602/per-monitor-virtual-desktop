using System.Text.Json;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Configuration;

public sealed class ConfigStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppConfig LoadOrCreate()
    {
        AppPaths.EnsureDirectories();

        if (!File.Exists(AppPaths.ConfigPath))
        {
            var created = new AppConfig();
            Save(created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(AppPaths.ConfigPath);
            var config = Deserialize(json);
            Migrate(config);
            return config;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load config. Falling back to default config.");
            return new AppConfig();
        }
    }

    public AppConfig Deserialize(string json)
    {
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
    }

    public string Serialize(AppConfig config)
    {
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    public void Save(AppConfig config)
    {
        AppPaths.EnsureDirectories();
        Migrate(config);
        var json = Serialize(config);
        File.WriteAllText(AppPaths.ConfigPath, json);
    }

    public void SaveRawJson(string json)
    {
        var config = Deserialize(json);
        Save(config);
    }

    private static void Migrate(AppConfig config)
    {
        config.ConfigVersion = Math.Max(config.ConfigVersion, 2);
        config.WorkspaceCountPerMonitor = Math.Clamp(config.WorkspaceCountPerMonitor, 1, 12);
        config.WinEventDebounceMs = Math.Clamp(config.WinEventDebounceMs <= 0 ? 160 : config.WinEventDebounceMs, 50, 2000);
        config.Hotkeys ??= new HotkeyConfig();
        config.Rules ??= AppConfig.DefaultRules();
        config.MonitorWindowLimits ??= [];
        foreach (var limit in config.MonitorWindowLimits)
            limit.MaxManagedWindows = Math.Clamp(limit.MaxManagedWindows, 0, 512);

        foreach (var defaultRule in AppConfig.DefaultRules())
        {
            if (!config.Rules.Any(rule => IsSameRuleIdentity(rule, defaultRule)))
                config.Rules.Add(defaultRule);
        }
    }

    private static bool IsSameRuleIdentity(WindowRule left, WindowRule right)
    {
        return string.Equals(left.Process, right.Process, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ClassName, right.ClassName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.TitleContains, right.TitleContains, StringComparison.OrdinalIgnoreCase);
    }
}
