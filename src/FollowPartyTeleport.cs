namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint acceptedPartyAetheryte,acceptedPartyTerritory,acceptedPartyWorld;
    private bool partyTripSawLoading,partyTripArrived;
    private void CapturePartyTeleport(string prompt,DateTimeOffset now)
    {
        acceptedPartyTeleportAt=now;acceptedPartyAetheryte=0;acceptedPartyTerritory=0;acceptedPartyWorld=Player.CurrentWorld.RowId;partyTripSawLoading=false;partyTripArrived=false;
        const string prefix="Accept Teleport to ";
        if(!prompt.StartsWith(prefix,StringComparison.Ordinal)||!prompt.EndsWith('?'))return;
        var name=prompt[prefix.Length..^1];
        var matches=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().Where(x=>x.IsAetheryte&&x.PlaceName.Value.Name.ToString()==name).Take(2).ToArray();
        if(matches.Length==1){acceptedPartyAetheryte=matches[0].RowId;acceptedPartyTerritory=matches[0].Territory.RowId;}
        RecordFollowTravel("Party teleport accepted",new {aetheryte=acceptedPartyAetheryte,territory=acceptedPartyTerritory});
    }
    private void ObservePartyTeleport(DateTimeOffset now,bool loading)
    {
        if(acceptedPartyAetheryte==0||now-acceptedPartyTeleportAt>TimeSpan.FromSeconds(120))return;
        partyTripSawLoading|=loading;
        if(!partyTripArrived&&partyTripSawLoading&&!loading&&Player.IsLoaded&&Player.CurrentWorld.RowId==acceptedPartyWorld&&Client.TerritoryType==acceptedPartyTerritory){
            partyTripArrived=true;RecordFollowTravel("Party teleport arrival confirmed",new {aetheryte=acceptedPartyAetheryte,territory=acceptedPartyTerritory});
        }
    }
    private bool MatchesAcceptedPartyTrip(FollowPortalSignal s,DateTimeOffset now)=>
        FollowPartyTeleportPolicy.Matches(s,acceptedPartyAetheryte,acceptedPartyWorld,acceptedPartyTeleportAt.ToUnixTimeMilliseconds(),now.ToUnixTimeMilliseconds(),followArmedAt);
}
