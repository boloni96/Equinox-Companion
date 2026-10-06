namespace EquinoxCompanion;
public static class FollowInstancePolicy
{
    public static bool Arrived(uint expected,uint actual)=>expected==0||expected==actual;
    public static int Choice(IReadOnlyList<string> entries,uint instance)
    {
        if(instance is <1 or >9)return -1;
        // Require an actual instance list with at least two numbered destinations.
        var matches=entries.Select((text,index)=>(text,index)).Where(x=>x.text.Contains((char)(0xE0B0+instance))).ToArray();
        var numbered=entries.Count(x=>x.Any(c=>c is >= '\uE0B1' and <= '\uE0B9'));
        return numbered>=2&&matches.Length==1?matches[0].index:-1;
    }
}
