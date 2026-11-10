using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed class EstateHomeReturn
    {
        public required ulong Character;
        public required ushort HomeWorld;
        public required ushort VisitingWorld;
        public required string Name;
        public required int ServiceSlot;
        public required (int Property,int Mode,string Label) Destination;
        public required DateTimeOffset Started;
        public DateTimeOffset Next;
        public bool DcSelected,ContextSent,ReturnRequested,LoginSent,LoginConfirmed,LogoutConfirmed,NativeWorldRoute;
        public ushort WorldRequested;
    }
    private EstateHomeReturn? estateHomeReturn;
    private unsafe void BeginEstateHomeReturn((int Property,int Mode,string Label) destination)
    {
        if(UsesWorldVisit(Player.CurrentWorld.RowId,Player.HomeWorld.RowId)){
            var sameDc=new EstateHomeReturn{Character=Player.ContentId,HomeWorld=(ushort)Player.HomeWorld.RowId,VisitingWorld=(ushort)Player.CurrentWorld.RowId,
                Name=Player.CharacterName,ServiceSlot=CurrentLoginServiceAccount(),Destination=destination,Started=DateTimeOffset.UtcNow,NativeWorldRoute=true};
            // Start before assigning the continuation: the native engine rejects
            // an already active estate return as a competing journey.
            if(!StartNativeWorldTravel(sameDc.HomeWorld,"estate"))throw new InvalidOperationException(nativeTravelStatus);
            estateHomeReturn=sameDc;
            FollowChatNotice("TRAVEL — Returning home through World Visit, then continuing to "+destination.Label+". /et stop cancels the remaining steps.");
            return;
        }
        var slot=CurrentLoginServiceAccount();
        if(slot is <0 or >15)throw new InvalidOperationException("This client's service-account slot is unknown; no logout was requested.");
        var ui=UIModule.Instance();if(ui==null)throw new InvalidOperationException("Game travel menu unavailable.");
        var run=new EstateHomeReturn{Character=Player.ContentId,HomeWorld=(ushort)Player.HomeWorld.RowId,VisitingWorld=(ushort)Player.CurrentWorld.RowId,
            Name=Player.CharacterName,ServiceSlot=slot,Destination=destination,Started=DateTimeOffset.UtcNow};
        estateHomeReturn=run;
        using var command=new Utf8String();command.SetString("/logout");ui->ProcessChatBoxEntry(&command,0,false);
        FollowChatNotice("TRAVEL — Returning this character to its home world through character selection, then continuing to "+destination.Label+". /et stop cancels the remaining steps.");
    }
    private unsafe void UpdateEstateHomeReturn(DateTimeOffset now)
    {
        if(estateHomeReturn is not {} run||now<run.Next)return;
        run.Next=now.AddMilliseconds(500);
        try{
            if(now-run.Started>TimeSpan.FromMinutes(15))throw new InvalidOperationException("Home-world return timed out; no alternate account or character was selected.");
            if(run.NativeWorldRoute){
                if(Player.IsLoaded&&Player.ContentId!=run.Character)throw new InvalidOperationException("A different character loaded. Estate travel cancelled.");
                if(nativeWorldTrip!=null)return;
                if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
                if(!EstateTravelCommand.CanResume(run.Character,Player.ContentId,run.HomeWorld,Player.CurrentWorld.RowId))throw new InvalidOperationException("Home-world travel stopped before arrival. "+nativeTravelStatus);
                estateHomeReturn=null;StartEstateTravelCommand(run.Destination);return;
            }
            if(Player.IsLoaded){
                if(Player.ContentId!=run.Character)throw new InvalidOperationException("A different character loaded. Estate travel cancelled.");
                if(EstateTravelCommand.CanResume(run.Character,Player.ContentId,run.HomeWorld,Player.CurrentWorld.RowId)){
                    if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
                    estateHomeReturn=null;StartEstateTravelCommand(run.Destination);return;
                }
                if(run.LogoutConfirmed||Conditions[ConditionFlag.LoggingOut])return;
                var logout=(AddonSelectYesno*)KeyboundAddon("SelectYesno");
                var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(115)?.Text.ToString()??"";
                if(logout!=null&&logout->PromptText!=null&&NativeEventPolicy.Prompt(expected,TravelMenuText(logout->PromptText->NodeText.StringPtr)??"")&&NativeEventButton((AtkUnitBase*)logout,logout->YesButton))run.LogoutConfirmed=true;
                return;
            }
            // Once login is accepted, queue/loading dialogs belong to the game.
            // In particular, the queue's single button must never be treated as OK.
            if(run.LoginConfirmed||Client.IsLoggedIn)return;
            if(SellerAddon("SelectString")!=null){SelectVerifiedServiceAccount(run.ServiceSlot);return;}
            var dcWorld=WorldName(run.ReturnRequested?run.HomeWorld:run.VisitingWorld)??"";
            if(SelectLoginDataCenter(dcWorld,ref run.DcSelected))return;
            if(SellerAddon("_TitleMenu")!=null){run.DcSelected=false;ClickLoginMenuButton("_TitleMenu",5);return;}
            var lobby=AgentLobby.Instance();if(lobby==null)return;
            if(lobby->ServiceAccountIndex>=0&&lobby->ServiceAccountIndex!=run.ServiceSlot)
                throw new InvalidOperationException("The game selected a different service account. No login was requested.");
            if(SellerAddon("SelectYesno")!=null){
                if(ConfirmNativeLogin(run.LoginSent,run.Character,WorldName(run.HomeWorld)??"",run.ServiceSlot,run.Name,run.HomeWorld))run.LoginConfirmed=true;
                return;
            }
            if(lobby->TemporaryLocked)return;
            // These dedicated game dialogs are handled only after our exact character's return action.
            if(run.ReturnRequested){
                var check=SellerAddon("LobbyWKTCheckHome");
                if(check!=null){NativeEventButton(check,check->GetComponentButtonById(3));return;}
                var list=SellerAddon("LobbyDKTWorldList");
                if(list!=null){if(list->GetComponentButtonById(25)!=null&&list->GetComponentButtonById(25)->IsEnabled)LoginCallback(list,4);return;}
                var execute=SellerAddon("LobbyDKTCheckExec");
                if(execute!=null){if(execute->GetComponentButtonById(3)!=null&&execute->GetComponentButtonById(3)->IsEnabled)LoginCallback(execute,0);return;}
                var ok=(AddonSelectOk*)SellerAddon("SelectOk");
                if(ok!=null){NativeEventButton((AtkUnitBase*)ok,ok->OkButton);return;}
            }
            var menu=SellerAddon("_CharaSelectListMenu");if(menu==null)return;
            var entries=lobby->LobbyData.CharaSelectEntries;
            if(entries.LongCount>512)return;
            int index=-1,current=0;ushort atWorld=0;int matches=0;
            foreach(var item in entries){
                var entry=item.Value;
                if(entry!=null&&entry->ContentId==run.Character&&entry->HomeWorldId==run.HomeWorld&&entry->NameString==run.Name){
                    index=current;atWorld=entry->CurrentWorldId;matches++;
                }
                current++;
            }
            if(matches!=1){
                var wanted=run.ReturnRequested?run.HomeWorld:run.VisitingWorld;
                if(run.WorldRequested==wanted)return;
                if(SelectEstateLoginWorld(wanted)){run.WorldRequested=wanted;run.Next=now.AddSeconds(2);}
                return;
            }
            if(run.ReturnRequested&&atWorld==run.HomeWorld){
                if(!run.LoginSent){ClickNativeLoginCharacter(menu,index);run.LoginSent=true;}
                return;
            }
            if(run.ReturnRequested)return;
            var context=SellerAddon("ContextMenu");
            if(context!=null){
                if(!run.ContextSent||lobby->HoveredCharacterContentId!=run.Character)return;
                var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Lobby>().GetRowOrDefault(1117)?.Text.ToString()??"";
                if(context->AtkValues==null||context->AtkValuesCount<9)return;
                var count=(int)context->AtkValues[0].UInt;
                if(count is <1 or >32||context->AtkValuesCount<8+count)return;
                var component=context->GetComponentListById(2);if(component==null)return;
                for(var i=0;i<count;i++){
                    var value=context->AtkValues[8+i];
                    if(((int)value.Type&15) is not (8 or 10)||component->GetItemDisabledState(i))continue;
                    if(!NativeEventPolicy.Prompt(expected,TravelMenuText(value.String.Value)??""))continue;
                    LoginCallback(context,0,i,0);run.ReturnRequested=true;run.DcSelected=false;run.WorldRequested=0;return;
                }
                throw new InvalidOperationException("Return to Home World is not available for the selected character.");
            }
            if(run.ContextSent)return;
            if(index>250)throw new InvalidOperationException("Character list index is out of bounds.");
            var eventIndex=(byte)(5+index);
            var evt=new AtkEvent{Listener=(AtkEventListener*)menu,Target=&AtkStage.Instance()->AtkEventTarget,State=new(){StateFlags=(AtkEventStateFlags)eventIndex}};
            var data=new AtkEventData();((byte*)&data)[6]=1;
            menu->ReceiveEvent(AtkEventType.MouseClick,eventIndex,&evt,&data);
            run.ContextSent=true;
        }catch(Exception ex){if(run.NativeWorldRoute&&nativeWorldTrip?.Owner=="estate")StopNativeWorldTravel(ex.Message);estateHomeReturn=null;FollowChatNotice("TRAVEL — "+ex.Message);}
    }
    private unsafe bool SelectEstateLoginWorld(ushort world)
    {
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("_CharaSelectWorldServer").Address;
        var module=RaptureAtkModule.Instance();
        if(addon==null||!addon->IsReady||module==null||module->AtkArrayDataHolder.StringArrayCount<=1||module->AtkArrayDataHolder.StringArrays==null)return false;
        var strings=module->AtkArrayDataHolder.StringArrays[1];
        if(strings==null||strings->StringArray==null)return false;
        var expected=WorldName(world);if(string.IsNullOrWhiteSpace(expected))return false;
        for(var i=0;i<Math.Min(strings->Size,16);i++){
            var p=strings->StringArray[i].Value;if(p==null)break;
            if(TravelMenuText(p)==expected){LoginCallback(addon,25,0,i);return true;}
        }
        return false;
    }
}
