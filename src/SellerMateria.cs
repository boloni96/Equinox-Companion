using System.Globalization;

namespace EquinoxCompanion;

// Canonical multiset of game materia type/grade pairs. Empty means observed
// plain; null means not observed. Order does not change the finished item's
// stats, but type, grade and repeated materia do. Do not compare by count alone.
public static class SellerMateria
{
    public static string? FromParts(IEnumerable<(int Type, int Grade)> parts, int? expected = null)
    {
        var rows = parts.Take(6).ToArray();
        if (rows.Length > 5 || expected is < 0 or > 5 || expected != null && expected != rows.Length ||
            rows.Any(x => x.Type is < 1 or > 255 || x.Grade is < 0 or > 15)) return null;
        return string.Join(",", rows.Select(x => (x.Type << 4) | x.Grade).Order().Select(x => x.ToString(CultureInfo.InvariantCulture)));
    }

    public static string? FromPacked(ReadOnlySpan<ushort> slots, int count)
    {
        if (count is < 0 or > 5 || slots.Length < count) return null;
        var pairs = new List<(int, int)>();
        foreach (var value in slots[..count])
        {
            if (value > 0xFFF) return null;
            pairs.Add((value >> 4, value & 15));
        }
        return FromParts(pairs, count);
    }

    public static string? FromInventory(ReadOnlySpan<ushort> types, ReadOnlySpan<byte> grades)
    {
        if (types.Length != 5 || grades.Length != 5) return null;
        var pairs = new List<(int, int)>();
        for (var i = 0; i < 5; i++) if (types[i] != 0) pairs.Add((types[i], grades[i]));
        return FromParts(pairs);
    }

    public static bool Known(string? value)
    {
        if (value == "") return true;
        if (value == null || value.Length > 24) return false;
        var values = value.Split(',');
        if (values.Length > 5) return false;
        var last = 0;
        foreach (var part in values)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n is < 16 or > 4095 || n < last ||
                part != n.ToString(CultureInfo.InvariantCulture)) return false;
            last = n;
        }
        return true;
    }

    public static bool Matches(string? left, string? right) => Known(left) && Known(right) && left == right;
    public static string Describe(string? value) => !Known(value) ? "Materia not recorded" : value == "" ? "No materia" : $"{value!.Split(',').Length} materia · same types and grades";
}
