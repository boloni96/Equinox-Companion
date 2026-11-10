namespace EquinoxCompanion;
internal static class WorldVisitSelectionPolicy
{
    // The visible World Visit list exposes up to eight world slots. Preserve positions,
    // including blanks, and reject ambiguous names instead of guessing a row.
    internal static int Callback(IReadOnlyList<string> worlds,string expected)
    {
        if(worlds.Count is <1 or >8||string.IsNullOrWhiteSpace(expected))return -1;
        var result=-1;
        for(var i=0;i<worlds.Count;i++)if(string.Equals(worlds[i].Trim(),expected.Trim(),StringComparison.Ordinal)){
            if(result!=-1)return -1;
            result=i+2;
        }
        return result;
    }
}
