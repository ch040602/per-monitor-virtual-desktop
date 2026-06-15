using PerMonitorVD.Configuration;
using PerMonitorVD.Core;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Overlay;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CommandRouter _router;
    private readonly ConfigStore _configStore;
    private readonly Action<AppConfig> _onConfigSaved;
    private readonly Action _showHome;

    public TrayIconService(CommandRouter router, ConfigStore configStore, Action<AppConfig> onConfigSaved, Action showHome)
    {
        _router = router;
        _configStore = configStore;
        _onConfigSaved = onConfigSaved;
        _showHome = showHome;
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PerMonitorVD",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += async (_, _) => await ShowStatusAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("Home", null, (_, _) => _showHome());
        menu.Items.Add("Status", null, async (_, _) => await ShowStatusAsync());
        menu.Items.Add("Return to active desktop", null, async (_, _) => await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.ReturnActive }));
        menu.Items.Add("Refresh windows", null, async (_, _) => await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.Refresh }));
        menu.Items.Add("Repair state", null, async (_, _) => await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.Repair }));
        menu.Items.Add("Rescue all windows", null, async (_, _) => await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.RescueAll }));
        menu.Items.Add("Export diagnostics", null, async (_, _) => await ExportDiagnosticsAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Pause", null, async (_, _) => await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.Pause }));
        menu.Items.Add("Resume", null, async (_, _) => await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.Resume }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Edit config", null, (_, _) => new ConfigEditorForm(_configStore, _onConfigSaved).Show());
        menu.Items.Add("Open config.json", null, (_, _) => OpenPath(AppPaths.ConfigPath));
        menu.Items.Add("Open log file", null, (_, _) => OpenPath(AppPaths.LogPath));
        menu.Items.Add("Open config folder", null, (_, _) => OpenPath(AppPaths.BaseDirectory));
        menu.Items.Add("Exit", null, (_, _) => Application.Exit());

        return menu;
    }

    private async Task ShowStatusAsync()
    {
        var status = await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.Status });
        MessageBox.Show(status, "PerMonitorVD Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ExportDiagnosticsAsync()
    {
        var response = await _router.ExecuteAsync(new WorkspaceCommand { Type = WorkspaceCommandType.Diagnostics });
        MessageBox.Show(response, "PerMonitorVD Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);

        var prefix = "OK diagnostics ";
        if (response.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            OpenPath(response[prefix.Length..]);
    }

    private static void OpenPath(string path)
    {
        try
        {
            AppPaths.EnsureDirectories();
            if (File.Exists(path) || Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to open path: {path}");
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
