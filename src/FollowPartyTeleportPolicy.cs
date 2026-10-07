namespace EquinoxCompanion;
public static class FollowPartyTeleportPolicy
{
    // Match the accepted destination and capture time, not the leader's exact
    // spawn coordinates: party members need not spawn at the same point/height.
    public static bool Matches(FollowPortalSignal s,uint aetheryte,uint world,long accepted,long now,long armed)=>
        aetheryte!=0&&s.TravelKind=="teleport"&&s.AetheryteId==aetheryte&&s.CurrentWorld==world&&accepted>=armed&&
        now>=accepted&&now-accepted<120000&&Math.Abs(s.SentAt-accepted)<=20000;
}

public static class FollowPartyDestination
{
    public static string Normalize(string value)
    {
        var text=string.Join(" ",value.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
        return text.StartsWith("the ",StringComparison.OrdinalIgnoreCase)?text[4..]:text;
    }
    public static (uint Id,uint Territory)? Resolve(string name,IEnumerable<(uint Id,uint Territory,string Name)> destinations)
    {
        var query=Normalize(name);if(query.Length==0)return null;
        var matches=destinations.Where(x=>string.Equals(Normalize(x.Name),query,StringComparison.OrdinalIgnoreCase)).Select(x=>(x.Id,x.Territory)).Distinct().Take(2).ToArray();
        return matches.Length==1?matches[0]:null;
    }
    public static bool Hold(long accepted,long now,long armed,bool arrived)=>
        accepted>0&&accepted>=armed&&now>=accepted&&now-accepted<45000&&!arrived;
    public static bool UnknownArrivalMatches(FollowPortalSignal s,uint world,uint territory,uint map,uint instance,long accepted,long now,long armed)=>
        accepted>0&&accepted>=armed&&now>=accepted&&now-accepted<120000&&Math.Abs(s.SentAt-accepted)<=20000&&
        s.TravelKind=="teleport"&&s.CurrentWorld==world&&s.ArrivalWorld==world&&territory!=0&&s.ArrivalTerritory==territory&&s.ArrivalMap==map&&FollowInstancePolicy.Arrived(s.ArrivalInstance,instance);
}
