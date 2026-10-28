namespace EquinoxCompanion;
internal static class RetainerMenuPolicy
{
    // Match the localized action, ignoring the game's trailing inventory/gil
    // counter. Never accept an arbitrary action prefix (e.g. Withdraw and sell).
    internal static string Normalize(string text)
    {
        var value=text.Trim();
        // Addon labels contain the static action; SelectString appends a live
        // parenthesised counter. Handle full-width punctuation in localized UI.
        foreach(var pair in new[]{('(',')'),('（','）')}){
            var start=value.LastIndexOf(pair.Item1);
            if(start>0&&value.EndsWith(pair.Item2)&&value.AsSpan(start).IndexOfAny(':','：')>=0)
                value=value[..start].TrimEnd();
        }
        return string.Join(" ",value.TrimEnd('.', '。').Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    }
    internal static bool Matches(string? actual,string? expected)=>!string.IsNullOrWhiteSpace(expected)&&Normalize(actual??"").Equals(Normalize(expected),StringComparison.OrdinalIgnoreCase);
    internal static bool SelectedMenu(ulong expected,ulong selected,ulong active,bool atBell,IEnumerable<string> entries,string items,string gil)=>expected!=0&&expected==selected&&expected==active&&atBell&&entries.Any(x=>Matches(x,items))&&entries.Any(x=>Matches(x,gil));
}

