using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed class EstateTrip
    {
        public required Address Destination;
        public required ulong Character;
        public required int Mode;
        public required string Label;
        public required DateTimeOffset Started;
        public int Stage,EntrySteps,DoorRevealSteps,DoorAttempt;
        public Vector3? EntryDirection;public OutdoorPlotEntry? Entry;
        public DateTimeOffset ActionAt,DoorMissingSince;
        public Vector3? Forward,DoorApproach;
        public ulong Door;
        public readonly GardenArrivalGate Arrival=new();
    }
    private EstateTrip? estateTrip;
    private void StopEstateTravel(string reason)
    {
        if(estateHomeReturn!=null){if(estateHomeReturn.NativeWorldRoute&&nativeWorldTrip?.Owner=="estate")StopNativeWorldTravel(reason);estateHomeReturn=null;FollowChatNotice("TRAVEL — Home return stopped. Any transfer already accepted by the game may finish.");}
        if(estateTrip==null)return;
        estateTrip=null;StopIndependentWalk("Estate travel: "+reason);
        FollowChatNotice("TRAVEL — "+reason);
    }
    private unsafe Address? ReadEstateInterior(HousingManager* manager)
    {
        if(manager==null)return null;
        if(manager->GetCurrentHousingTerritoryType()==HousingTerritoryType.Workshop){
            if(manager->WorkshopTerritory==null)return null;
            var workshop=AddressOf(manager->WorkshopTerritory->HouseId);
            return workshop is null?null:workshop with { Workshop=true };
        }
        return manager->GetCurrentHousingTerritoryType()==HousingTerritoryType.Indoor
            ?AddressOf(manager->GetCurrentIndoorHouseId()):null;
    }
    private static bool SameEstate(Address? a,Address b)=>a!=null&&a.WorldId==b.WorldId&&a.TerritoryTypeId==b.TerritoryTypeId&&a.Ward==b.Ward&&a.Plot==b.Plot;

    private unsafe void StartEstateTravelCommand((int Property,int Mode,string Label) destination)
    {
        if(NativeGardenWalking||nativeWorldTrip!=null||nativeHousingTrip!=null||estateTrip!=null||estateHomeReturn!=null){FollowChatNotice("TRAVEL — Estate travel is running. Use /et stop first.");return;}
        if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){
            FollowChatNotice("TRAVEL — Wait until your character has loaded.");return;
        }
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Casting]||HelperInCutscene()||QuestConversationVisible()||VisibleFollowAddon("SelectYesno")){
            FollowChatNotice("TRAVEL — Finish the current combat, duty, cast or conversation first.");return;
        }
        if(followSession.Armed||independentMoving||independentSearch!=null||independentQuestId!=0||assignedLogin!=null||eventAccountActive||gardenTendRoute!=null||shoppingTravel!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null){
            FollowChatNotice("TRAVEL — Stop the active follower, routine or journey first.");return;
        }
        try{
            var shared=destination.Property>=3;
            var sharedSlot=EstateTravelCommand.SharedSlot(destination.Property);
            if(shared&&sharedSlot<0)throw new InvalidOperationException("Shared estate "+(destination.Property-2)+" is not available for this character. No tenant estate can be selected in this slot. No travel started.");
            if(Player.CurrentWorld.RowId!=Player.HomeWorld.RowId){BeginEstateHomeReturn(destination);return;}
            var owned=shared?HousingManager.GetOwnedHouseId(EstateType.SharedEstate,sharedSlot):HousingManager.GetOwnedHouseId(destination.Property==1?EstateType.PersonalEstate:EstateType.FreeCompanyEstate);
            var address=AddressOf(owned);
            if(address==null||address.WorldId!=Player.HomeWorld.RowId||address.Apartment)throw new InvalidOperationException(shared
                ?"Shared estate "+(sharedSlot+1)+" is not available for this character. Check that you're a tenant there. No travel started."
                :"This character has no verified "+(destination.Property==1?"private":"FC")+" plot.");
            var manager=HousingManager.Instance();var inside=ReadEstateInterior(manager);
            var trip=new EstateTrip{Destination=address,Character=Player.ContentId,Mode=destination.Mode,Label=destination.Label,Started=DateTimeOffset.UtcNow};
            if(SameEstate(inside,address)&&destination.Mode!=0){
                if(EstateTravelCommand.InteriorArrived(destination.Mode,true,inside!.Workshop,inside.Room)){FollowChatNotice("TRAVEL — Already "+destination.Label+".");return;}
                if(destination.Mode==3&&!inside!.Workshop&&inside.Room==0){trip.Stage=5;trip.ActionAt=trip.Started;estateTrip=trip;FollowChatNotice("TRAVEL — Walking to the FC workshop entrance.");return;}
            }
            if(destination.Mode==3&&manager!=null&&manager->GetCurrentHousingTerritoryType()==HousingTerritoryType.Workshop&&inside==null){
                FollowChatNotice("TRAVEL — Workshop details are still loading. No teleport was started.");return;
            }
            var outside=manager==null?null:AddressOf(manager->GetCurrentHouseId());
            if(manager!=null&&manager->GetCurrentHousingTerritoryType()==HousingTerritoryType.Outdoor&&SameEstate(outside,address)){
                trip.Stage=2;estateTrip=trip;FollowChatNotice("TRAVEL — Going to "+destination.Label+".");return;
            }
            var telepo=Telepo.Instance();if(telepo==null||telepo->ActiveTeleportRequest)throw new InvalidOperationException("Teleport is not ready.");
            telepo->UpdateAetheryteList();
            foreach(var entry in telepo->TeleportList){
                if(entry.HouseId.Id!=owned.Id||entry.AetheryteId==0)continue;
                if(!telepo->Teleport(entry.AetheryteId,entry.SubIndex))throw new InvalidOperationException("The game refused the estate teleport.");
                trip.ActionAt=trip.Started;estateTrip=trip;FollowChatNotice("TRAVEL — Teleporting to "+destination.Label+". /et stop cancels the remaining route.");return;
            }
            throw new InvalidOperationException(shared
                ?"Shared estate "+(sharedSlot+1)+" teleport is not available. Check your tenant access and the estate teleport settings."
                :"The exact estate teleport is not available on this character.");
        }catch(Exception ex){FollowChatNotice("TRAVEL — "+ex.Message);}
    }

    private unsafe void UpdateEstateTravel(DateTimeOffset now)
    {
        if(estateHomeReturn!=null){UpdateEstateHomeReturn(now);return;}
        if(estateTrip is not {} trip)return;
        try{
            if(followSession.Armed||independentQuestId!=0||eventAccountActive||gardenTendRoute!=null||assignedLogin!=null)throw new InvalidOperationException("Another routine started; estate travel stopped.");
            if(now-trip.Started>TimeSpan.FromMinutes(5))throw new InvalidOperationException("Estate travel timed out.");
            if(!Player.IsLoaded)return;
            if(Player.ContentId!=trip.Character||Player.CurrentWorld.RowId!=trip.Destination.WorldId)throw new InvalidOperationException("Character or world changed; estate travel stopped.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){trip.Arrival.Reset();StopIndependentWalk("Loading estate");return;}
            if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty])throw new InvalidOperationException("Estate travel stopped for combat or duty.");
            if(Conditions[ConditionFlag.Casting]||HelperInCutscene())return;
            if(Objects.LocalPlayer is not {} self)return;
            var manager=HousingManager.Instance();if(manager==null)return;
            var inside=ReadEstateInterior(manager);
            var outside=AddressOf(manager->GetCurrentHouseId());
            var indoors=manager->GetCurrentHousingTerritoryType()!=HousingTerritoryType.Outdoor&&SameEstate(inside,trip.Destination);
            if(EstateTravelCommand.InteriorArrived(trip.Mode,indoors,inside?.Workshop??false,inside?.Room??-1)&&trip.Mode==3){StopEstateTravel("Arrived inside the FC workshop.");return;}
            if(indoors&&!inside!.Workshop&&inside.Room==0){
                if(trip.Mode==2){StopEstateTravel("Arrived "+trip.Label+".");return;}
                if(trip.Mode==3&&trip.Stage<5){trip.Stage=5;trip.Door=0;trip.DoorAttempt=0;trip.DoorApproach=null;trip.ActionAt=now;trip.DoorMissingSince=default;trip.Arrival.Reset();StopIndependentWalk("Loading FC interior");}
            }
            // A submitted workshop entry changes territory before its housing record is ready.
            if(trip.Stage==7){
                if(now-trip.ActionAt>TimeSpan.FromSeconds(30))throw new InvalidOperationException("The game did not confirm entry into the FC workshop.");
                return;
            }
            if(trip.Stage>=5){
                if(!indoors||inside!.Workshop||inside.Room!=0)throw new InvalidOperationException("The expected FC house interior is not confirmed.");
            }else if(trip.Stage<4){
                if(Client.TerritoryType!=trip.Destination.TerritoryTypeId||manager->GetCurrentWard()!=trip.Destination.Ward-1)return;
                if(manager->GetCurrentHousingTerritoryType()!=HousingTerritoryType.Outdoor)return;
            }
            if(trip.Stage==0){
                if(now-trip.ActionAt<TimeSpan.FromSeconds(3)||!trip.Arrival.Ready(false,self.Position,now))return;
                if(SameEstate(outside,trip.Destination)){trip.Stage=2;}
                else{
                    if(outside!=null)throw new InvalidOperationException("Teleport arrived on a different plot.");
                    // Keep the settled inward heading across successive checked segments.
                    trip.EntryDirection=new Vector3(MathF.Sin(self.Rotation),0,MathF.Cos(self.Rotation));
                    trip.Forward=null;trip.Stage=1;
                }
            }
            if(trip.Stage==1){
                if(outside!=null&&!SameEstate(outside,trip.Destination))
                    throw new InvalidOperationException("Forward approach reached a different plot; stopped.");
                if(SameEstate(outside,trip.Destination)){
                    StopIndependentWalk("Requested plot confirmed");trip.Forward=null;trip.Stage=2;
                }else{
                    trip.Entry??=new(trip.EntryDirection!.Value);
                    WalkOutdoorPlotEntry(trip.Entry,now);trip.EntrySteps=trip.Entry.Steps;
                    return;
                }
            }
            if(trip.Stage==2&&trip.Mode==0){StopEstateTravel("Arrived at the "+trip.Label+".");return;}
            if(trip.Stage==4||trip.Stage==7){
                if(now-trip.ActionAt>TimeSpan.FromSeconds(30))throw new InvalidOperationException("The game did not confirm entry into "+trip.Label+".");
                return;
            }
            if(trip.Stage is 3 or 6){
                if(now-trip.ActionAt>TimeSpan.FromSeconds(20))throw new InvalidOperationException("The expected entrance prompt was not available.");
                if(trip.DoorAttempt==0&&now-trip.ActionAt>TimeSpan.FromSeconds(3)&&
                    !VisibleFollowAddon("SelectYesno")&&!VisibleFollowAddon("SelectString")&&!QuestConversationVisible()){
                    trip.DoorAttempt=1;trip.DoorApproach=null;trip.Stage=trip.Stage==3?2:5;
                    RecordTravelPrompt("Estate entrance closer retry",new{trip.Door,trip.DoorAttempt});return;
                }
                if(Targets.Target?.GameObjectId!=trip.Door)return;
                if(trip.Stage==3){
                    var prompt=(AddonSelectYesno*)KeyboundAddon("SelectYesno");
                    var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Warp>().GetRowOrDefault(131148)?.Question.ToString()??"";
                    if(prompt!=null&&prompt->PromptText!=null&&NativeEventPolicy.Prompt(expected,TravelMenuText(prompt->PromptText->NodeText.StringPtr)??"")&&NativeEventButton((AtkUnitBase*)prompt,prompt->YesButton)){
                        RecordTravelPrompt("Estate entrance menu confirmed",new{door=trip.Door});
                        trip.Stage=4;trip.ActionAt=now;
                    }
                }else{
                    var addon=KeyboundAddon("SelectString");if(addon==null||!addon->IsVisible||!addon->IsReady)return;
                    var expected=DataManager.GetExcelSheet<NativeEventQuestText>(name:"custom/001/CmnDefHousingPersonalRoomEntrance_00178").GetRowOrDefault(11)?.Text??"";
                    if(string.IsNullOrWhiteSpace(expected))throw new InvalidOperationException("Workshop menu text could not be verified.");
                    var matches=HelperChoices(addon,"SelectString").Select((text,index)=>(text,index)).Where(x=>NativeEventPolicy.Prompt(expected,x.text)).ToArray();
                    if(matches.Length==1){SelectTravelChoice(addon,matches[0].index);trip.Stage=7;trip.ActionAt=now;}
                }
                return;
            }
            if(QuestConversationVisible()||VisibleFollowAddon("SelectYesno"))return;
            if(Conditions[ConditionFlag.Mounted])throw new InvalidOperationException("Dismount and run the estate command again.");
            var workshop=trip.Stage==5;
            // Housing identity can arrive before its targetable entrances.
            if(workshop&&now-trip.ActionAt<TimeSpan.FromSeconds(3))return;
            var ids=workshop?new uint[]{2004353}:new uint[]{2002737,2002739};
            var names=ids.Select(id=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.EObjName>().GetRowOrDefault(id)?.Singular.ToString()??"").Select(EstateTravelCommand.EntranceName).Where(n=>n.Length>0).ToArray();
            var doors=Objects.Where(o=>o.ObjectKind==ObjectKind.EventObj&&o.IsTargetable&&(ids.Contains(o.BaseId)||names.Contains(EstateTravelCommand.EntranceName(o.Name.ToString()),StringComparer.OrdinalIgnoreCase)))
                .Where(o=>Vector3.Distance(o.Position,self.Position)<40).OrderBy(o=>Vector3.DistanceSquared(o.Position,self.Position)).ToArray();
            var door=trip.Door==0?doors.FirstOrDefault():doors.FirstOrDefault(o=>o.GameObjectId==trip.Door);
            if(door==null){
                if(!workshop&&SameEstate(outside,trip.Destination)&&trip.Door==0){
                    // Some entrances only become targetable farther inside the yard.
                    // Scan again every frame and hand off immediately when the door appears.
                    if(trip.Forward!=null||EstateTravelCommand.CanExtendEntry(true,trip.EntrySteps,trip.DoorRevealSteps)){
                        if(trip.Forward==null){
                            trip.EntryDirection??=new Vector3(MathF.Sin(self.Rotation),0,MathF.Cos(self.Rotation));
                            trip.DoorRevealSteps++;trip.Forward=self.Position+trip.EntryDirection.Value*3;
                        }
                        if(!IndependentWalk(trip.Forward.Value,.3f,now,allowFlight:false,directOnly:true))return;
                        StopIndependentWalk("Extra approach complete · checking house entrance");
                        trip.Forward=null;trip.DoorMissingSince=now;return;
                    }
                }
                if(!workshop&&trip.Forward!=null&&!SameEstate(outside,trip.Destination))
                    throw new InvalidOperationException("The forward approach left the requested plot; stopped.");
                if(trip.DoorMissingSince==default)trip.DoorMissingSince=now;
                if(EstateTravelCommand.WaitForEntrance(now-trip.DoorMissingSince)){
                    independentWalkStatus="Waiting for the "+(workshop?"additional chambers":"house")+" entrance to load";return;
                }
                throw new InvalidOperationException("No "+(workshop?"additional chambers / workshop":"house")+" entrance became available nearby after waiting for loading.");
            }
            trip.DoorMissingSince=default;
            if(trip.Forward!=null){StopIndependentWalk("House entrance available");trip.Forward=null;}
            if(trip.Door==0&&doors.Length>1&&Vector3.Distance(doors[1].Position,self.Position)-Vector3.Distance(door.Position,self.Position)<3)
                throw new InvalidOperationException("Multiple entrances are equally close. Move nearer the correct door and retry.");
            trip.Door=door.GameObjectId;
            if(!TargetWithinInteractionRange(door)){
            if(!GameWalkingCollision.Available())return;
            trip.DoorApproach??=EstateDoorApproach.Find(independentCollision,self.Position,door.Position,
                point=>independentCollision.InteractionVisible(point,door.Position),InteractionLimit(door));
            if(trip.DoorApproach is not {} approach)throw new InvalidOperationException("No clear standing point within range of the estate entrance. Move nearer the door and retry.");
            if(!IndependentWalk(approach,.35f,now,allowFlight:false,interactionTarget:door))return;
            }
            StopIndependentWalk("At estate door");
            if(!InteractionApproachPolicy.WithinDistance(self.Position,door.Position,InteractionLimit(door)))throw new InvalidOperationException("Door approach finished outside interaction distance; no entrance interaction sent.");
            var targetSystem=TargetSystem.Instance();if(targetSystem==null)return;
            Targets.Target=door;
            RecordTravelPrompt("Estate entrance interaction requested",new{door=door.GameObjectId,dataId=door.BaseId,distance=Vector3.Distance(self.Position,door.Position),limit=InteractionLimit(door),trip.DoorAttempt,player=FollowTravelPosition.From(self.Position),target=FollowTravelPosition.From(door.Position)});
            targetSystem->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)door.Address,false);
            trip.Stage=workshop?6:3;trip.ActionAt=now;
        }catch(Exception ex){StopEstateTravel(ex.Message);}
    }
}
