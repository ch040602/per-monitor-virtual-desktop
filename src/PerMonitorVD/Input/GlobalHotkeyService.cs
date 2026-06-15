using PerMonitorVD.Configuration;
using PerMonitorVD.Core;
using PerMonitorVD.Native;
using PerMonitorVD.Utilities;

namespace PerMonitorVD.Input;

public sealed class GlobalHotkeyService : NativeWindow, IDisposable
{
    private readonly Dictionary<int, WorkspaceCommand> _commands = new();
    private int _nextId = 100;

    public GlobalHotkeyService()
    {
        CreateHandle(new CreateParams { Caption = "PerMonitorVD.HotkeyWindow" });
    }

    public event EventHandler<WorkspaceCommand>? CommandReceived;

    public void RegisterFromConfig(AppConfig config)
    {
        UnregisterAll();
        Register(config.Hotkeys.SwitchPrev, new WorkspaceCommand { Type = WorkspaceCommandType.SwitchPrev });
        Register(config.Hotkeys.SwitchNext, new WorkspaceCommand { Type = WorkspaceCommandType.SwitchNext });
        Register(config.Hotkeys.Switch1, new WorkspaceCommand { Type = WorkspaceCommandType.SwitchToIndex, WorkspaceIndex = 1 });
        Register(config.Hotkeys.Switch2, new WorkspaceCommand { Type = WorkspaceCommandType.SwitchToIndex, WorkspaceIndex = 2 });
        Register(config.Hotkeys.Switch3, new WorkspaceCommand { Type = WorkspaceCommandType.SwitchToIndex, WorkspaceIndex = 3 });
        Register(config.Hotkeys.MoveWindowPrev, new WorkspaceCommand { Type = WorkspaceCommandType.MoveFocusedWindowPrev });
        Register(config.Hotkeys.MoveWindowNext, new WorkspaceCommand { Type = WorkspaceCommandType.MoveFocusedWindowNext });
        Register(config.Hotkeys.Home, new WorkspaceCommand { Type = WorkspaceCommandType.ShowHome });
        Register(config.Hotkeys.Repair, new WorkspaceCommand { Type = WorkspaceCommandType.Repair });
    }

    public void Register(string hotkeyText, WorkspaceCommand command)
    {
        if (string.IsNullOrWhiteSpace(hotkeyText))
            return;

        try
        {
            var hotkey = HotkeyDefinition.Parse(hotkeyText);
            var id = _nextId++;

            if (!Win32.RegisterHotKey(Handle, id, hotkey.Modifiers, hotkey.KeyCode))
            {
                Log.Warn($"Could not register hotkey {hotkeyText}. It may be reserved by another application.");
                return;
            }

            _commands[id] = command;
            Log.Info($"Registered hotkey {hotkeyText} => {command.Type}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to register hotkey {hotkeyText}");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_HOTKEY)
        {
            var id = m.WParam.ToInt32();
            if (_commands.TryGetValue(id, out var command))
            {
                CommandReceived?.Invoke(this, command);
                return;
            }
        }

        base.WndProc(ref m);
    }

    private void UnregisterAll()
    {
        foreach (var id in _commands.Keys.ToArray())
        {
            Win32.UnregisterHotKey(Handle, id);
        }

        _commands.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}
