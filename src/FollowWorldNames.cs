namespace EquinoxCompanion;
public static class FollowWorldNames
{
    public static (uint Id,string Name)[] Matches(IEnumerable<(uint Id,string Name)> worlds,string query)
    {
        query=query.Trim();if(query.Length==0)return [];
        var all=worlds.ToArray();var exact=all.Where(x=>string.Equals(x.Name,query,StringComparison.OrdinalIgnoreCase)).ToArray();
        return exact.Length>0?exact:all.Where(x=>x.Name.StartsWith(query,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Name).ToArray();
    }
}

public static class FollowWorldIntentPolicy
{
    public static bool Clear(uint destination,TimeSpan age,bool loaded,ulong originalCharacter,ulong character,uint currentWorld)=>
        destination!=0&&(age>=TimeSpan.FromMinutes(30)||loaded&&(originalCharacter!=character||destination==currentWorld));
}
