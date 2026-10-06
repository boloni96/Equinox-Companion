namespace EquinoxCompanion;
public static class CofferStyles
{
    // Persisted indices: append new styles; never reorder existing values.
    public static readonly string[] Names = ["Game chest", "Classic chest", "Rounded chest", "Royal jewel", "Pixel chest", "Minimal outline"];
    public static int Normalize(int style) => style >= 0 && style < Names.Length ? style : 0;
}
