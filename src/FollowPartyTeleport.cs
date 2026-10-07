using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint acceptedPartyAetheryte,acceptedPartyTerritory,acceptedPartyWorld;
    private bool partyTripSawLoading,partyTripArrived;
    private uint partyArrivalTerritory,partyArrivalMap,partyArrivalInstance;
    private void CapturePartyTeleport(string prompt,DateTimeOffset now)
    {
        partyArrivalTerritory=partyArrivalMap=partyArrivalInstance=0;
        acceptedPartyTeleportAt=now;acceptedPartyAetheryte=0;acceptedPartyTerritory=0;acceptedPartyWorld=Player.CurrentWorld.RowId;partyTripSawLoading=false;partyTripArrived=false;
        const string prefix="Accept Teleport to ";
        if(!prompt.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!prompt.EndsWith('?'))return;
        var name=prompt[prefix.Length..^1];
        var match=FollowPartyDestination.Resolve(name,DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().Where(x=>x.IsAetheryte).Select(x=>(x.RowId,x.Territory.RowId,x.PlaceName.Value.Name.ToString())));
        if(match is {} found){acceptedPartyAetheryte=found.Id;acceptedPartyTerritory=found.Territory;}
        if(match==null)RecordFollowTravel("Party destination unresolved",new {destination=name,reason="No unique game-data match; waiting for observed arrival"});
        RecordFollowTravel("Party teleport accepted",new {aetheryte=acceptedPartyAetheryte,territory=acceptedPartyTerritory});
    }
    private unsafe void ObservePartyTeleport(DateTimeOffset now,bool loading)
    {
        if(acceptedPartyTeleportAt==default||acceptedPartyTeleportAt.ToUnixTimeMilliseconds()<followArmedAt||now-acceptedPartyTeleportAt>TimeSpan.FromSeconds(120))return;
        partyTripSawLoading|=loading;
        if(!partyTripArrived&&partyTripSawLoading&&!loading&&Player.IsLoaded&&Player.CurrentWorld.RowId==acceptedPartyWorld&&(acceptedPartyTerritory==0||Client.TerritoryType==acceptedPartyTerritory)&&Objects.LocalPlayer!=null&&AgentMap.Instance()!=null){
            partyArrivalTerritory=Client.TerritoryType;partyArrivalMap=AgentMap.Instance()->CurrentMapId;partyArrivalInstance=CurrentFollowInstance();
            partyTripArrived=true;RecordFollowTravel("Party teleport arrival confirmed",new {aetheryte=acceptedPartyAetheryte,territory=partyArrivalTerritory,map=partyArrivalMap});
        }
    }
    private bool MatchesAcceptedPartyTrip(FollowPortalSignal s,DateTimeOffset now)=>
        FollowPartyTeleportPolicy.Matches(s,acceptedPartyAetheryte,acceptedPartyWorld,acceptedPartyTeleportAt.ToUnixTimeMilliseconds(),now.ToUnixTimeMilliseconds(),followArmedAt)||acceptedPartyAetheryte==0&&partyTripArrived&&FollowPartyDestination.UnknownArrivalMatches(s,acceptedPartyWorld,partyArrivalTerritory,partyArrivalMap,partyArrivalInstance,acceptedPartyTeleportAt.ToUnixTimeMilliseconds(),now.ToUnixTimeMilliseconds(),followArmedAt);
    private bool HoldAcceptedPartyTeleport(DateTimeOffset now)=>FollowPartyDestination.Hold(acceptedPartyTeleportAt.ToUnixTimeMilliseconds(),now.ToUnixTimeMilliseconds(),followArmedAt,partyTripArrived);
}
