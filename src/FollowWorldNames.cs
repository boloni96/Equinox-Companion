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

public static class EstateTravelCommand
{
    public static bool CanResume(ulong expectedCharacter,ulong character,uint homeWorld,uint currentWorld) =>
        expectedCharacter!=0&&expectedCharacter==character&&homeWorld!=0&&homeWorld==currentWorld;
    public static bool InteriorArrived(int mode,bool sameEstate,bool workshop,int room) =>
        sameEstate&&(mode==3?workshop:mode==2&&!workshop&&room==0);
    public static bool CanExtendEntry(bool plotConfirmed,int entrySteps,int doorRevealSteps) =>
        plotConfirmed?doorRevealSteps<1:entrySteps<6;
    public static string EntranceName(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name,@"\[.*?\]","").Trim();
    public static bool WaitForEntrance(TimeSpan missingFor) => missingFor<TimeSpan.FromSeconds(15);
    public static bool CanConfirmLogin(bool requested,ulong expectedCharacter,ulong selectedCharacter,
        int expectedSlot,int selectedSlot,string name,string prompt)
    {
        if(!requested||expectedCharacter==0||expectedCharacter!=selectedCharacter||
            expectedSlot<0||expectedSlot!=selectedSlot||string.IsNullOrWhiteSpace(name))return false;
        var start=prompt.IndexOf(name,StringComparison.Ordinal);
        if(start<0)return false;
        var end=start+name.Length;
        return (start==0||!char.IsLetterOrDigit(prompt[start-1]))&&
            (end==prompt.Length||!char.IsLetterOrDigit(prompt[end]));
    }
    // Explicit destinations: private=1, FC=2; plot=0, house interior=2, workshop=3.
    public static (int Property,int Mode,string Label)? Parse(string input) => input.Trim().ToLowerInvariant() switch
    {
        "fc" => (2,0,"FC plot"),
        "priv" or "private" => (1,0,"private plot"),
        "efc" => (2,2,"inside the FC house"),
        "epriv" or "eprivate" => (1,2,"inside the private house"),
        "workshop" => (2,3,"inside the FC workshop"),
        _ => null
    };
}
