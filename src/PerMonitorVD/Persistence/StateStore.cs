using System.Text.Json;
using PerMonitorVD.Core;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Persistence;

public sealed class StateStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public WorkspaceRuntimeState LoadOrCreate()
    {
        AppPaths.EnsureDirectories();

        if (!File.Exists(AppPaths.StatePath))
            return new WorkspaceRuntimeState();

        try
        {
            var json = File.ReadAllText(AppPaths.StatePath);
            var state = JsonSerializer.Deserialize<WorkspaceRuntimeState>(json, JsonOptions) ?? new WorkspaceRuntimeState();
            Migrate(state);
            return state;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load state. Starting with empty state.");
            return new WorkspaceRuntimeState();
        }
    }

    public void Save(WorkspaceRuntimeState state)
    {
        AppPaths.EnsureDirectories();
        state.StateVersion = Math.Max(state.StateVersion, 2);
        state.LastUpdated = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(AppPaths.StatePath, json);
    }

    private static void Migrate(WorkspaceRuntimeState state)
    {
        state.StateVersion = Math.Max(state.StateVersion, 2);
        state.Monitors ??= [];
        state.Windows ??= new Dictionary<string, WindowRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var window in state.Windows.Values)
        {
            window.Hwnd ??= "";
            window.ProcessName ??= "";
            window.ClassName ??= "";
            window.Title ??= "";
            window.MonitorKey ??= "";
            window.WorkspaceId ??= "";
        }
    }
}
