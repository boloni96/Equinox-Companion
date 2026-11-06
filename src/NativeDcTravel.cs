using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe void UpdateNativeDc(NativeWorldTrip trip,DateTimeOffset now)
    {
        if(trip.Stage==NativeTravelStage.Logout){
            if(Player.IsLoaded){
                var yes=(AddonSelectYesno*)SellerAddon("SelectYesno");
                var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(115)?.Text.ToString()??"";
                if(!trip.LogoutConfirmed&&yes!=null&&yes->PromptText!=null&&NativeEventPolicy.Prompt(expected,TravelMenuText(yes->PromptText->NodeText.StringPtr)??""))trip.LogoutConfirmed=NativeEventButton((AtkUnitBase*)yes,yes->YesButton);
                return;
            }
            if(Client.IsLoggedIn)return;
            SetNativeTravelStage(trip,NativeTravelStage.CharacterMenu,"Finding the exact character on service account "+(trip.Service+1));
        }
        if(Player.IsLoaded||Client.IsLoggedIn||trip.LoginConfirmed)return;
        if(SellerAddon("SelectString")!=null){if(!trip.ServiceSent&&SelectVerifiedServiceAccount(trip.Service)){trip.ServiceSent=true;RecordTravelPrompt("Service account selected",new{slot=trip.Service});}return;}
        trip.ServiceSent=false;
        var loginWorld=trip.TransferCommitted||trip.Stage==NativeTravelStage.Login?trip.Leg:trip.Source;
        if(SelectLoginDataCenter(WorldName(loginWorld)??"",ref trip.DcSelected))return;
        if(SellerAddon("_TitleMenu")!=null){trip.DcSelected=false;ClickLoginMenuButton("_TitleMenu",5);return;}
        var lobby=AgentLobby.Instance();if(lobby==null)return;
        if(lobby->ServiceAccountIndex>=0&&lobby->ServiceAccountIndex!=trip.Service)throw new InvalidOperationException("The displayed service account differs from the requested account.");
        if(trip.Stage==NativeTravelStage.Login&&trip.LoginSent){
            if(ConfirmNativeLogin(true,trip.Character,WorldName(trip.Home)??"",trip.Service,trip.Name,(ushort)trip.Leg)){
                trip.LoginConfirmed=true;nativeTravelStatus="Login accepted; waiting for the queue and destination arrival";RecordTravelPrompt("Login confirmed",new{trip.Character,trip.Leg,trip.Service});
            }
            return; // Never touch queue dialogs after issuing login.
        }
        if(lobby->TemporaryLocked)return;
        if(trip.Stage==NativeTravelStage.CharacterMenu){
            var menu=SellerAddon("_CharaSelectListMenu");if(menu==null)return;
            var index=NativeTravelCharacterIndex(trip,out var world);
            if(index<0){RequestNativeTravelLoginWorld(trip,trip.Source);return;}
            if(world!=trip.Source)throw new InvalidOperationException("The character's current world changed before the transfer request.");
            var context=SellerAddon("ContextMenu");
            if(context!=null){
                if(!trip.ContextSent||lobby->HoveredCharacterContentId!=trip.Character)throw new InvalidOperationException("The character context menu does not belong to this trip.");
                var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Lobby>().GetRowOrDefault(trip.ReturnHome?1117u:1150u)?.Text.ToString()??"";
                if(context->AtkValues==null||context->AtkValuesCount<9)return;
                var count=(int)context->AtkValues[0].UInt;var list=context->GetComponentListById(2);
                if(count is <1 or >32||context->AtkValuesCount<8+count||list==null)return;
                for(var i=0;i<count;i++)if(((int)context->AtkValues[8+i].Type&15) is 8 or 10&&!list->GetItemDisabledState(i)&&NativeEventPolicy.Prompt(expected,TravelMenuText(context->AtkValues[8+i].String.Value)??"")){
                    SetNativeTravelStage(trip,NativeTravelStage.VisitIntro,trip.ReturnHome?"Returning this character home":"Opening native DC destinations");NativeTravelCallback(context,0,i,0);return;
                }
                throw new InvalidOperationException("The requested travel option is unavailable for this character.");
            }
            if(trip.ContextSent)return;
            if(index>250)throw new InvalidOperationException("Character index outside supported bounds.");
            var evt=new AtkEvent{Listener=(AtkEventListener*)menu,Target=&AtkStage.Instance()->AtkEventTarget,State=new(){StateFlags=(AtkEventStateFlags)(5+index)}};
            var data=new AtkEventData();((byte*)&data)[6]=1;
            menu->ReceiveEvent(AtkEventType.MouseClick,5+index,&evt,&data);trip.ContextSent=true;
            RecordTravelPrompt("Character context requested",new{trip.Character,index});return;
        }
        if(trip.Stage==NativeTravelStage.VisitIntro){
            var home=SellerAddon("LobbyWKTCheckHome");
            if(trip.ReturnHome&&home!=null){if(NativeEventButton(home,home->GetComponentButtonById(3))){trip.TransferCommitted=true;SetNativeTravelStage(trip,NativeTravelStage.TransferWait,"Waiting for home-world return");}return;}
            var intro=(AddonLobbyDKTCheck*)SellerAddon("LobbyDKTCheck");
            if(!trip.ReturnHome&&intro!=null){
                // Node-list index 3 is NOT node ID 3 (the Help button).
                // Use the typed confirmation control and wait for the destination UI.
                if(!trip.IntroSent)trip.IntroSent=NativeEventButton((AtkUnitBase*)intro,intro->ConfirmButton);
                return;
            }
            if(SellerAddon("LobbyDKTWorldList")!=null){SetNativeTravelStage(trip,NativeTravelStage.Destination,"Checking the native destination list");return;}
            if(trip.ReturnHome&&SellerAddon("LobbyDKTCheckExec")!=null){SetNativeTravelStage(trip,NativeTravelStage.TransferConfirm,"Verifying home-return confirmation");return;}
        }
        if(trip.Stage==NativeTravelStage.Destination){
            var menu=SellerAddon("LobbyDKTWorldList");if(menu==null)return;
            if(trip.ReturnHome){
                // Home-return dialog fixes the destination itself. Final prompt is
                // still verified against character, home world and home DC.
                if(NativeEventButton(menu,menu->GetComponentButtonById(25)))SetNativeTravelStage(trip,NativeTravelStage.TransferConfirm,"Verifying home-return destination");return;
            }
            if(TravelValueText(menu,152)!=TravelDcName(trip.Leg)){SelectNativeTravelTree(menu,23,TravelDcName(trip.Leg));return;}
            if(TravelValueText(menu,10)!=WorldName(trip.Leg)){SelectNativeTravelTree(menu,24,WorldName(trip.Leg)??"");return;}
            if(NativeEventButton(menu,menu->GetComponentButtonById(25)))SetNativeTravelStage(trip,NativeTravelStage.TransferConfirm,"Verifying destination before transfer");return;
        }
        if(trip.Stage==NativeTravelStage.TransferConfirm){
            var menu=(AddonLobbyDKTCheckExec*)SellerAddon("LobbyDKTCheckExec");if(menu==null||menu->PromptText==null)return;
            var text=TravelMenuText(menu->PromptText->NodeText.StringPtr)??"";
            if(!NativeTravelPolicy.TransferPrompt(text,trip.Name,WorldName(trip.Leg)??"",TravelDcName(trip.Leg)))throw new InvalidOperationException("Transfer confirmation does not match the requested character and destination.");
            if(NativeEventButton((AtkUnitBase*)menu,menu->ConfirmButton)){
                trip.TransferCommitted=true;trip.DcSelected=false;
                SetNativeTravelStage(trip,NativeTravelStage.TransferWait,"Transfer submitted; waiting for the game (the transfer may no longer be cancellable)");
            }return;
        }
        if(trip.Stage==NativeTravelStage.TransferWait){
            var ok=(AddonSelectOk*)SellerAddon("SelectOk");
            if(ok!=null){
                var text=ok->PromptText==null?"":TravelMenuText(ok->PromptText->NodeText.StringPtr)??"";
                if(text.Contains(WorldName(trip.Leg)??"\0",StringComparison.Ordinal)&&
                   (text.Contains("arrived safely",StringComparison.OrdinalIgnoreCase)||text.Contains("transfer successful",StringComparison.OrdinalIgnoreCase))){
                    if(NativeEventButton((AtkUnitBase*)ok,ok->OkButton)){CompleteNativeTransfer(trip);}
                }else nativeTravelStatus="Game message needs attention; it has been recorded. Confirm it manually to continue.";
                return;
            }
            if(NativeTravelCharacterIndex(trip,out var at)>=0&&at==trip.Leg){CompleteNativeTransfer(trip);}
            return;
        }
        if(trip.Stage==NativeTravelStage.Login){
            var menu=SellerAddon("_CharaSelectListMenu");if(menu==null)return;
            var index=NativeTravelCharacterIndex(trip,out var world);
            if(index<0){RequestNativeTravelLoginWorld(trip,trip.Leg);return;}
            if(world!=trip.Leg){nativeTravelStatus="Waiting for the character's destination world to update";return;}
            ClickNativeLoginCharacter(menu,index);trip.LoginSent=true;RecordTravelPrompt("Login requested",new{trip.Character,trip.Leg,trip.Service});
        }
    }
    private void CompleteNativeTransfer(NativeWorldTrip trip)
    {
        trip.DcSelected=false;
        if(NativeTravelPolicy.ContinueDcFromHome(trip.ReturnHome,trip.Leg,trip.Home,trip.Destination,TravelDc(trip.Home),TravelDc(trip.Destination))){
            PrepareNativeFinalLeg(trip);
            SetNativeTravelStage(trip,NativeTravelStage.CharacterMenu,"Home return complete; continuing DC travel without logging in");
        }else SetNativeTravelStage(trip,NativeTravelStage.Login,"Transfer complete; logging into "+WorldName(trip.Leg));
    }
    private void RequestNativeTravelLoginWorld(NativeWorldTrip trip,uint world){
        if(trip.RequestedLoginWorld==world)return;
        if(SelectEstateLoginWorld((ushort)world)){trip.RequestedLoginWorld=world;RecordTravelPrompt("Login world selected",new{world});}
    }
    private unsafe int NativeTravelCharacterIndex(NativeWorldTrip trip,out uint world)
    {
        world=0;var lobby=AgentLobby.Instance();if(lobby==null||lobby->LobbyData.CharaSelectEntries.LongCount>512)return -1;
        var index=-1;var current=0;var matches=0;
        foreach(var item in lobby->LobbyData.CharaSelectEntries){var c=item.Value;
            if(c!=null&&c->ContentId==trip.Character&&c->NameString==trip.Name&&c->HomeWorldId==trip.Home&&(c->LoginFlags&CharaSelectCharacterEntryLoginFlags.Locked)==0){index=current;world=c->CurrentWorldId;matches++;}current++;
        }return matches==1?index:-1;
    }
    private unsafe string TravelValueText(AtkUnitBase* addon,int index)=>addon!=null&&addon->AtkValues!=null&&index>=0&&index<addon->AtkValuesCount&&((int)addon->AtkValues[index].Type&15) is 8 or 10?TravelMenuText(addon->AtkValues[index].String.Value)??"":"";
    private unsafe object ReadNativeTravelTrees(AtkUnitBase* addon)
    {
        var result=new List<object>();
        foreach(var id in new uint[]{23,24}){
            var node=addon->GetNodeById(id);if(node==null)continue;
            var component=node->GetAsAtkComponentNode();if(component==null||component->Component==null||component->Component->GetComponentType()!=ComponentType.TreeList)continue;
            var tree=(AtkComponentTreeList*)component->Component;if(tree->Items.LongCount is <0 or >128)continue;
            var items=new List<object>();
            for(var i=0;i<tree->Items.LongCount;i++){
                var item=tree->Items[i].Value;if(item==null)continue;
                var text=item->StringValues.LongCount is >0 and <=32?TravelMenuText(item->StringValues[0].Value):null;
                items.Add(new{index=i,text,hidden=item->IsHidden,state=(int)item->State,type=(int)item->Type,depth=item->Depth});
            }
            result.Add(new{id,items});
        }
        return result;
    }
    private unsafe bool SelectNativeTravelTree(AtkUnitBase* addon,uint nodeId,string expected)
    {
        var node=addon->GetNodeById(nodeId);if(node==null)return false;
        var component=node->GetAsAtkComponentNode();if(component==null||component->Component==null||component->Component->GetComponentType()!=ComponentType.TreeList)return false;
        var tree=(AtkComponentTreeList*)component->Component;
        if(tree->Items.LongCount is <=0 or >128)return false;
        for(var i=0;i<tree->Items.LongCount;i++){
            var item=tree->Items[i].Value;
            if(item==null||item->StringValues.LongCount is <1 or >32||item->IsHidden||(item->State&TreeListItemState.Disabled)!=0||(item->Type&(TreeListItemType.Group|TreeListItemType.SectionHeader))!=0)continue;
            if(TravelMenuText(item->StringValues[0].Value)!=expected)continue;
            // Use the game's virtual tree selection; validate the selected names
            // on the next tick before enabling any confirmation.
            tree->SelectItem(i,false);
            tree->DispatchItemEvent(i,AtkEventType.ListItemClick);
            RecordTravelPrompt("Destination list selection",new{nodeId,index=i,expected});return true;
        }
        nativeTravelStatus="Waiting for available destination: "+expected;return false;
    }
}
