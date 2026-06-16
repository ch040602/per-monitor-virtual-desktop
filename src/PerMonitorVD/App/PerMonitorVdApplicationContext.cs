using PerMonitorVD.Configuration;
using PerMonitorVD.Core;
using PerMonitorVD.Input;
using PerMonitorVD.Native;
using PerMonitorVD.Overlay;
using PerMonitorVD.Persistence;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.App;

public sealed class PerMonitorVdApplicationContext : ApplicationContext
{
    private readonly ConfigStore _configStore;
    private readonly AppConfig _config;
    private readonly WorkspaceRuntimeState _state;
    private readonly StateStore _stateStore;
    private readonly OverlayService _overlay;
    private readonly WorkspaceEngine _engine;
    private readonly CommandRouter _router;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly PipeCommandServer _pipeServer;
    private readonly TrayIconService _tray;
    private HomeWindowForm? _homeWindow;
    private readonly AsyncDebouncer? _windowEventDebouncer;
    private readonly WindowEventListener? _windowEvents;
    private readonly WinCtrlOverrideKeyboardHook? _winCtrlOverride;
    private readonly SynchronizationContext? _syncContext;

    public PerMonitorVdApplicationContext(string[] args)
    {
        AppPaths.EnsureDirectories();
        Log.Info("Starting PerMonitorVD.");
        _syncContext = SynchronizationContext.Current;

        _configStore = new ConfigStore();
        _config = _configStore.LoadOrCreate();
        StartupRegistrationService.Apply(_config.StartWithWindows);
        TaskbarSettingsService.EnsureAllDesktopAppsVisible(_config.EnsureTaskbarShowsAllDesktopWindows);

        _stateStore = new StateStore();
        _state = _stateStore.LoadOrCreate();

        _overlay = new OverlayService(SynchronizationContext.Current);

        var desktopBridge = new SlionsVirtualDesktopBridge();
        var monitorResolver = new MonitorResolver(_config);
        var inspector = new WindowInspector(_config);
        var placementStore = new WindowPlacementStore();
        var tracker = new WindowTracker(inspector, placementStore, monitorResolver, desktopBridge, _state, _config);

        _engine = new WorkspaceEngine(
            _config,
            _state,
            _stateStore,
            monitorResolver,
            desktopBridge,
            tracker,
            _overlay);

        _router = new CommandRouter(_engine, monitorResolver, _state);
        _hotkeys = new GlobalHotkeyService();
        _pipeServer = new PipeCommandServer(_router);
        _tray = new TrayIconService(_router, _configStore, ReloadConfigFromEditor, ShowHomeWindow);

        _hotkeys.CommandReceived += async (_, command) => await _router.ExecuteAsync(command);
        _router.HomeRequested += (_, _) => ShowHomeWindow();
        _hotkeys.RegisterFromConfig(_config);

        if (_config.EnableWinCtrlOverride)
        {
            _winCtrlOverride = new WinCtrlOverrideKeyboardHook(command => _ = _router.ExecuteAsync(command));
            _winCtrlOverride.Install();
        }

        if (_config.EnableWinEventHooks)
        {
            _windowEventDebouncer = new AsyncDebouncer(
                TimeSpan.FromMilliseconds(Math.Clamp(_config.WinEventDebounceMs, 50, 2000)),
                () => _engine.RefreshAsync("window-event"));
            _windowEvents = new WindowEventListener();
            _windowEvents.WindowEvent += (_, _) => _windowEventDebouncer.Schedule();
            _windowEvents.Install();
        }

        if (_config.AutoRepairOnMonitorChange)
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _pipeServer.Start();

        _ = InitializeAsync(args);
    }

    private void ReloadConfigFromEditor(AppConfig config)
    {
        _config.CopyFrom(config);
        StartupRegistrationService.Apply(_config.StartWithWindows);
        TaskbarSettingsService.EnsureAllDesktopAppsVisible(_config.EnsureTaskbarShowsAllDesktopWindows);
        _hotkeys.RegisterFromConfig(_config);
        _ = _engine.RefreshAsync("config-reload");
    }

    private async void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        await _engine.RefreshAsync("display-change");
    }

    private void ShowHomeWindow()
    {
        void Show()
        {
            if (_homeWindow is null || _homeWindow.IsDisposed)
                _homeWindow = new HomeWindowForm(_engine, _config, _configStore);

            _homeWindow.Show();
            _homeWindow.WindowState = FormWindowState.Normal;
            _homeWindow.Activate();
            _ = _homeWindow.RefreshSnapshotAsync();
        }

        if (_syncContext is null)
            Show();
        else
            _syncContext.Post(_ => Show(), null);
    }

    private async Task InitializeAsync(string[] args)
    {
        try
        {
            await _engine.InitializeAsync();

            if (args.Length > 0)
            {
                var command = CommandParser.Parse(args);
                await _router.ExecuteAsync(command);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fatal initialization error.");
            MessageBox.Show(
                ex.Message,
                "PerMonitorVD initialization failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Log.Info("Stopping PerMonitorVD.");
            _stateStore.Save(_state);
            if (_config.AutoRepairOnMonitorChange)
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _windowEventDebouncer?.Dispose();
            _windowEvents?.Dispose();
            _winCtrlOverride?.Dispose();
            _pipeServer.Dispose();
            _hotkeys.Dispose();
            _tray.Dispose();
        }

        base.Dispose(disposing);
    }
}
