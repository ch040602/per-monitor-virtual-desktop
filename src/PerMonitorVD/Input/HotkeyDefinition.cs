using PerMonitorVD.Native;

namespace PerMonitorVD.Input;

public sealed record HotkeyDefinition(uint Modifiers, uint KeyCode)
{
    public static HotkeyDefinition Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Hotkey text is empty.", nameof(text));

        uint modifiers = Win32.MOD_NOREPEAT;
        uint keyCode = 0;

        foreach (var rawPart in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                modifiers |= Win32.MOD_CONTROL;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= Win32.MOD_ALT;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= Win32.MOD_SHIFT;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= Win32.MOD_WIN;
            else
                keyCode = ParseKey(part);
        }

        if (keyCode == 0)
            throw new ArgumentException($"Hotkey '{text}' has no key code.", nameof(text));

        return new HotkeyDefinition(modifiers, keyCode);
    }

    private static uint ParseKey(string text)
    {
        return text.ToUpperInvariant() switch
        {
            "LEFT" => (uint)Keys.Left,
            "RIGHT" => (uint)Keys.Right,
            "UP" => (uint)Keys.Up,
            "DOWN" => (uint)Keys.Down,
            "SPACE" => (uint)Keys.Space,
            "ENTER" => (uint)Keys.Enter,
            "ESC" or "ESCAPE" => (uint)Keys.Escape,
            _ when text.Length == 1 && char.IsLetter(text[0]) => (uint)char.ToUpperInvariant(text[0]),
            _ when int.TryParse(text, out var digit) && digit is >= 0 and <= 9 => (uint)((int)Keys.D0 + digit),
            _ => Enum.TryParse<Keys>(text, ignoreCase: true, out var key) ? (uint)key : 0
        };
    }
}
