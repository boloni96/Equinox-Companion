namespace EquinoxCompanion;
public sealed class Shortcut
{
    public string Key { get; set; } = "None";
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Matches(string key, bool ctrl, bool alt, bool shift) => Key != "None" && Key == key && Ctrl == ctrl && Alt == alt && Shift == shift;
    public string Label => Key == "None" ? "Unassigned" : (Ctrl ? "Ctrl + " : "") + (Alt ? "Alt + " : "") + (Shift ? "Shift + " : "") + Key.Replace("Key", "");
}
