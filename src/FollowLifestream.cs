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
    private bool LifestreamBusy(){try{return Pi.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc();}catch{return false;}}
    private void DrawLifestreamSettings()
    {
        MessageToggle("Use Lifestream for supported travel",config.FollowThem.UseLifestream,v=>config.FollowThem.UseLifestream=v);
        ImGui.SameLine();
        if(ImGui.SmallButton("Install guide##FollowLifestream"))
        {
            try { Dalamud.Utility.Util.OpenLink("https://github.com/NightmareXIV/Lifestream#installation"); }
            catch(Exception) { PrintEquinox("[FollowThem] Could not open the Lifestream installation page. Visit https://github.com/NightmareXIV/Lifestream#installation"); }
        }
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Open Lifestream's installation instructions in your browser.");
        SettingsHelp("The optional legacy approach routes require Lifestream. Native world/DC travel and aethernet do not.");
        MessageToggle("Follow World Visits (Companion)",config.FollowThem.FollowWorldVisits,v=>config.FollowThem.FollowWorldVisits=v);
        MessageToggle("Follow Data Center travel (Companion; logs this character out and back in)",config.FollowThem.FollowDataCenters,v=>config.FollowThem.FollowDataCenters=v);
        SettingsHelp("World/DC travel uses Companion native menus and the verified service account. Lifestream remains optional for other legacy routes. Use /et WorldName (unique prefixes accepted, e.g. /et sir); /eqtravel is also supported; /equinox travel WorldName also works to share the destination before World/DC departure. Observed /li WorldName commands also share when available; menu travel has an arrival fallback. Queues can take time; use Stop to cancel a request started here.");
    }
    private bool TryLifestreamAethernet(FollowPortalSignal signal)
    {
        if(!config.FollowThem.UseLifestream)return false;
        try{
            if(LifestreamBusy()){TravelDiagnostic("Lifestream is busy; waiting without replacing its task.");return true;}
            if(!RequestUnlockedAethernet(ResolveAethernetDestination(signal.Destination,signal.BaseId),out var reason)){TravelDiagnostic(reason);return false;}
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
            try{if(Player.IsLoaded&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]&&now-lifestreamTravelAt>TimeSpan.FromSeconds(2)&&!LifestreamBusy()){
                lifestreamTravelOwned=false;followReady.Reset();
                if(lifestreamDestination==0||Player.CurrentWorld.RowId==lifestreamDestination)ResumeAfterConfirmedTravel();
                TravelDiagnostic(Player.IsLoaded&&(lifestreamDestination==0||Player.CurrentWorld.RowId==lifestreamDestination)?"Lifestream travel finished; waiting for the selected character nearby.":"Lifestream stopped before arrival; waiting. Check its travel settings or queue message.");
            }}catch(Exception){lifestreamTravelOwned=false;TravelDiagnostic("Lifestream became unavailable; waiting.");}
        }
        if(!SharingTravel){worldSource=null;sharedWorldIntent=0;announcedWorldTrip=null;return;}
        _=SharingWorldIntent; // Also clears arrival even when worldSource was lost.
        if(sharedWorldIntent!=0&&Player.IsLoaded&&now-sharedWorldIntentAt>TimeSpan.FromSeconds(15)){
            try { if(nativeWorldTrip==null&&!LifestreamBusy()){RecordFollowTravel("World intent cleared",new {destination=sharedWorldIntent,reason="Lifestream idle"});sharedWorldIntent=0;} } catch(Exception){sharedWorldIntent=0;}
        }
        if(!Player.IsLoaded||Objects.LocalPlayer is not {} self)return;
        if(worldSource is {} source&&source.Name==Player.CharacterName&&source.HomeWorld==Player.HomeWorld.RowId&&source.CurrentWorld!=Player.CurrentWorld.RowId&&now-worldSourceAt<TimeSpan.FromMinutes(30)&&config.PairingKey.Length==64){
            var signal=source with {DestinationWorld=Player.CurrentWorld.RowId,SentAt=now.ToUnixTimeMilliseconds()};
            if(announcedWorldKey==config.PairingKey&&FollowWorldReplayPolicy.SuppressFallback(announcedWorldTrip,source.Name,source.HomeWorld,signal.DestinationWorld,now.ToUnixTimeMilliseconds(),SharingWorldIntent))
                RecordFollowTravel("World arrival duplicate suppressed",new {destination=signal.DestinationWorld,announced=announcedWorldTrip!.Id});
            else EnqueueOutgoingTravel(config.PairingKey,signal,portalRelay.HasFollowers(config.PairingKey,source.Name,source.HomeWorld));
            if(sharedWorldIntent==Player.CurrentWorld.RowId||!SharingWorldIntent)sharedWorldIntent=0;
        }
        if(announcedWorldTrip is {} announced&&(announced.DestinationWorld==Player.CurrentWorld.RowId||announced.Name!=Player.CharacterName||announced.HomeWorld!=Player.HomeWorld.RowId||now.ToUnixTimeMilliseconds()-announced.SentAt>=1800000))announcedWorldTrip=null;
        worldSource=TravelSignal("world",0,"",0,self.Position);worldSourceAt=now;
    }
    private void TryFollowWorldTravel(FollowPortalSignal s,DateTimeOffset now)
    {
        if(!config.FollowThem.FollowWorldVisits){TravelDiagnostic("World travel received; enable Follow World Visits to use it.");return;}
        if(!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,s.Name,s.HomeWorld)||s.CurrentWorld!=Player.CurrentWorld.RowId||s.DestinationWorld==0||s.DestinationWorld==s.CurrentWorld||s.EntityId!=lastLeaderEntity||s.SentAt<followArmedAt||s.SentAt<now.ToUnixTimeMilliseconds()-120000||s.ExpiresAt<=now.ToUnixTimeMilliseconds())return;
        if(FollowTransitionBusy()||Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]){TravelDiagnostic("World travel waiting until your character is free.");return;}
        var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        var destination=sheet.GetRowOrDefault(s.DestinationWorld);var current=sheet.GetRowOrDefault(Player.CurrentWorld.RowId);
        if(destination is not {} target||current is not {} from)return;
        var cross=target.LogicalDc().RowId!=from.LogicalDc().RowId;
        if(cross&&!config.FollowThem.FollowDataCenters){TravelDiagnostic("Data Center travel received; the separate Data Center option is off.");return;}
        try{
            if(nativeWorldTrip!=null||LifestreamBusy()){TravelDiagnostic("Travel is already busy; waiting.");return;}
            PauseFollowForTravel();
            if(!StartNativeWorldTravel(s.DestinationWorld,"follow")){TravelDiagnostic(nativeTravelStatus);return;}
            lastPortalSignalId=s.Id;TravelDiagnostic("Companion requested world: "+target.Name.ToString());
        }catch(Exception){TravelDiagnostic("Native world travel could not start; waiting.");}
    }
}
