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
    // Native layout: regional DC choices, home and current world labels, then destination slots.
    internal static int TableCallback(string[] labels,HashSet<string> centers,string dc,HashSet<string> roster,string expected)
    {
        var headers=new HashSet<string>(StringComparer.Ordinal);
        int start=0;
        while(start<labels.Length && centers.Contains(labels[start])){
            if(!headers.Add(labels[start++]))return -1;
        }
        if(!headers.Contains(dc)||roster.Count is <1 or >8)return -1;
        start+=2;
        if(start+roster.Count>labels.Length)return -1;
        var worlds=labels[start..(start+roster.Count)];
        if(!roster.SetEquals(worlds))return -1;
        return Callback(worlds,expected);
    }

}
