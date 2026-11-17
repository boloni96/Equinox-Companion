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
        // A rejection can arrive before the final confirmation advances the stage.
        // Inspect it before selecting another data center or waiting on lobby locks.
        var failure=(AddonSelectOk*)SellerAddon("SelectOk");
        if(failure!=null&&failure->PromptText!=null){
            var message=TravelMenuText(failure->PromptText->NodeText.StringPtr)??"";
            if(NativeTravelPolicy.HomeReturnSucceeded(trip.Stage,trip.ReturnHome,trip.TransferCommitted,trip.Leg,trip.Home,message)){
                // Acknowledge before lobby lock / DC selection gates. The character
                // list can retain the visiting world until this message is closed.
                if(!trip.HomeReturnAcknowledged&&NativeEventButton((AtkUnitBase*)failure,failure->OkButton)){
                    trip.HomeReturnAcknowledged=true;
                    RecordTravelPrompt("Home return success acknowledged",new{trip.Character,trip.Home,trip.Leg,trip.Destination});
                }
                nativeTravelStatus="Home return succeeded; waiting for character world verification";
                return;
            }
            if(NativeTravelPolicy.HomeReturnRejected(trip.Stage,trip.ReturnHome,message)){
                RecordTravelPrompt("Home return rejected",new{message,trip.Source,trip.Leg,stage=trip.Stage.ToString()});
                trip.TransferCommitted=false;
                StopNativeWorldTravel("The game refused the home-world return [1000]. Dismiss the game message before retrying.");
                return;
            }
        }
        if(SellerAddon("SelectString")!=null){if(!trip.ServiceSent&&SelectVerifiedServiceAccount(trip.Service)){trip.ServiceSent=true;RecordTravelPrompt("Service account selected",new{slot=trip.Service});}return;}
        trip.ServiceSent=false;
        var loginWorld=trip.TransferCommitted||trip.Stage==NativeTravelStage.Login?trip.Leg:trip.Source;
        var selectingCharacter=trip.Stage==NativeTravelStage.CharacterMenu || trip.Stage==NativeTravelStage.Login && !trip.LoginSent;
        if(trip.Stage is NativeTravelStage.CharacterMenu or NativeTravelStage.Login){
            var physicalWorld=trip.Stage==NativeTravelStage.CharacterMenu?trip.Source:trip.Leg;
            loginWorld=CharacterLoginWorldPolicy.ListWorld(trip.Home,physicalWorld,TravelDc(trip.Home),TravelDc(physicalWorld),TravelRegion(trip.Home),TravelRegion(physicalWorld));
            var currentLobby=AgentLobby.Instance();
            if(selectingCharacter && currentLobby!=null && SellerAddon("_CharaSelectListMenu")!=null){
                var displayedWorld=currentLobby->WorldId;
                var displayedDc=TravelDc(displayedWorld);
                if(currentLobby->TemporaryLocked || displayedDc==0){
                    trip.LoginList.Ready(-1,now);
                    return;
                }
                if(displayedDc!=TravelDc(loginWorld)){
                    nativeTravelStatus=$"Opening {TravelDcName(loginWorld)} for {trip.Name}'s {WorldName(loginWorld)} character list";
                    if(SellerAddon("ContextMenu")!=null || SellerAddon("SelectYesno")!=null || SellerAddon("SelectOk")!=null || SellerAddon("_CharaSelectWorldServer")!=null)return;
                    if(now>=trip.NextWorldSelection && ClickLoginMenuButton("_CharaSelectReturn",4)){
                        trip.DcSelected=false;
                        trip.NextWorldSelection=now.AddSeconds(3);
                        RecordTravelPrompt("Changing character-list data center",new{displayedWorld,loginWorld,physicalWorld});
                    }
                    return;
                }
            }
        }
        if(SelectLoginDataCenter(WorldName(loginWorld)??"",ref trip.DcSelected))return;
        if(SellerAddon("_TitleMenu")!=null){trip.DcSelected=false;ClickLoginMenuButton("_TitleMenu",5);return;}
        var lobby=AgentLobby.Instance();if(lobby==null)return;
        if(lobby->ServiceAccountIndex>=0&&lobby->ServiceAccountIndex!=trip.Service)throw new InvalidOperationException("The displayed service account differs from the requested account.");
        if(trip.Stage==NativeTravelStage.Login&&trip.LoginSent){
            var dialog=(AddonSelectYesno*)SellerAddon("SelectYesno");
            if(lobby->WorldId==loginWorld && ConfirmNativeLogin(true,trip.Character,WorldName(trip.Home)??"",trip.Service,trip.Name,(ushort)trip.Leg)){
                trip.LoginConfirmed=true;nativeTravelStatus="Login accepted; waiting for the queue and destination arrival";RecordTravelPrompt("Login confirmed",new{trip.Character,trip.Leg,trip.Service});
                return;
            }
            var elapsed=now-trip.LoginRequestedAt;
            if(dialog!=null && elapsed>=TimeSpan.FromSeconds(3)){
                nativeTravelStatus=$"Verifying login for {trip.Name} on {WorldName(trip.Leg)} · displayed list: {WorldName(lobby->WorldId)}";
                var prompt=dialog->PromptText==null?"":TravelMenuText(dialog->PromptText->NodeText.StringPtr)??"";
                if(lobby->WorldId!=0 && lobby->WorldId!=loginWorld && lobby->ServiceAccountIndex==trip.Service &&
                    trip.StaleLoginRetries<2 && TravelLoginListGate.IsNamedLoginPrompt(prompt,trip.Name) &&
                    NativeEventButton((AtkUnitBase*)dialog,dialog->NoButton)){
                    trip.LoginSent=false;
                    trip.StaleLoginRetries++;
                    trip.LoginList.Ready(-1,now);
                    RecordTravelPrompt("Dismissed stale character login",new{trip.Name,expectedWorld=loginWorld,displayedWorld=lobby->WorldId,trip.StaleLoginRetries});
                    return;
                }
                if(elapsed>=TimeSpan.FromSeconds(15))throw new InvalidOperationException(nativeTravelStatus+". Review the open game prompt.");
            }
            if(dialog==null && elapsed>=TimeSpan.FromSeconds(20))
                throw new InvalidOperationException("The game did not present a verifiable login prompt. Check the character list or login queue.");
            return;
        }
        if(lobby->TemporaryLocked){trip.LoginList.Ready(-1,now);nativeTravelStatus="Waiting for the character-selection server to unlock · "+trip.Stage;return;}
        if(trip.Stage==NativeTravelStage.CharacterMenu){
            var menu=SellerAddon("_CharaSelectListMenu");if(menu==null)return;
            if(lobby->ServiceAccountIndex!=trip.Service)return;
            if(lobby->WorldId!=loginWorld){
                nativeTravelStatus=$"Selecting {WorldName(loginWorld)} to find {trip.Name} visiting {WorldName(trip.Source)}";
                if(now>=trip.NextWorldSelection && SellerAddon("ContextMenu")==null && SellerAddon("SelectYesno")==null && SellerAddon("SelectOk")==null && SelectEstateLoginWorld((ushort)loginWorld)){
                    trip.NextWorldSelection=now.AddSeconds(5);
                    RecordTravelPrompt("Selected pre-transfer character list",new{loginWorld,trip.Source,trip.Home});
                }
                return;
            }
            var index=NativeTravelCharacterIndex(trip,out var world);
            if(index<0){nativeTravelStatus=$"Waiting for {trip.Name} on the {WorldName(loginWorld)} list";return;}
            if(world!=trip.Source)throw new InvalidOperationException("The character's current world changed before the transfer request.");
            var context=SellerAddon("ContextMenu");
            if(context!=null){
                var action=trip.ContextRecovery.Check(true,trip.ContextSent,lobby->HoveredCharacterContentId,trip.Character,now);
                if(action!=TravelContextAction.Use){
                    if(action==TravelContextAction.Wait)return;
                    if(action==TravelContextAction.Stop)throw new InvalidOperationException("Could not verify the character travel menu after three retries. Close it and retry.");
                    RecordTravelPrompt("Reopening character travel menu",new{trip.Character,hovered=lobby->HoveredCharacterContentId,trip.ContextSent,retries=trip.ContextRecovery.Retries});
                    CloseSellerAddon("ContextMenu");trip.ContextSent=false;trip.Next=now.AddMilliseconds(500);return;
                }
                var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Lobby>().GetRowOrDefault(trip.ReturnHome?1117u:1150u)?.Text.ToString()??"";
                if(context->AtkValues==null||context->AtkValuesCount<9)return;
                var count=(int)context->AtkValues[0].UInt;var list=context->GetComponentListById(2);
                if(count is <1 or >32||context->AtkValuesCount<8+count||list==null)return;
                for(var i=0;i<count;i++)if(((int)context->AtkValues[8+i].Type&15) is 8 or 10&&!list->GetItemDisabledState(i)&&NativeEventPolicy.Prompt(expected,TravelMenuText(context->AtkValues[8+i].String.Value)??"")){
                    SetNativeTravelStage(trip,NativeTravelStage.VisitIntro,trip.ReturnHome?"Returning this character home":"Opening native DC destinations");NativeTravelCallback(context,0,i,0);return;
                }
                throw new InvalidOperationException("The requested travel option is unavailable for this character.");
            }
            if(trip.ContextSent){
                var action=trip.ContextRecovery.Check(false,true,0,trip.Character,now);
                if(action==TravelContextAction.Wait)return;
                if(action==TravelContextAction.Stop)throw new InvalidOperationException("Character travel menu did not open after three retries.");
                trip.ContextSent=false;
            }
            if(index>250)throw new InvalidOperationException("Character index outside supported bounds.");
            var evt=new AtkEvent{Listener=(AtkEventListener*)menu,Target=&AtkStage.Instance()->AtkEventTarget,State=new(){StateFlags=(AtkEventStateFlags)(5+index)}};
            var data=new AtkEventData();((byte*)&data)[6]=1;
            menu->ReceiveEvent(AtkEventType.MouseClick,5+index,&evt,&data);trip.ContextSent=true;trip.ContextRecovery.Requested(now);
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
                }else nativeTravelStatus="Waiting for game message (SelectOk): "+(string.IsNullOrWhiteSpace(text)?"[prompt text unavailable]":text)+" · Review and dismiss it in game to continue.";
                return;
            }
            if(NativeTravelCharacterIndex(trip,out var at)>=0&&at==trip.Leg){CompleteNativeTransfer(trip);}
            return;
        }
        if(trip.Stage==NativeTravelStage.Login){
            var menu=SellerAddon("_CharaSelectListMenu");
            if(menu==null || !menu->IsReady || lobby->ServiceAccountIndex!=trip.Service ||
                SellerAddon("SelectYesno")!=null || SellerAddon("SelectOk")!=null || SellerAddon("ContextMenu")!=null){
                trip.LoginList.Ready(-1,now);
                return;
            }
            if(lobby->WorldId!=loginWorld){
                trip.LoginList.Ready(-1,now);
                nativeTravelStatus=$"Selecting {WorldName(loginWorld)} for login · displayed list: {WorldName(lobby->WorldId)}";
                if(lobby->WorldId!=0 && now>=trip.NextWorldSelection && SelectEstateLoginWorld((ushort)loginWorld)){
                    trip.NextWorldSelection=now.AddSeconds(5);
                    RecordTravelPrompt("Reselected login world",new{world=loginWorld});
                }
                return;
            }
            var index=NativeTravelCharacterIndex(trip,out var world);
            nativeTravelStatus=$"Waiting for {trip.Name} on the settled {WorldName(loginWorld)} list";
            if(!trip.LoginList.Ready(world==trip.Leg?index:-1,now))return;
            usingSharedTravel=true;
            try{ClickNativeLoginCharacter(menu,index);}finally{usingSharedTravel=false;}
            trip.LoginSent=true;trip.LoginRequestedAt=now;
            RecordTravelPrompt("Login requested",new{trip.Character,trip.Leg,trip.Service,index});
        }
    }
    private void CompleteNativeTransfer(NativeWorldTrip trip)
    {
        trip.DcSelected=false;
        if(trip.NoLogin&&trip.Leg==trip.Destination){
            StopNativeWorldTravel("Transfer complete at "+WorldName(trip.Destination)+" · remaining logged out.",arrived:true);return;
        }
        if(!UsesWorldVisit(trip.Home,trip.Destination)&&NativeTravelPolicy.ContinueDcFromHome(trip.ReturnHome,trip.Leg,trip.Home,trip.Destination,TravelDc(trip.Home),TravelDc(trip.Destination))){
            PrepareNativeFinalLeg(trip);
            SetNativeTravelStage(trip,NativeTravelStage.CharacterMenu,"Home return complete; continuing DC travel without logging in");
        }else SetNativeTravelStage(trip,NativeTravelStage.Login,"Transfer complete; logging into "+WorldName(trip.Leg));
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
        if(addon==null||!addon->IsReady||addon->AtkValues==null||nodeId is not (23 or 24))return false;
        var node=addon->GetNodeById(nodeId);if(node==null)return false;
        var tree=node->GetAsAtkComponentTreeList();if(tree==null)return false;
        var names=new string[nodeId==23?16:8];
        for(var i=0;i<names.Length;i++)names[i]=TravelValueText(addon,nodeId==23?17+(i/4)*34+(i%4)*8:155+i*8);
        var choice=NativeDcListSelection.Find(nodeId,expected,names);
        if(choice is not {} selection){nativeTravelStatus="Waiting for available destination: "+expected;return false;}
        // Match a visible, enabled row as well as the data entry. Header rows and
        // disabled destinations never become a substitute for the requested name.
        var treeBase=(AtkComponentBase*)tree;
        var layout=&treeBase->UldManager;
        if(layout->NodeList==null||layout->NodeListCount>256)return false;
        var highlight=-1;
        for(var i=0;i<layout->NodeListCount;i++){
            var row=layout->NodeList[i];if(row==null||!row->IsVisible())continue;
            var renderer=row->GetAsAtkComponentListItemRenderer();if(renderer==null||!renderer->IsEnabled||renderer->ListItemIndex is <0 or >127)continue;
            var contents=&((AtkComponentBase*)renderer)->UldManager;
            if(contents->NodeList==null||contents->NodeListCount>64)continue;
            for(var n=0;n<contents->NodeListCount;n++){
                var child=contents->NodeList[n];if(child==null)continue;
                var label=child->GetAsAtkTextNode();
                if(label==null||!child->IsVisible()||child->Alpha_2!=255||TravelMenuText(label->NodeText.StringPtr)!=expected)continue;
                if(highlight>=0&&highlight!=renderer->ListItemIndex)return false;
                highlight=renderer->ListItemIndex;
            }
        }
        if(highlight<0){nativeTravelStatus="Waiting for enabled destination row: "+expected;return false;}
        var key=new NativeDcTreeKey{Selected=1,Item=selection.PackedItem};var keyPointer=&key;
        var data=new NativeDcTreeInput{Key=&keyPointer,Highlight=highlight};
        var evt=new AtkEvent{Target=(AtkEventTarget*)node,Listener=&treeBase->AtkEventListener,Param=1,State=new(){EventType=AtkEventType.ListItemClick}};
        // Ordinary DispatchItemEvent omits the region/item payload this addon
        // reads. Dispatch directly, then verify the selected names next tick.
        usingSharedTravel=true;
        try{addon->ReceiveEvent(AtkEventType.ListItemClick,selection.EventParameter,&evt,(AtkEventData*)&data);tree->SelectItem(highlight,false);}
        finally{usingSharedTravel=false;}
        if(nativeWorldTrip is {} trip)trip.Next=DateTimeOffset.UtcNow.AddSeconds(2);
        RecordTravelPrompt("Destination list selection",new{nodeId,expected,selection.Category,selection.Item,selection.PackedItem,highlight,selection.EventParameter});
        return true;
    }
}
