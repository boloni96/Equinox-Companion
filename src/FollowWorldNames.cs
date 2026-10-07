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

public static class FollowWorldReplayPolicy
{
    public static bool Announced(FollowPortalSignal? receipt,string name,uint home,uint destination,long now)=>
        receipt is not null && receipt.TravelKind=="world" && receipt.DestinationWorld==destination &&
        FollowThemSession.Matches(name,home,receipt.Name,receipt.HomeWorld) && now>=receipt.SentAt && now-receipt.SentAt<1800000;
    public static bool SuppressFallback(FollowPortalSignal? receipt,string name,uint home,uint destination,long now,bool activeIntent)=>
        Announced(receipt,name,home,receipt?.DestinationWorld??0,now)&&(activeIntent||receipt!.DestinationWorld==destination);
    public static bool AlreadyArrived(FollowPortalSignal s,string name,uint home,uint currentWorld,long armedAt,long now)=>
        s.TravelKind=="world" && s.DestinationWorld!=0 && s.DestinationWorld!=s.CurrentWorld && s.DestinationWorld==currentWorld &&
        FollowThemSession.Matches(name,home,s.Name,s.HomeWorld) && s.SentAt>=armedAt && s.SentAt>=now-120000 && s.SentAt<=now+5000 &&
        s.ExpiresAt>now && s.ExpiresAt<=now+125000;
}
