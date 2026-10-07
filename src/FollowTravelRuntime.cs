using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe delegate byte FollowTeleportDelegate(Telepo* telepo,uint id,byte subIndex);
    private Hook<FollowTeleportDelegate>? followTeleportHook;
    private FollowPortalSignal? outgoingTravel,pendingAethernet;
    private Task<bool>? travelAudience;
    private DateTimeOffset travelAt,aethernetAt,aethernetNext;
    private Vector3 travelPosition;
    private uint travelTerritory;
    private int aethernetSelections;
    private bool travelHookFailed,usingSharedTravel,travelSawLoading;
    private bool SharingTravel=>config.EnableFollowThem&&config.FollowThem.SharePortalTransitions;
    private unsafe FollowPortalSignal? TravelSignal(string kind,uint id,string destination,uint crystal,Vector3 position)
    {
        var self=Objects.LocalPlayer;var map=AgentMap.Instance();if(self==null||map==null||!Player.IsLoaded)return null;
        return new(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,Player.CurrentWorld.RowId,self.GameObjectId.ToString(),Client.TerritoryType,map->CurrentMapId,crystal,0,position.X,position.Y,position.Z,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),"",TravelKind:kind,AetheryteId:id,Destination:destination,Approach:kind is "world" or "leaveDuty" or "teleport" or "estate" or "friendestate"?null:FollowTravelPosition.From(self.Position),DutyId:(uint)(FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance()==null?0:FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance()->CurrentContentFinderConditionId));
    }
    private void CaptureTravel(FollowPortalSignal? signal,uint destinationTerritory)
    {
        if(signal==null||config.PairingKey.Length!=64)return;
        var source=Objects.FirstOrDefault(x=>x.BaseId==signal.BaseId&&Vector3.Distance(x.Position,new(signal.X,signal.Y,signal.Z))<1);
        if(source!=null){
            signal=signal with {SourceRadius=Math.Clamp(source.HitboxRadius,0,10)};
            if(signal.TravelKind=="aethernet"&&signal.Approach is {} approach)signal=signal with {Approach=FollowTravelPosition.From(FollowCrystalApproach.Point(source.Position,approach.Point,signal.SourceRadius))};
        }
        portalRelayStatus="Observed "+signal.TravelKind+" destination; waiting for departure.";
        if(outgoingTravel is {} prior&&prior.TravelKind==signal.TravelKind&&prior.AetheryteId==signal.AetheryteId&&prior.Destination==signal.Destination&&DateTimeOffset.UtcNow-travelAt<TimeSpan.FromSeconds(1))return;
        RecordFollowTravel("Travel captured",new {signal.Id,signal.TravelKind,destinationTerritory});
        outgoingTravel=signal;travelSawLoading=false;travelAt=DateTimeOffset.UtcNow;travelTerritory=destinationTerritory;
        travelPosition=Objects.LocalPlayer?.Position??new(signal.X,signal.Y,signal.Z);
        travelAudience=portalRelay.HasFollowers(config.PairingKey,signal.Name,signal.HomeWorld);
    }
    private unsafe byte ObserveFollowTeleport(Telepo* telepo,uint id,byte subIndex)
    {
        FollowPortalSignal? signal=null;uint destination=0;
        try{
            if(SharingTravel&&!SharingWorldIntent&&!usingSharedTravel&&telepo!=null&&Objects.LocalPlayer is {} self)
                foreach(var entry in telepo->TeleportList)if(entry.AetheryteId==id&&entry.SubIndex==subIndex){
                    var estate=AddressOf(entry.HouseId);
                    signal=TravelSignal(estate!=null?"estate":"teleport",id,"",0,self.Position);
                    if(estate!=null&&signal!=null){
                        signal=signal with {EstateId=estate.HouseId,DestinationTerritory=estate.TerritoryTypeId};
                        if(HousingManager.GetOwnedHouseId(EstateType.PersonalEstate).Id==entry.HouseId.Id)
                            signal=signal with {FriendContentId=Player.ContentId.ToString(),Destination="Private Estate"};
                        else if(HousingManager.GetOwnedHouseId(EstateType.FreeCompanyEstate).Id==entry.HouseId.Id)
                            signal=signal with {FriendContentId=Player.ContentId.ToString(),Destination="Free Company Estate"};
                    }
                    else if(subIndex!=0||entry.Ward!=0||entry.Plot!=0)signal=null;
                    destination=entry.TerritoryId;break;
                }
        }catch(Exception e){errorJournal.Record("follow-teleport","Could not observe teleport destination",exceptionType:e.GetType().Name);}
        var accepted=followTeleportHook!.Original(telepo,id,subIndex);
        if(SharingTravel&&!usingSharedTravel)RecordFollowTravel("Teleport observation",new {aetheryte=id,subIndex,accepted=accepted!=0,captured=signal!=null,worldIntent=sharedWorldIntent,reason=signal!=null?"destination captured":SharingWorldIntent?"World travel in progress":"destination not found or unsupported estate"});
        if(accepted!=0&&signal!=null){
            if(transportCapture is {TravelKind:"friendestate",Steps.Length:>0})outgoingTravel=null;
            else CaptureTravel(signal,destination);
        }
        return accepted;
    }
    // Read the native town list; require bounded, typed entries and exact names.
    private unsafe List<(string Name,uint Callback)> AethernetChoices(AtkUnitBase* addon)
    {
        var result=new List<(string,uint)>();if(addon==null||addon->AtkValues==null||addon->AtkValuesCount<282)return result;
        for(var i=0;i<20;i++){
            var n=addon->AtkValues[262+i];var c=addon->AtkValues[9+i*4];var kind=addon->AtkValues[6+i*4];
            if(((int)kind.Type&15) is not (3 or 5))continue;
            if(((int)n.Type&15) is not (8 or 10)||((int)c.Type&15) is not (3 or 5))continue;
            var name=TravelMenuText(n.String.Value);if(FollowAethernetEntry.IsDestination(kind.UInt,name))result.Add((name!,c.UInt));
        }return result;
    }
    private unsafe void ObserveFollowAethernetCallback(AtkUnitBase* addon,uint count,AtkValue* values)
    {
        if(!SharingTravel||usingSharedTravel||!Player.IsLoaded||count!=2||values==null||addon==null||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("TelepotTown").Address)return;
        if(((int)values[0].Type&15) is not (3 or 5)||values[0].Int!=11||((int)values[1].Type&15) is not (3 or 5))return;
        var matches=AethernetChoices(addon).Where(x=>x.Callback==values[1].UInt).ToArray();if(matches.Length!=1||Objects.LocalPlayer is not {} self)return;
        var crystal=Objects.Where(x=>x.ObjectKind==ObjectKind.Aetheryte&&Vector3.Distance(x.Position,self.Position)<=x.HitboxRadius+4).MinBy(x=>Vector3.DistanceSquared(x.Position,self.Position));
        if(crystal==null)return;
        CaptureTravel(TravelSignal("aethernet",0,matches[0].Name,crystal.BaseId,crystal.Position),0);
    }
    private unsafe void UpdateFollowTravel(DateTimeOffset now)
    {
        UpdateFollowBoundary(now);
        UpdateFollowWorldCommandHook();
        UpdateFriendEstateHook();
        UpdateFollowWard(now);
        UpdateFollowTransport(now);
        UpdateFollowWorldTravel(now);
        if(SharingTravel&&!travelHookFailed&&followTeleportHook==null){try{followTeleportHook=Interop.HookFromAddress<FollowTeleportDelegate>(Telepo.MemberFunctionPointers.Teleport,ObserveFollowTeleport);}catch(Exception e){travelHookFailed=true;errorJournal.Record("follow-teleport","Travel observer unavailable",exceptionType:e.GetType().Name);}}
        if(followTeleportHook!=null){if(SharingTravel&&!followTeleportHook.IsEnabled)followTeleportHook.Enable();else if(!SharingTravel&&followTeleportHook.IsEnabled)followTeleportHook.Disable();}
        if(SharingTravel&&callbackHook is {IsEnabled:false})callbackHook.Enable();
        if(SharingTravel&&callbackIntHook is {IsEnabled:false})callbackIntHook.Enable();
        if(!SharingTravel)outgoingTravel=null;
        if(outgoingTravel is {} travel){
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])travelSawLoading=true;
            if(now-travelAt>TimeSpan.FromSeconds(120)||Player.IsLoaded&&(Player.CharacterName!=travel.Name||Player.HomeWorld.RowId!=travel.HomeWorld))outgoingTravel=null;
            else if(Player.IsLoaded&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]&&Objects.LocalPlayer is {} self&&
                FollowDeparturePolicy.Arrived(travel.Territory,travelTerritory,Client.TerritoryType,Vector3.Distance(self.Position,travelPosition),travelSawLoading,travel.TravelKind is "teleport" or "estate")){
                outgoingTravel=null;
                if(travel.TravelKind=="ward"){
                    var housing=HousingManager.Instance();
                    if(housing==null||housing->GetCurrentWard()!=travel.Ward-1||!FollowPortalPolicy.IsConfirmationSupported(travel.Confirmation))return;
                    travel=travel with {DestinationTerritory=Client.TerritoryType};
                }
                if(travelAudience!=null)EnqueueOutgoingTravel(config.PairingKey,travel with {SentAt=now.ToUnixTimeMilliseconds()},travelAudience);
            }
        }
        if(pendingAethernet is not {} pending)return;
        if(!config.EnableFollowThem||!config.FollowThem.UseSharedTeleports||!followSession.Armed||Client.TerritoryType!=pending.Territory||Player.CurrentWorld.RowId!=pending.CurrentWorld||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,pending.Name,pending.HomeWorld)){pendingAethernet=null;return;}
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){pendingAethernet=null;return;}
        if(now-aethernetAt>TimeSpan.FromSeconds(30)){FailFollowTrip("Aethernet timed out waiting for its destination menu.");return;}
        if(!travelStepReady||now<aethernetNext)return;
        RetryTravelInteraction(pending,now);
        var town=(AtkUnitBase*)GardenGui.GetAddonByName("TelepotTown").Address;
        if(town!=null&&town->IsVisible){
            var choices=AethernetChoices(town).Where(x=>x.Name.Trim()==pending.Destination.Trim()).ToArray();
            if(choices.Length!=1){TravelDiagnostic("Waiting for the selected destination in the aethernet menu.");return;}
            var args=stackalloc AtkValue[2];args[0].Type=AtkValueType.Int;args[0].Int=11;args[1].Type=AtkValueType.UInt;args[1].UInt=choices[0].Callback;
            aethernetNext=now.AddSeconds(2);aethernetSelections++;if(aethernetSelections>=2)pendingAethernet=null;
            usingSharedTravel=true;try{town->FireCallback(2,args,true);}finally{usingSharedTravel=false;}
            FollowChatNotice("TRAVEL — Requested aethernet: "+pending.Destination);return;
        }
        var menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;
        if(aethernetSelections==0){
            var choices=TransportChoices(menu);
            var i=choices.FindIndex(x=>x.Trim().TrimEnd('.')=="Aethernet");
            if(i>=0){aethernetNext=now.AddMilliseconds(750);usingSharedTravel=true;try{SelectTravelChoice(menu,i);}finally{usingSharedTravel=false;}}
        }
    }
    private unsafe void TryUseSharedTravel(FollowPortalSignal signal,DateTimeOffset now)
    {
        if(signal.TravelKind=="world"){TryFollowWorldTravel(signal,now);return;}
        if(!config.FollowThem.UseSharedTeleports||Objects.LocalPlayer is not {} self){TravelDiagnostic("Shared travel is disabled or your character is unavailable.");return;}
        if(MatchesAcceptedPartyTrip(signal,now)&&partyTripArrived){travelAwaitingArrival=null;routeArrivalConfirmed=true;RecordFollowTravel("Party relay duplicate skipped",new {signal.Id});return;}
        var map=AgentMap.Instance();
        if(map!=null&&FollowArrivalPolicy.AlreadyAtTravelArrival(signal,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,self.Position,acceptedPartyTeleportAt.ToUnixTimeMilliseconds()>=followArmedAt&&now-acceptedPartyTeleportAt<TimeSpan.FromSeconds(90))){
            travelAwaitingArrival=null;routeArrivalConfirmed=true;TravelDiagnostic("Already at the shared destination; duplicate Teleport skipped.");return;
        }
        if(map==null||!FollowTravelPolicy.CanUse(signal,now.ToUnixTimeMilliseconds(),followArmedAt,config.FollowThem.TargetName,config.FollowThem.HomeWorld,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,lastLeaderEntity,(now-lastLeaderSeen).TotalSeconds,self.Position,config.FollowThem.MeetAtTeleports)){RecordFollowTravel("Travel dispatch rejected",new {signal.Id,signal.TravelKind,distance=Vector3.Distance(self.Position,new(signal.X,signal.Y,signal.Z)),signal.SourceRadius,leaderAge=(now-lastLeaderSeen).TotalSeconds,expectedEntity=signal.EntityId,actualEntity=lastLeaderEntity,sourceTerritory=signal.Territory,currentTerritory=Client.TerritoryType});TravelDiagnostic("Travel instruction rejected: source, range or session validation failed.");return;}
        if(signal.TravelKind=="ward"&&signal.SourceKind=="boundary"){
            var block=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectBlock").Address;
            if(Conditions[ConditionFlag.InCombat]||(block==null||!block->IsVisible)&&!MatchingTravelMenu(signal)){TravelDiagnostic("Walk into the same housing entrance to open ward selection; waiting.");return;}
            lastPortalSignalId=signal.Id;pendingWard=signal;wardAt=now;wardNext=default;wardStage=0;followSession.Pause();return;
        }
        if(FollowTransitionBusy()&&!MatchingTravelMenu(signal)||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]){TravelDiagnostic("Waiting until your character is free to interact.");return;}
        foreach(var name in new[]{"SelectYesno","SelectString","TelepotTown","Talk"}){var ui=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;if(ui!=null&&ui->IsVisible&&!MatchingTravelMenu(signal)){TravelDiagnostic("Close your open dialogue to allow shared travel.");return;}}
        if(signal.TravelKind is "transport" or "friendestate" or "door"){TryFollowTransport(signal,now);return;}
        if(signal.TravelKind is "aethernet" or "ward"){
            var crystal=Objects.FirstOrDefault(x=>(x.ObjectKind==ObjectKind.Aetheryte||signal.TravelKind=="ward"&&signal.SourceKind=="EventNpc"&&x.ObjectKind==ObjectKind.EventNpc)&&x.BaseId==signal.BaseId&&x.IsTargetable&&Vector3.Distance(x.Position,new(signal.X,signal.Y,signal.Z))<1&&Vector3.Distance(x.Position,self.Position)<=x.HitboxRadius+3);
            if(crystal==null){FollowChatNotice("TRAVEL — Move closer to the same crystal; FollowThem is waiting.");return;}
            // Native menus are used consistently; optional Lifestream handles approach/world travel.
            if(signal.TravelKind=="ward"){pendingWard=signal;wardAt=now;wardNext=default;wardStage=0;}
            else pendingAethernet=signal;aethernetAt=now;aethernetNext=now.AddMilliseconds(500);aethernetSelections=0;
            lastPortalSignalId=signal.Id;
            PauseFollowForTravel();
            if(!MatchingTravelMenu(signal))RetryTravelInteraction(signal,now);return;
        }
        lastPortalSignalId=signal.Id;
        if(signal.TravelKind=="teleport"&&config.FollowThem.AcceptPartyTeleports&&Telepo.Instance()!=null&&Telepo.Instance()->ActiveTeleportRequest&&VisibleFollowAddon("SelectYesno")&&FollowParty.Any(x=>FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,x.Name.TextValue,x.World.RowId))){FollowChatNotice("TRAVEL — Waiting for the party teleport offer.");return;}
        var telepo=Telepo.Instance();var inventory=InventoryManager.Instance();if(telepo==null||inventory==null)return;
        telepo->UpdateAetheryteList();
        foreach(var destination in telepo->TeleportList)if((signal.TravelKind=="estate"?destination.HouseId.Id.ToString("X16")==signal.EstateId:destination.AetheryteId==signal.AetheryteId&&destination.SubIndex==0&&destination.Ward==0&&destination.Plot==0)){
            if(destination.GilCost>Math.Max(0,config.FollowThem.TeleportGilLimit)||destination.GilCost>inventory->GetGil()){FollowChatNotice("TRAVEL — Teleport exceeds your gil limit or available gil; waiting.");return;}
            usingSharedTravel=true;try{var ok=telepo->Teleport(destination.AetheryteId,destination.SubIndex);FollowChatNotice(ok?"TRAVEL — Requested the selected character's Teleport destination.":"TRAVEL — Game refused Teleport; FollowThem is waiting.");}finally{usingSharedTravel=false;}return;
        }
        if(signal.TravelKind=="estate"&&signal.FriendContentId.Length>0&&signal.Destination is "Private Estate" or "Free Company Estate"){
            TryFollowTransport(signal with {TravelKind="friendestate",SourceKind="FriendEstate",Steps=[new(signal.Destination)]},now);return;
        }
        FollowChatNotice("TRAVEL — That Teleport destination or exact estate is not available on this character; FollowThem is waiting.");
    }
}
