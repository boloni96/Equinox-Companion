namespace EquinoxCompanion;
internal static class RetainerMenuPolicy
{
    // Ignore presentation punctuation/spacing, never accept arbitrary substring matches.
    internal static string Normalize(string text)=>string.Join(" ",text.Trim().TrimEnd('.', '。').Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    internal static bool Matches(string? actual,string? expected)=>!string.IsNullOrWhiteSpace(expected)&&Normalize(actual??"").Equals(Normalize(expected),StringComparison.OrdinalIgnoreCase);
}
