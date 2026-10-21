using System.Runtime.InteropServices;
namespace EquinoxCompanion;

internal static class ShortcutKeyboard
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    public static bool OwnGameFocused()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var window = GetForegroundWindow();
        if (window == 0) return false;
        GetWindowThreadProcessId(window, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    // Read only while this FFXIV process owns the foreground window. No key
    // injection, background capture, typed text recording or global hotkeys.
    // Physical down state survives ImGui/game consumption of a key event.
    public static bool Down(int key, bool focused) => focused && key is > 0 and < 255
        && OperatingSystem.IsWindows() && (GetAsyncKeyState(key) & 0x8000) != 0;
}
