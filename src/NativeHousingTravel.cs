using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed class NativeHousingTrip
    {
        public required uint World;public required ulong Character;public required int Ward,Plot;
        public required NativeHousingDistrict District;public required Task<HousingGeometry> Geometry;
        public required DateTimeOffset Started;public DateTimeOffset Next,StageAt;
        public bool WardOnly;public ulong SourceObject;public uint ShardOnly;
        public int Stage,Attempt,Selections;public bool WorldSent,TeleportSent,WardSelected,MenuOpened;
        public readonly GardenArrivalGate EntryArrival=new();public bool NearApproachSelected;public Vector3? Approach;public HousingShard? Shard;public Vector3 Source;
    }
    private NativeHousingTrip? nativeHousingTrip;
    private string nativeHousingStatus="";
    private void HousingStage(NativeHousingTrip trip,int stage,string status)
    {
        trip.Stage=stage;trip.StageAt=DateTimeOffset.UtcNow;trip.Approach=null;trip.NearApproachSelected=false;nativeHousingStatus=bidTravelMessage=characterTravelStatus=status;
        RecordTravelPrompt("Housing travel step",new{stage,status,trip.World,trip.Ward,trip.Plot,territory=trip.District.Territory});
    }
    private void StopNativeHousing(string reason)
    {
        if(nativeHousingTrip==null)return;
        nativeHousingTrip=null;
        if(nativeWorldTrip?.Owner=="housing")StopNativeWorldTravel(reason);
        StopIndependentWalk(reason);nativeHousingStatus=bidTravelMessage=characterTravelStatus=reason;
        RecordTravelPrompt("Housing travel stopped",new{reason});FollowChatNotice("TRAVEL — "+reason);
    }
    private void StartNativeHousing(uint world,string district,int ward,int plot,bool wardOnly=false,ulong sourceObject=0,uint shardOnly=0)
    {
        if(!CanRequestWorldTravel(world,out var reason)){bidTravelMessage=characterTravelStatus=reason;return;}
        var area=NativeHousingPolicy.District(district);
        if(area==null||!NativeHousingPolicy.Address(ward,plot)){bidTravelMessage="Choose a supported housing district, ward 1–30 and plot 1–60.";return;}
        if(nativeHousingTrip!=null||NativeGardenWalking||nativeWorldTrip!=null||nativeAethernetTrip!=null||estateTrip!=null||assignedLogin!=null||independentQuestId!=0||nativeEventActive||keyboundBuying||independentMoving||independentSearch!=null||independentPath!=null||followSession.Armed||LifestreamBusy()){
            bidTravelMessage="Another journey is active. Stop it before starting GO SEE.";return;
        }
        var now=DateTimeOffset.UtcNow;
        var trip=new NativeHousingTrip{World=world,Character=Player.ContentId,Ward=ward,Plot=plot,District=area,Geometry=wardOnly?Task.FromResult(new HousingGeometry([],[],[])):LoadHousingGeometry(area),Started=now,WardOnly=wardOnly,SourceObject=sourceObject,ShardOnly=shardOnly};
        nativeHousingTrip=trip;HousingStage(trip,0,"Reading game housing destinations");
    }
    private void BeginNativeEstateLanding(uint world,string district,int ward,int plot){
        var area=NativeHousingPolicy.District(district)??throw new InvalidOperationException("Unknown estate district.");
        var now=DateTimeOffset.UtcNow;
        nativeHousingTrip=new(){World=world,Character=Player.ContentId,Ward=ward,Plot=plot,District=area,Geometry=LoadHousingGeometry(area),Started=now};
        HousingStage(nativeHousingTrip,12,"Waiting for estate teleport arrival");
    }
    private unsafe bool HousingAtWard(NativeHousingTrip trip)
    {
        var housing=HousingManager.Instance();return housing!=null&&Client.TerritoryType==trip.District.Territory&&
            housing->GetCurrentHousingTerritoryType()==HousingTerritoryType.Outdoor&&housing->GetCurrentWard()==trip.Ward-1;
    }
    private unsafe bool HousingApproach(NativeHousingTrip trip,IGameObject target,DateTimeOffset now)
    {
        if(TargetWithinInteractionRange(target)){StopIndependentWalk("Within reach of housing travel target");return true;}
        var self=Objects.LocalPlayer!;
        if(IndependentWalk(target.Position,InteractionLimit(target),now,allowFlight:false,interactionTarget:target,useInteractionRegion:true)){
            if(TargetWithinInteractionRange(target))return true;
            if(trip.Attempt>=2)throw new InvalidOperationException("Reached the housing approach, but the target is still not within verified interaction reach. Export the travel report.");
            trip.Attempt++;trip.Approach=null;
            RecordTravelPrompt("Housing approach retry",new{target=target.BaseId,trip.Attempt,position=FollowTravelPosition.From(self.Position),targetPosition=FollowTravelPosition.From(target.Position),reason="Walking arrived without verified interaction reach"});
            StopIndependentWalk("Trying a closer housing interaction approach");
        }
        nativeHousingStatus="Approaching housing travel target · "+independentWalkStatus;return false;
    }
    private unsafe bool HousingInteract(IGameObject target)
    {
        if(Targets.Target?.Address!=target.Address){Targets.Target=target;return false;}
        var system=TargetSystem.Instance();if(system==null)return false;
        usingSharedTravel=true;try{system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address,false);}finally{usingSharedTravel=false;}
        RecordTravelPrompt("Housing target interaction",new{target=target.BaseId,distance=Vector3.Distance(Objects.LocalPlayer!.Position,target.Position)});return true;
    }
    private unsafe void UpdateNativeHousing(DateTimeOffset now)
    {
        if(nativeHousingTrip is not {} trip||now<trip.Next)return;trip.Next=now.AddMilliseconds(trip.Stage==12?100:500);
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try{
            if(now-trip.Started>TimeSpan.FromMinutes(15))throw new InvalidOperationException("Housing travel timed out.");
            if(trip.Stage!=1&&now-trip.StageAt>TimeSpan.FromMinutes(3))throw new InvalidOperationException("Housing travel timed out at stage "+trip.Stage+". Export the report.");
            if(!Player.IsLoaded){if(trip.Stage!=1&&now-trip.StageAt>TimeSpan.FromSeconds(30))throw new InvalidOperationException("Housing travel stopped: character logged out.");return;}
            if(Player.ContentId!=trip.Character)throw new InvalidOperationException("Housing travel stopped: character changed.");
            if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty])throw new InvalidOperationException("Housing travel interrupted by combat or duty.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.Casting])return;
            var self=Objects.LocalPlayer;if(self==null)return;
            if(trip.WardOnly&&Player.CurrentWorld.RowId==trip.World&&HousingAtWard(trip)){
                StopNativeHousing($"Arrived at {trip.District.Name} · Ward {trip.Ward}.");return;
            }
            // GO SEE owns arrival only. Garden care and house entry own their
            // inward movement; an estate teleport must not start a yard route.
            if(trip.Stage==12){
                var pending=now-trip.Started<TimeSpan.FromSeconds(3)||!HousingAtWard(trip)||Player.CurrentWorld.RowId!=trip.World||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("TeleportHousingFriend")||
                    FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance()!=null&&FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance()->ActiveTeleportRequest;
                if(!trip.EntryArrival.Ready(pending,self.Position,now))return;
                var housing=HousingManager.Instance();var at=housing==null?null:AddressOf(housing->GetCurrentHouseId());
                if(at!=null&&(at.WorldId!=trip.World||at.Ward!=trip.Ward||at.Plot!=trip.Plot))throw new InvalidOperationException("Estate landing reached a different plot.");
                if(at==null&&!trip.Geometry.IsCompleted)return;
                if(at==null&&!trip.Geometry.IsCompletedSuccessfully)throw new InvalidOperationException("Estate landing geometry is unavailable; arrival could not be verified.");
                var placard=at!=null?self.Position:trip.Geometry.Result.Placards[trip.Plot-1];
                if(NativeHousingPolicy.GoSeeArrival(true,at!=null,self.Position,placard,true,true)){
                    goSeeNativeUntil=default;
                    StopNativeHousing($"Arrived at {trip.District.Name} W{trip.Ward} P{trip.Plot} · GO SEE complete.");return;
                }
                if(now-trip.StageAt>TimeSpan.FromSeconds(30))throw new InvalidOperationException("Estate teleport arrival could not be confirmed near the requested plot. No inward walk started.");
                nativeHousingStatus="Waiting for arrival at the requested estate entrance";return;
            }
            // Street navigation still needs collision, but teleport completion does not.
            if(trip.Stage>=2&&!GameWalkingCollision.Available()){
                nativeHousingStatus="Waiting for housing collision geometry";return;
            }
            if(trip.Stage==0){
                if(trip.WardOnly){HousingStage(trip,1,"Reaching the housing ward");return;}
                if(!trip.Geometry.IsCompleted)return;
                if(!trip.Geometry.IsCompletedSuccessfully)throw new InvalidOperationException(trip.Geometry.Exception?.GetBaseException().Message??"Housing layout unavailable.");
                var data=trip.Geometry.Result;
                if(trip.ShardOnly!=0){
                    trip.Shard=data.Shards.SingleOrDefault(s=>s.Id==trip.ShardOnly)??throw new InvalidOperationException("The requested residential shard is unavailable.");
                    HousingStage(trip,1,"Reaching the residential aethernet");return;
                }
                var placard=data.Placards[trip.Plot-1];
                var ordered=data.Shards.OrderBy(s=>s.Order).ToArray();var half=ordered.Length/2;
                trip.Shard=ordered.Where((s,i)=>(i>=half)==(trip.Plot>30)).MinBy(s=>Vector3.DistanceSquared(s.Position,placard));
                if(trip.Shard==null)throw new InvalidOperationException("No verified shard for the plot's division.");
                RecordTravelPrompt("Housing route planned",new{trip.World,trip.Ward,trip.Plot,shard=trip.Shard.Id,shardPosition=FollowTravelPosition.From(trip.Shard.Position),plotPosition=FollowTravelPosition.From(placard)});
                HousingStage(trip,1,"Reaching the housing world");return;
            }
            if(trip.Stage==1){
                if(nativeWorldTrip!=null){nativeHousingStatus=nativeTravelStatus;return;}
                if(Player.CurrentWorld.RowId!=trip.World){
                    if(trip.WorldSent)throw new InvalidOperationException("World travel ended before reaching the housing world. "+nativeTravelStatus);
                    if(!StartNativeWorldTravel(trip.World,"housing"))throw new InvalidOperationException(nativeTravelStatus);trip.WorldSent=true;return;
                }
                HousingStage(trip,HousingAtWard(trip)?7:2,"Reaching the requested residential ward");return;
            }
            if(Player.CurrentWorld.RowId!=trip.World)throw new InvalidOperationException("Housing world changed unexpectedly.");
            if(trip.Stage==2){
                if(HousingAtWard(trip)){HousingStage(trip,7,"Choosing a residential shard");return;}
                if(trip.WardOnly&&trip.SourceObject!=0){
                    var source=Objects.FirstOrDefault(o=>o.GameObjectId==trip.SourceObject);
                    if(source==null||!source.IsTargetable)throw new InvalidOperationException("The selected housing transport is no longer available.");
                    if(SellerAddon("HousingSelectBlock")!=null){HousingStage(trip,4,"Selecting ward "+trip.Ward);return;}
                    if(Targets.Target?.GameObjectId==trip.SourceObject&&SellerAddon("SelectString")!=null){HousingStage(trip,3,"Reading housing transport options");return;}
                    if(!NativeTravelMovementReady())return;
                    if(!HousingApproach(trip,source,now))return;
                    if(HousingInteract(source))HousingStage(trip,3,"Opening housing transport");return;
                }
                if(!NativeTravelMovementReady())return;
                var main=Objects.FirstOrDefault(x=>x.IsTargetable&&x.ObjectKind==ObjectKind.Aetheryte&&x.BaseId==trip.District.Gateway);
                if(main==null||Vector3.Distance(self.Position,main.Position)>100){
                    if(trip.TeleportSent)return;
                    if(!RequestNativePublicTeleport(trip.District.Gateway))throw new InvalidOperationException("This character cannot teleport to the district's city. Unlock its aetheryte first.");
                    trip.TeleportSent=true;return;
                }
                // A manually opened, recognized travel menu is already evidence
                // that the target interaction succeeded; don't keep approaching it.
                var openMenu=SellerAddon("SelectString");
                if(openMenu!=null){
                    var residentialText=DataManager.GetExcelSheet<NativeEventQuestText>(name:"transport/Aetheryte").GetRowOrDefault(2)?.Text??"";
                    var wardText=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(6349).Text.ToString().Trim();
                    var recognized=TransportChoices(openMenu).Any(text=>NativeEventPolicy.Prompt(residentialText,text)||NativeEventPolicy.Prompt(wardText,text));
                    if(recognized){HousingStage(trip,3,"Reading the open residential travel menu");return;}
                }
                if(!HousingApproach(trip,main,now))return;
                if(HousingInteract(main))HousingStage(trip,3,"Opening residential district travel");return;
            }
            if(trip.Stage==3){
                var block=SellerAddon("HousingSelectBlock");if(block!=null){HousingStage(trip,4,"Selecting ward "+trip.Ward);return;}
                var menu=SellerAddon("SelectString");
                if(menu==null){
                    if(!trip.MenuOpened&&now-trip.StageAt>TimeSpan.FromSeconds(3)){
                        if(trip.Attempt>=2)throw new InvalidOperationException("Residential travel menu did not open after closer approaches.");
                        trip.Attempt++;HousingStage(trip,2,"Moving closer to the district aetheryte");
                    }return;
                }
                trip.MenuOpened=true;
                var choices=TransportChoices(menu);var residential=DataManager.GetExcelSheet<NativeEventQuestText>(name:"transport/Aetheryte").GetRowOrDefault(2)?.Text??"";
                var go=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(6349).Text.ToString().Trim();
                var match=choices.Select((text,index)=>(text,index)).Where(x=>NativeEventPolicy.Prompt(residential,x.text)||NativeHousingPolicy.WardChoice(go,x.text)).ToArray();
                if(match.Length!=1)throw new InvalidOperationException("Residential district travel is unavailable in this menu. The district may not be unlocked.");
                if(!NearbyTransportChoiceEnabled(menu,match[0].index))throw new InvalidOperationException("Ward selection is currently unavailable from this transport.");
                usingSharedTravel=true;try{SelectTravelChoice(menu,match[0].index);}finally{usingSharedTravel=false;}return;
            }
            if(trip.Stage==4){
                var block=SellerAddon("HousingSelectBlock");if(block==null)return;
                if(!trip.WardSelected){NativeTravelCallback(block,1,trip.Ward-1);trip.WardSelected=true;return;}
                var button=block->GetComponentButtonById(34);
                if(button!=null&&NativeEventButton(block,button))HousingStage(trip,5,"Confirming the selected residential ward");return;
            }
            if(trip.Stage==5){
                if(HousingAtWard(trip)){HousingStage(trip,7,"Choosing a residential shard");return;}
                var yes=(AddonSelectYesno*)SellerAddon("SelectYesno");if(yes==null||yes->PromptText==null)return;
                var prompt=TravelMenuText(yes->PromptText->NodeText.StringPtr)??"";
                if(!NativeHousingPolicy.WardPrompt(prompt,trip.Ward,trip.District.Name)){nativeHousingStatus="Confirm the requested ward manually; this prompt was not verified.";return;}
                if(NativeEventButton((AtkUnitBase*)yes,yes->YesButton))HousingStage(trip,6,"Waiting for confirmed ward arrival");return;
            }
            if(trip.Stage==6){if(HousingAtWard(trip))HousingStage(trip,7,"Choosing a residential shard");return;}
            if(!HousingAtWard(trip))throw new InvalidOperationException("The current residential district or ward differs from the requested address.");
            var shard=trip.Shard!;var targetPlot=trip.ShardOnly!=0?shard.Position:trip.Geometry.Result.Placards[trip.Plot-1];
            if(trip.Stage==7){
                // Using the same shard cannot shorten this trip. Walk directly from
                // the current street/plot instead of approaching it and cancelling.
                var nearest=trip.Geometry.Result.Shards.MinBy(s=>Vector3.DistanceSquared(self.Position,s.Position));
                if(trip.ShardOnly!=0&&nearest?.Id==shard.Id&&Vector3.Distance(self.Position,shard.Position)<12){StopNativeHousing("Already at "+shard.Name+".");return;}
                if(trip.ShardOnly==0&&(nearest?.Id==shard.Id||nearest!=null&&AethernetTravelPolicy.PreferWalking(self.Position,targetPlot,nearest.Position,shard.Position)||Vector3.Distance(self.Position,shard.Position)<12)){HousingStage(trip,10,"Walking to plot "+trip.Plot);return;}
                var source=Objects.Where(x=>x.IsTargetable&&x.ObjectKind is ObjectKind.Aetheryte or ObjectKind.EventObj&&
                    trip.Geometry.Result.Shards.Any(s=>Vector3.DistanceSquared(s.Position,x.Position)<16)).MinBy(x=>Vector3.DistanceSquared(self.Position,x.Position));
                if(source==null){
                    var near=trip.Geometry.Result.Shards.MinBy(s=>Vector3.DistanceSquared(self.Position,s.Position))!;
                    var goal=NativeAetheryteApproach.InteractionEdge(self.Position,near.Position,4);
                    if(!TryNativeTravelGround(goal,self.Position,near.Position,out var floor))return;
                    IndependentWalk(floor,.5f,now,allowFlight:false,directDistance:8);nativeHousingStatus="Reaching the nearest residential shard · "+independentWalkStatus;return;
                }
                if(!HousingApproach(trip,source,now))return;
                if(HousingInteract(source)){trip.Source=self.Position;HousingStage(trip,8,"Selecting "+shard.Name);}return;
            }
            if(trip.Stage is 8 or 9&&Vector3.Distance(self.Position,shard.Position)<12&&Vector3.Distance(self.Position,trip.Source)>8){
                if(trip.ShardOnly!=0){StopNativeHousing("Arrived at "+shard.Name+".");return;}
                HousingStage(trip,10,"Walking to plot "+trip.Plot);return;
            }
            if(trip.Stage==8){
                var town=SellerAddon("TelepotTown");
                if(town==null){if(now-trip.StageAt>TimeSpan.FromSeconds(10))throw new InvalidOperationException("Residential aethernet menu did not open.");return;}
                var choices=AethernetChoices(town).Where(x=>x.Name.Trim()==shard.Name.Trim()).ToArray();
                if(choices.Length!=1){
                    if(now-trip.StageAt<TimeSpan.FromSeconds(10)){nativeHousingStatus="Waiting for residential shard destinations to load";return;}
                    throw new InvalidOperationException("The requested residential shard was not uniquely available after waiting for the menu to load.");
                }
                if(trip.ShardOnly!=0){
                    var agent=FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentTelepotTown.Instance();
                    var callback=choices[0].Callback;
                    if(agent==null||agent->Data==null||agent->Data->AetheryteCount>64||callback>=agent->Data->AetheryteCount)throw new InvalidOperationException("Residential shard availability is not ready.");
                    var entry=agent->Data->Entries[(int)callback];
                    if(entry.IsLocked||entry.IsUnusable)throw new InvalidOperationException("This residential shard is currently unavailable.");
                }
                NativeTravelCallback(town,11,(int)choices[0].Callback);trip.Selections++;
                if(trip.Selections>=2)HousingStage(trip,9,"Waiting for residential shard arrival");return;
            }
            if(trip.Stage==9){
                if(Vector3.Distance(self.Position,shard.Position)<12&&Vector3.Distance(self.Position,trip.Source)>8)HousingStage(trip,10,"Walking to plot "+trip.Plot);return;
            }
            if(trip.Stage==10){
                var manager=HousingManager.Instance();var address=manager==null?null:AddressOf(manager->GetCurrentHouseId());
                var matching=address!=null&&address.WorldId==trip.World&&address.Ward==trip.Ward&&address.Plot==trip.Plot;
                if(NativeHousingPolicy.GoSeeArrival(true,matching,self.Position,targetPlot,false,
                    Vector3.Distance(self.Position,targetPlot)<=4.5f&&independentCollision.InteractionVisible(self.Position,targetPlot))){
                    StopNativeHousing($"Reached the plot entrance at {trip.District.Name} W{trip.Ward} P{trip.Plot} · GO SEE complete.");return;
                }
                // Re-evaluate the approach once from the actual nearby street. A
                // point picked from the distant shard may be on the wrong side
                // of the entrance steps; a manual retry previously corrected it.
                if(!trip.NearApproachSelected&&Vector3.Distance(self.Position,targetPlot)<12){
                    trip.NearApproachSelected=true;
                    var nearby=EstateDoorApproach.Find(new GameWalkingCollision(cacheGround:true),self.Position,targetPlot,p=>independentCollision.InteractionVisible(p,targetPlot),4.5f);
                    if(nearby is {} revised){
                        trip.Approach=revised;
                        RecordTravelPrompt("Housing nearby approach selected",new{trip.Plot,position=FollowTravelPosition.From(self.Position),goal=FollowTravelPosition.From(revised)});
                    }
                }
                if(trip.Approach is not {} goal){
                    trip.Approach=EstateDoorApproach.Find(new GameWalkingCollision(cacheGround:true),self.Position,targetPlot,p=>independentCollision.InteractionVisible(p,targetPlot),4.5f);
                    if(trip.Approach is not {} point)throw new InvalidOperationException("No verified walkable approach to this plot's placard. Export the housing route report.");goal=point;
                }
                IndependentWalk(goal,.4f,now,allowFlight:false,directDistance:8);nativeHousingStatus="Plot "+trip.Plot+" · "+independentWalkStatus;
            }
        }catch(Exception ex){StopNativeHousing(ex.Message);}
        finally{RecordPerformanceElapsed("Travel: native housing step",started);}
    }
}
