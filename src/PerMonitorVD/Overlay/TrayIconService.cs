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
    private readonly ToolStripMenuItem _otherDesktopAppsMenu = new("Other desktop apps");

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
        menu.Items.Add(_otherDesktopAppsMenu);
        menu.Items.Add(new ToolStripSeparator());
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

        menu.Opening += async (_, _) => await RefreshOtherDesktopAppsMenuAsync();
        return menu;
    }

    private async Task RefreshOtherDesktopAppsMenuAsync()
    {
        _otherDesktopAppsMenu.DropDownItems.Clear();

        try
        {
            var snapshot = await _router.GetHomeSnapshotAsync();
            var currentWorkspaceByMonitor = snapshot.Workspaces
                .Where(w => w.IsCurrent)
                .ToDictionary(w => w.MonitorKey, w => w.WorkspaceId, StringComparer.OrdinalIgnoreCase);

            var hiddenApps = snapshot.Windows
                .Where(w => !w.Ignored)
                .Where(w => currentWorkspaceByMonitor.TryGetValue(w.MonitorKey, out var currentWorkspaceId)
                            && !string.Equals(w.WorkspaceId, currentWorkspaceId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(w => w.MonitorKey)
                .ThenBy(w => w.WorkspaceLabel)
                .ThenBy(w => w.ProcessName)
                .ThenBy(w => w.Title)
                .ToArray();

            if (hiddenApps.Length == 0)
            {
                AddDisabledMenuItem(_otherDesktopAppsMenu.DropDownItems, "No apps on other desktops");
                _otherDesktopAppsMenu.Enabled = true;
                return;
            }

            foreach (var monitorGroup in hiddenApps.GroupBy(w => w.MonitorKey, StringComparer.OrdinalIgnoreCase))
            {
                var monitorName = snapshot.Monitors.FirstOrDefault(m => string.Equals(m.MonitorKey, monitorGroup.Key, StringComparison.OrdinalIgnoreCase))?.MonitorName
                                  ?? monitorGroup.Key;
                var monitorItem = new ToolStripMenuItem(monitorName);

                foreach (var workspaceGroup in monitorGroup.GroupBy(w => w.WorkspaceLabel).OrderBy(g => g.Key))
                {
                    var desktopItem = new ToolStripMenuItem($"Desktop {workspaceGroup.Key}");
                    foreach (var window in workspaceGroup)
                    {
                        var text = FormatWindowMenuText(window);
                        var item = new ToolStripMenuItem(text)
                        {
                            ToolTipText = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title
                        };
                        item.Click += async (_, _) => await _router.ExecuteAsync(new WorkspaceCommand
                        {
                            Type = WorkspaceCommandType.ActivateWindow,
                            Hwnd = window.Hwnd
                        });
                        desktopItem.DropDownItems.Add(item);
                    }

                    monitorItem.DropDownItems.Add(desktopItem);
                }

                _otherDesktopAppsMenu.DropDownItems.Add(monitorItem);
            }

            _otherDesktopAppsMenu.Enabled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh other desktop apps menu.");
            AddDisabledMenuItem(_otherDesktopAppsMenu.DropDownItems, "Could not load app list");
            _otherDesktopAppsMenu.Enabled = true;
        }
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

    private static void AddDisabledMenuItem(ToolStripItemCollection items, string text)
    {
        items.Add(new ToolStripMenuItem(text) { Enabled = false });
    }

    private static string FormatWindowMenuText(WindowHomeItem window)
    {
        var title = string.IsNullOrWhiteSpace(window.Title) ? "" : " - " + TrimMiddle(window.Title, 72);
        return $"{window.ProcessName}{title}";
    }

    private static string TrimMiddle(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        var side = Math.Max(1, (maxLength - 3) / 2);
        return value[..side] + "..." + value[^side..];
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
