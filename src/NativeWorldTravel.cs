using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed class NativeWorldTrip
    {
        public string Id=Guid.NewGuid().ToString("N");public string Owner="command";
        public required ulong Character;public required string Name;public required uint Home,Source,Destination,Leg;
        public required int Service;public required DateTimeOffset Started;
        public NativeTravelStage Stage;public DateTimeOffset Next,StageAt;
        public bool PartyLeaveSent,PartyLeaveConfirmed,LogoutCommandSent;public DateTimeOffset PartyLeaveAt;
        public bool ReturnHome,DcSelected,ContextSent,LoginSent,LoginConfirmed,GatewaySent,TransferCommitted;
        public uint Gateway,PreferredGateway,RequestedLoginWorld;public bool ServiceSent,LogoutConfirmed,IntroSent;public int ApproachAttempt;public Vector3? ApproachGoal;
    }
    private NativeWorldTrip? nativeWorldTrip;
    private string nativeTravelStatus="Choose a map, aetheryte or world.";
    private uint TravelDc(uint world)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(world)?.DataCenter.RowId??0;
    private string TravelDcName(uint world)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(world)?.DataCenter.Value.Name.ToString()??"";
    private bool CanRequestWorldTravel(uint destination,out string reason)
    {
        reason="";
        var worlds=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        var target=worlds.GetRowOrDefault(destination);
        var home=worlds.GetRowOrDefault(Player.HomeWorld.RowId);
        if(!Player.IsLoaded||home==null||target==null||!target.Value.IsPublic){reason="Wait for your character to load and choose a public world.";return false;}
        var homeRegion=home.Value.DataCenter.Value.Region.RowId;
        var targetRegion=target.Value.DataCenter.Value.Region.RowId;
        if(NativeTravelPolicy.RegionReachable(homeRegion,targetRegion))return true;
        reason=$"Travel unavailable: a character from {home.Value.Name} cannot visit {target.Value.Name} under the supported regional travel rules. No journey started.";
        RecordTravelPrompt("Travel request rejected",new{home=Player.HomeWorld.RowId,current=Player.CurrentWorld.RowId,destination,homeRegion,targetRegion,reason});
        return false;
    }
    private void SetNativeTravelStage(NativeWorldTrip trip,NativeTravelStage stage,string message)
    {
        trip.Stage=stage;trip.StageAt=DateTimeOffset.UtcNow;trip.Next=trip.StageAt.AddMilliseconds(500);nativeTravelStatus=message;
        RecordTravelPrompt("Travel step",new{stage=stage.ToString(),message,trip.Character,trip.Service,trip.Source,trip.Destination,trip.Leg});
    }
    private void StopNativeWorldTravel(string reason,bool arrived=false)
    {
        if(nativeWorldTrip is not {} trip)return;
        RecordTravelPrompt("Travel stopped",new{reason,trip.TransferCommitted});nativeWorldTrip=null;
        StopIndependentWalk("Travel: "+reason);nativeTravelStatus=reason+(trip.TransferCommitted&&!arrived?" An accepted game transfer may still finish.":"");
        FollowChatNotice("TRAVEL — "+nativeTravelStatus);
    }
    private unsafe bool StartNativeWorldTravel(uint destination,string owner="command")
    {
        if(nativeWorldTrip!=null||NativeGardenWalking||nativeAethernetTrip!=null||nativeHousingTrip!=null&&owner!="housing"){nativeTravelStatus="A trip is already running. Use /et stop first.";return false;}
        if(!Player.IsLoaded||Player.ContentId==0||TravelDc(destination)==0){nativeTravelStatus="Wait for your character to load and choose a public world.";return false;}
        if(!CanRequestWorldTravel(destination,out var rejection)){nativeTravelStatus=rejection;return false;}
        if(destination==Player.CurrentWorld.RowId){nativeTravelStatus="Already on "+WorldName(destination)+".";return true;}
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Casting]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||HelperInCutscene()||QuestConversationVisible()||VisibleFollowAddon("SelectYesno")){
            nativeTravelStatus="Finish combat, duty, casting, loading or the current conversation first.";return false;
        }
        if(LifestreamBusy()||estateTrip!=null||estateHomeReturn!=null||assignedLogin!=null||titleLoginProbe.Active||loginMenuReturn.Active||independentMoving||independentSearch!=null||owner=="command"&&(QuestToolsOtherOperation()||independentQuestId!=0||eventAccountActive||gardenTendRoute!=null||shoppingTravel!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null)){nativeTravelStatus="Stop the active routine or journey before starting world travel.";return false;}
        var source=Player.CurrentWorld.RowId;var home=Player.HomeWorld.RowId;var cross=!NativeTravelPolicy.SameDataCenter(TravelDc(source),TravelDc(destination));var slot=CurrentLoginServiceAccount();
        if(cross&&slot is <0 or >15){nativeTravelStatus="Identify this client's service account before DC travel (log in once with Companion).";return false;}
        var returning=NativeTravelPolicy.ReturnFirst(source,home,TravelDc(source),TravelDc(destination));
        var trip=new NativeWorldTrip{PreferredGateway=TravelCityPolicy.Preferred(config.TravelCity,config.ShoppingCity,owner=="shopping"),Owner=owner,Character=Player.ContentId,Name=Player.CharacterName,Home=home,Source=source,Destination=destination,Leg=returning?home:destination,Service=slot,Started=DateTimeOffset.UtcNow,ReturnHome=returning};
        nativeWorldTrip=trip;StopIndependentWalk("Starting native travel",resetFailure:true);
        RecordTravelPrompt("Travel requested",new{trip.Character,trip.Name,trip.Home,trip.Source,trip.Destination,trip.Service,trip.Owner,trip.PreferredGateway});
        if(cross)BeginNativeTravelLogout(trip);else SetNativeTravelStage(trip,NativeTravelStage.Gateway,"Reaching a World Visit aetheryte");
        return true;
    }
    private static void PrepareNativeFinalLeg(NativeWorldTrip trip)
    {
        trip.Source=trip.Leg;trip.Leg=trip.Destination;trip.ReturnHome=false;
        trip.DcSelected=trip.ContextSent=trip.LoginSent=trip.LoginConfirmed=trip.TransferCommitted=trip.IntroSent=trip.LogoutConfirmed=trip.ServiceSent=false;
        trip.RequestedLoginWorld=0;trip.PartyLeaveSent=trip.PartyLeaveConfirmed=trip.LogoutCommandSent=false;
    }
    private unsafe void BeginNativeTravelLogout(NativeWorldTrip trip)
    {
        var ui=UIModule.Instance();if(ui==null){StopNativeWorldTravel("Game menu unavailable.");return;}
        if(trip.Stage!=NativeTravelStage.Logout)SetNativeTravelStage(trip,NativeTravelStage.Logout,trip.ReturnHome?"Returning home before the next destination":"Logging out for data-center travel");
        if(!NativeTravelPartyReady(trip,DateTimeOffset.UtcNow,true))return;
        trip.LogoutCommandSent=true;
        using var command=new Utf8String();command.SetString("/logout");ui->ProcessChatBoxEntry(&command,0,false);
    }
    private unsafe void UpdateNativeWorldTravel(DateTimeOffset now)
    {
        if(nativeWorldTrip is not {} trip||now<trip.Next)return;
        trip.Next=now.AddMilliseconds(500);
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try{
            if(now-trip.Started>TimeSpan.FromMinutes(30))throw new InvalidOperationException("Travel exceeded 30 minutes. Check the game queue before retrying.");
            var loading=Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51];
            if(Player.IsLoaded&&Player.ContentId!=trip.Character)throw new InvalidOperationException("A different character loaded; no further travel was requested.");
            if(Player.IsLoaded&&NativeTravelPolicy.Arrival(trip.Character,Player.ContentId,trip.Leg,Player.CurrentWorld.RowId,loading)){
                if(trip.Leg==trip.Destination){var following=trip.Owner=="follow";StopNativeWorldTravel("Arrived at "+WorldName(trip.Destination)+".",arrived:true);if(following)ResumeAfterConfirmedTravel();return;}
                PrepareNativeFinalLeg(trip);
                if(TravelDc(trip.Source)==TravelDc(trip.Destination))SetNativeTravelStage(trip,NativeTravelStage.Gateway,"Home return complete; continuing to the requested world");
                else BeginNativeTravelLogout(trip);
                return;
            }
            if(trip.Stage is not (NativeTravelStage.WorldQueue or NativeTravelStage.TransferWait or NativeTravelStage.Login)&&now-trip.StageAt>TimeSpan.FromMinutes(3))throw new InvalidOperationException("Timed out during "+trip.Stage+". Export diagnostics to include the prompts and last action.");
            if(loading){StopIndependentWalk("Waiting for travel loading");return;}
            if(trip.Stage==NativeTravelStage.Logout&&!trip.LogoutCommandSent&&Player.IsLoaded){BeginNativeTravelLogout(trip);return;}
            if(trip.Stage<=NativeTravelStage.WorldQueue){if(Player.IsLoaded)UpdateNativeSameDc(trip,now);return;}
            UpdateNativeDc(trip,now);
        }catch(Exception ex){StopNativeWorldTravel(ex.Message);}
        finally{RecordPerformanceElapsed("Travel: native world step",started);}
    }
    private unsafe void UpdateNativeSameDc(NativeWorldTrip trip,DateTimeOffset now)
    {
        if(Conditions[ConditionFlag.InCombat])throw new InvalidOperationException("Combat interrupted world travel.");
        if(trip.Stage is NativeTravelStage.Gateway or NativeTravelStage.WorldList or NativeTravelStage.WorldConfirm)
            if(!NativeTravelPartyReady(trip,now,false))return;
        if(trip.Stage==NativeTravelStage.Gateway){
            if(!NativeTravelMovementReady())return;
            if(Conditions[ConditionFlag.Casting])return;
            var self=Objects.LocalPlayer;if(self==null)return;
            var crystal=Objects.Where(x=>x.ObjectKind==ObjectKind.Aetheryte&&x.IsTargetable&&x.BaseId is 2 or 8 or 9).MinBy(x=>Vector3.DistanceSquared(x.Position,self.Position));
            if(crystal==null||Vector3.Distance(crystal.Position,self.Position)>100){
                if(trip.GatewaySent)return;
                var telepo=Telepo.Instance();if(telepo==null)return;telepo->UpdateAetheryteList();
                var unlocked=new List<uint>();
                foreach(var t in telepo->TeleportList)if(t.AetheryteId is 2 or 8 or 9&&t.SubIndex==0)unlocked.Add(t.AetheryteId);
                var gateway=TravelCityPolicy.Select(unlocked,trip.PreferredGateway);
                if(gateway!=0){
                    if(!telepo->Teleport(gateway,0))throw new InvalidOperationException("The game refused the city teleport.");
                    trip.GatewaySent=true;trip.Gateway=gateway;nativeTravelStatus="Teleporting to the World Visit aetheryte";
                    RecordTravelPrompt("City teleport requested",new{aetheryteId=gateway,trip.PreferredGateway,trip.Owner,fallback=trip.PreferredGateway!=0&&trip.PreferredGateway!=gateway});return;
                }
                throw new InvalidOperationException("Unlock a main aetheryte in Gridania, Limsa Lominsa or Ul'dah first.");
            }
            if(!TargetWithinInteractionRange(crystal)){
                IndependentWalk(crystal.Position,InteractionLimit(crystal),now,allowFlight:false,interactionTarget:crystal,useInteractionRegion:true);
                nativeTravelStatus="Approaching aetheryte · "+independentWalkStatus;return;
            }
            StopIndependentWalk("At World Visit aetheryte");
            if(Targets.Target?.Address!=crystal.Address){Targets.Target=crystal;return;}
            RecordTravelPrompt("Aetheryte interaction",new{crystal=crystal.BaseId,distance=Vector3.Distance(self.Position,crystal.Position),trip.ApproachAttempt});
            var system=TargetSystem.Instance();if(system==null)return;
            SetNativeTravelStage(trip,NativeTravelStage.AetheryteMenu,"Opening World Visit");
            usingSharedTravel=true;try{system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)crystal.Address,false);}finally{usingSharedTravel=false;}return;
        }
        if(trip.Stage==NativeTravelStage.AetheryteMenu){
            var menu=(AddonSelectString*)SellerAddon("SelectString");
            if(menu==null){
                if(now-trip.StageAt<TimeSpan.FromSeconds(trip.ApproachAttempt==0?2:5))return;
                if(trip.ApproachAttempt>=2)throw new InvalidOperationException("The aetheryte menu did not open after the closer approach.");
                trip.ApproachAttempt++;trip.ApproachGoal=null;SetNativeTravelStage(trip,NativeTravelStage.Gateway,"Aetheryte did not respond; moving a little closer");return;
            }
            var label=DataManager.GetExcelSheet<NativeEventQuestText>(name:"transport/Aetheryte").GetRowOrDefault(3)?.Text??"";
            var choices=TransportChoices((AtkUnitBase*)menu);var index=choices.FindIndex(x=>NativeEventPolicy.Prompt(label,x));
            if(index<0)throw new InvalidOperationException("World Visit is not available in this aetheryte menu.");
            var list=menu->PopupMenu.PopupMenu.List;if(list==null||list->GetItemDisabledState(index))throw new InvalidOperationException("World Visit is currently disabled.");
            SetNativeTravelStage(trip,NativeTravelStage.WorldList,"Selecting "+WorldName(trip.Leg));list->DispatchItemEvent(index,AtkEventType.ListItemClick);return;
        }
        if(trip.Stage==NativeTravelStage.WorldList){
            var menu=SellerAddon("WorldTravelSelect");if(menu==null)return;
            var module=RaptureAtkModule.Instance();var arrayIndex=(int)StringArrayType.WorldTranslate;
            if(module==null||module->AtkArrayDataHolder.StringArrayCount<=arrayIndex||module->AtkArrayDataHolder.StringArrays==null)return;
            var names=module->AtkArrayDataHolder.StringArrays[arrayIndex];if(names==null||names->StringArray==null)return;
            for(var i=3;i<=10&&i<names->Size;i++)if(TravelMenuText(names->StringArray[i].Value)==WorldName(trip.Leg)){
                SetNativeTravelStage(trip,NativeTravelStage.WorldConfirm,"Confirming destination "+WorldName(trip.Leg));NativeTravelCallback(menu,0,i-1);return;
            }
            nativeTravelStatus="Waiting for "+WorldName(trip.Leg)+" in the available World Visit list";return;
        }
        if(trip.Stage==NativeTravelStage.WorldConfirm){
            var dialog=(AddonSelectYesno*)SellerAddon("SelectYesno");if(dialog==null||dialog->PromptText==null)return;
            var prompt=TravelMenuText(dialog->PromptText->NodeText.StringPtr)??"";
            if(!NativeTravelPolicy.WorldPrompt(prompt,WorldName(trip.Leg)??"")){nativeTravelStatus="Confirm the destination manually; this localized prompt is not yet recognized";return;}
            if(NativeEventButton((AtkUnitBase*)dialog,dialog->YesButton)){trip.TransferCommitted=true;SetNativeTravelStage(trip,NativeTravelStage.WorldQueue,"Waiting for World Visit queue and arrival");}return;
        }
    }
    private unsafe bool NativeTravelPartyReady(NativeWorldTrip trip,DateTimeOffset now,bool changingDc)
    {
        var cross=Conditions[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance];
        var blocked=NativeTravelPolicy.PartyBlocksTravel(FollowParty.Length,FollowParty.PartyId!=0,cross,changingDc);
        if(!blocked)return true;
        StopIndependentWalk("Leaving party for world travel");
        if(Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.InCombat])throw new InvalidOperationException("Cannot leave the party for travel during combat or a duty.");
        var dialog=(AddonSelectYesno*)SellerAddon("SelectYesno");
        var prompt=dialog==null||dialog->PromptText==null?"":TravelMenuText(dialog->PromptText->NodeText.StringPtr)??"";
        if(!trip.PartyLeaveSent){
            if(dialog!=null){
                // Only dismiss this trip's destination prompt, never an unrelated dialog.
                if(trip.Stage==NativeTravelStage.WorldConfirm&&NativeTravelPolicy.WorldPrompt(prompt,WorldName(trip.Leg)??"")){
                    if(NativeEventButton((AtkUnitBase*)dialog,dialog->NoButton))SetNativeTravelStage(trip,NativeTravelStage.WorldList,"Leaving party before World Visit");
                    return false;
                }
                nativeTravelStatus="Close the current confirmation so Companion can leave the party";return false;
            }
            var ui=UIModule.Instance();if(ui==null)return false;
            trip.PartyLeaveSent=true;trip.PartyLeaveAt=now;
            RecordTravelPrompt("Travel party leave requested",new{changingDc,cross,members=FollowParty.Length,trip.Leg});
            using var command=new Utf8String();command.SetString("/leave");ui->ProcessChatBoxEntry(&command,0,false);
        }else{
            if(now-trip.PartyLeaveAt>TimeSpan.FromSeconds(15))throw new InvalidOperationException("Party did not clear after the leave request; travel was not submitted.");
            if(!trip.PartyLeaveConfirmed&&dialog!=null&&NativeTravelPolicy.LeavePartyPrompt(prompt)){
                if(NativeEventButton((AtkUnitBase*)dialog,dialog->YesButton))trip.PartyLeaveConfirmed=true;
            }
        }
        nativeTravelStatus="Leaving party · waiting for party membership to clear";return false;
    }
    private unsafe void NativeTravelCallback(AtkUnitBase* addon,params int[] args)
    {
        RecordTravelPrompt("Companion action",new{addon=addon->NameString,args});usingSharedTravel=true;try{LoginCallback(addon,args);}finally{usingSharedTravel=false;}
    }
}
