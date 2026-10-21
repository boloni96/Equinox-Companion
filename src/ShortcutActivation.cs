namespace EquinoxCompanion;

// Saved ImGui key names are stable configuration; activation reads the game's
// virtual-key state, independently of ImGui focus/key ownership.
internal static class ShortcutKeyMap
{
    public static int VirtualKey(string key)
    {
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z') return key[0];
        if (key.StartsWith("F", StringComparison.Ordinal) && int.TryParse(key.AsSpan(1), out var f) && f is >= 1 and <= 24) return 0x70 + f - 1;
        if (key.Length == 4 && key.StartsWith("Key", StringComparison.Ordinal) && key[3] is >= '0' and <= '9') return key[3];
        if (key.Length == 7 && key.StartsWith("Keypad", StringComparison.Ordinal) && key[6] is >= '0' and <= '9') return 0x60 + key[6] - '0';
        return key switch
        {
            "Space" => 0x20, "Tab" => 0x09, "Backspace" => 0x08, "Insert" => 0x2D,
            "Delete" => 0x2E, "Home" => 0x24, "End" => 0x23, "PageUp" => 0x21,
            "PageDown" => 0x22, "LeftArrow" => 0x25, "RightArrow" => 0x27,
            "UpArrow" => 0x26, "DownArrow" => 0x28, _ => 0
        };
    }
}

internal sealed class ShortcutActivation
{
    private string lastKey = "";
    private bool lastCtrl, lastAlt, lastShift, wasDown;

    public void Reset() { lastKey = ""; wasDown = false; }

    public bool Step(Shortcut binding, bool down, bool ctrl, bool alt, bool shift, bool super, bool blocked)
    {
        if (binding.Key != lastKey || binding.Ctrl != lastCtrl || binding.Alt != lastAlt || binding.Shift != lastShift)
        {
            // Newly assigned shortcuts must be released before they can fire.
            lastKey = binding.Key; lastCtrl = binding.Ctrl; lastAlt = binding.Alt; lastShift = binding.Shift;
            wasDown = down;
            return false;
        }
        var pressed = down && !wasDown;
        wasDown = down; // Consume presses even during typing/capture/unfocus.
        return pressed && !blocked && !super && ShortcutKeyMap.VirtualKey(binding.Key) != 0
            && binding.Matches(binding.Key, ctrl, alt, shift);
    }
}
