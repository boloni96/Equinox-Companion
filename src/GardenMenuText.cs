using Dalamud.Game.Text.SeStringHandling;

namespace EquinoxCompanion;

internal static class GardenMenuText
{
    // Native item labels are SeStrings, including colour payloads around the name.
    // Decoding them as UTF-8 leaves those payloads in the identity comparison.
    // Keep native reads bounded and use the game's text format before exact matching.
    internal static unsafe string Read(byte* text)
    {
        if (text == null) return "";
        var length = 0;
        while (length < 2048 && text[length] != 0) length++;
        if (length == 2048) return "";
        return SeString.Parse(new ReadOnlySpan<byte>(text, length)).TextValue.Trim();
    }
}
