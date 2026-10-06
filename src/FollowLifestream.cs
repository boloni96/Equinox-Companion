using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool lifestreamTravelOwned;
    private DateTimeOffset lifestreamTravelAt;
    private ulong lifestreamCharacter;
    private uint lifestreamDestination;
    private FollowPortalSignal? worldSource;
    private DateTimeOffset worldSourceAt;
    private bool LifestreamBusy()=>Pi.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc();
    private void DrawLifestreamSettings()
    {
        MessageToggle("Use Lifestream for supported travel",config.FollowThem.UseLifestream,v=>config.FollowThem.UseLifestream=v);
        MessageToggle("Follow World Visits (requires Lifestream)",config.FollowThem.FollowWorldVisits,v=>config.FollowThem.FollowWorldVisits=v);
        MessageToggle("Follow Data Center travel (Lifestream; logs this character out and back in)",config.FollowThem.FollowDataCenters,v=>config.FollowThem.FollowDataCenters=v);
        ImGui.TextWrapped("Optional Lifestream integration. Its own travel restrictions and service-account configuration apply. Companion requests no vnavmesh movement. World/DC travel waits for the leader to arrive before requesting the same World. Queues can take time; use Stop to cancel a request started here.");
    }
    private bool TryLifestreamAethernet(FollowPortalSignal signal)
    {
        if(!config.FollowThem.UseLifestream)return false;
        try{
            if(LifestreamBusy()){TravelDiagnostic("Lifestream is busy; waiting without replacing its task.");return true;}
            if(!Pi.GetIpcSubscriber<string,bool>("Lifestream.AethernetTeleport").InvokeFunc(signal.Destination))return false;
            lastPortalSignalId=signal.Id;BeginLifestreamTravel(0);TravelDiagnostic("Lifestream requested aethernet: "+signal.Destination);return true;
        }catch(Exception){TravelDiagnostic("Lifestream integration unavailable; using the native aethernet menu.");return false;}
    }
    private void BeginLifestreamTravel(uint world)
    {
        PauseFollowForTravel();lifestreamTravelOwned=true;lifestreamTravelAt=DateTimeOffset.UtcNow;
        lifestreamCharacter=Player.ContentId;lifestreamDestination=world;
    }
    private void CancelLifestreamTravel()
    {
        if(!lifestreamTravelOwned)return;lifestreamTravelOwned=false;
        try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception){TravelDiagnostic("Lifestream could not be reached to cancel travel; check its task window.");}
    }
    private void UpdateFollowWorldTravel(DateTimeOffset now)
    {
        if(lifestreamTravelOwned){
            if(!followSession.Armed||!config.EnableFollowThem||!config.FollowThem.UseLifestream||now-lifestreamTravelAt>TimeSpan.FromMinutes(30)||Player.IsLoaded&&Player.ContentId!=lifestreamCharacter){CancelLifestreamTravel();return;}
            try{if(now-lifestreamTravelAt>TimeSpan.FromSeconds(2)&&!LifestreamBusy()){
                lifestreamTravelOwned=false;followReady.Reset();
                TravelDiagnostic(Player.IsLoaded&&(lifestreamDestination==0||Player.CurrentWorld.RowId==lifestreamDestination)?"Lifestream travel finished; waiting for the selected character nearby.":"Lifestream stopped before arrival; waiting. Check its travel settings or queue message.");
            }}catch(Exception){lifestreamTravelOwned=false;TravelDiagnostic("Lifestream became unavailable; waiting.");}
        }
        if(!SharingTravel){worldSource=null;return;}
        if(!Player.IsLoaded||Objects.LocalPlayer is not {} self)return;
        if(worldSource is {} source&&source.Name==Player.CharacterName&&source.HomeWorld==Player.HomeWorld.RowId&&source.CurrentWorld!=Player.CurrentWorld.RowId&&now-worldSourceAt<TimeSpan.FromMinutes(30)&&portalSendTask==null&&config.PairingKey.Length==64){
            var signal=source with {DestinationWorld=Player.CurrentWorld.RowId,SentAt=now.ToUnixTimeMilliseconds()};
            portalSendTask=SendPortalToAudience(config.PairingKey,signal,portalRelay.HasFollowers(config.PairingKey,source.Name,source.HomeWorld));
        }
        worldSource=TravelSignal("world",0,"",0,self.Position);worldSourceAt=now;
    }
    private void TryFollowWorldTravel(FollowPortalSignal s,DateTimeOffset now)
    {
        if(!config.FollowThem.UseLifestream||!config.FollowThem.FollowWorldVisits){TravelDiagnostic("World travel received; enable Lifestream and Follow World Visits to use it.");return;}
        if(!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,s.Name,s.HomeWorld)||s.CurrentWorld!=Player.CurrentWorld.RowId||s.DestinationWorld==0||s.DestinationWorld==s.CurrentWorld||s.EntityId!=lastLeaderEntity||s.SentAt<followArmedAt||s.SentAt<now.ToUnixTimeMilliseconds()-15000||s.ExpiresAt<=now.ToUnixTimeMilliseconds())return;
        if(FollowTransitionBusy()||Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]){TravelDiagnostic("World travel waiting until your character is free.");return;}
        var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        var destination=sheet.GetRowOrDefault(s.DestinationWorld);var current=sheet.GetRowOrDefault(Player.CurrentWorld.RowId);
        if(destination is not {} target||current is not {} from)return;
        var cross=target.DataCenter.RowId!=from.DataCenter.RowId;
        if(cross&&!config.FollowThem.FollowDataCenters){TravelDiagnostic("Data Center travel received; the separate Data Center option is off.");return;}
        try{
            if(LifestreamBusy()){TravelDiagnostic("Lifestream is already busy; not replacing its task.");return;}
            var name=target.Name.ToString();
            if(!Pi.GetIpcSubscriber<string,bool>(cross?"Lifestream.CanVisitCrossDC":"Lifestream.CanVisitSameDC").InvokeFunc(name)){TravelDiagnostic("Lifestream does not offer this destination World; travel remains manual.");return;}
            lastPortalSignalId=s.Id;
            // Explicitly disable secondary teleport and returning to a gateway after arrival.
            Pi.GetIpcSubscriber<string,bool,string,bool,int?,bool?,bool?,object>("Lifestream.TPAndChangeWorld").InvokeAction(name,cross,"",true,null,false,false);
            if(LifestreamBusy()){BeginLifestreamTravel(s.DestinationWorld);TravelDiagnostic("Lifestream requested World: "+name);}
            else TravelDiagnostic("Lifestream declined travel; check its settings and game restrictions.");
        }catch(Exception){TravelDiagnostic("Lifestream travel integration is unavailable; waiting.");}
    }
}
