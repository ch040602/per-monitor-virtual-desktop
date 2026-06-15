# Logitech setup

Use Logitech Options+ or Logi Options+ to map mouse gestures/buttons to PerMonitorVD hotkeys.

Recommended mapping:

```text
Gesture left   -> Ctrl + Alt + Shift + Left
Gesture right  -> Ctrl + Alt + Shift + Right
Gesture up     -> Ctrl + Alt + Shift + Up
Gesture down   -> Ctrl + Alt + Shift + Down
Gesture click  -> Ctrl + Alt + Shift + R, or leave unmapped
```

Do not use Logitech's built-in Virtual Desktop profile at first, because that usually sends Windows' native global virtual desktop command.

After validating PerMonitorVD with custom hotkeys, you can test `EnableWinCtrlOverride: true` in config. That mode intercepts `Win+Ctrl+Left/Right` using a low-level keyboard hook and suppresses the native global switch.
