using System.Globalization;
using System.Text.RegularExpressions;
namespace EquinoxCompanion;

public static class DisplayTime
{
    public static string Local(DateTimeOffset value) => value.ToLocalTime().ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture)+" (local)";
    private static readonly Regex Timestamp = new(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string InText(string text) => Timestamp.Replace(text, m => DateTimeOffset.TryParse(m.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? Local(value) : m.Value);
}
