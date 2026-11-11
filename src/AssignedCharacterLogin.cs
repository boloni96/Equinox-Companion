using FFXIVClientStructs.FFXIV.Client.System.String;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record AssignedLogin(SharedCharacter Target,ulong From,int ServiceAccount,string Pairing,string World,DateTimeOffset Deadline);
    private AssignedLogin? assignedLogin;
    private bool assignedDcSent,assignedServiceSent,assignedLoginSent,assignedLoginConfirmed,assignedLogoutConfirmed,assignedAccountReturnSent;
    private DateTimeOffset assignedLoginNext,loginWorldNext;
    private readonly HashSet<ushort> loginWorldChecked=[];
    private ushort loginWorldPending,loginWorldSample;private DateTimeOffset loginWorldSentAt,loginWorldStableAt;
    private string assignedLoginError="";
    private uint assignedVisitingReturnFromDc;
    private bool identificationDcSent;
    private string LastLoginWorld(SharedCharacter target)
    {
        var observed=config.Discoveries.Where(e=>e.Actor.ContentId==target.ContentId).OrderByDescending(e=>e.At).FirstOrDefault();
        var presence=adventureBoards.Select(b=>b.Locations?.GetValueOrDefault(target.Id)).Where(x=>x!=null&&DateTimeOffset.TryParse(x.At,out _)).OrderByDescending(x=>DateTimeOffset.Parse(x!.At)).FirstOrDefault();
        if(presence!=null&&(observed==null||DateTimeOffset.Parse(presence.At)>observed.At))return CharacterSelectionWorld(target.World,presence.World);
        return CharacterSelectionWorld(target.World,observed?.Actor.CurrentWorldName??target.World);
    }
    private string CharacterSelectionWorld(string home,string current)
    {
        var worlds=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        var homeDc=worlds.Where(w=>w.Name.ToString()==home).Select(w=>w.LogicalDc().RowId).FirstOrDefault();
        var currentDc=worlds.Where(w=>w.Name.ToString()==current).Select(w=>w.LogicalDc().RowId).FirstOrDefault();
        var homeRegion=worlds.Where(w=>w.Name.ToString()==home).Select(w=>w.LogicalDc().Region.RowId).FirstOrDefault();
        var currentRegion=worlds.Where(w=>w.Name.ToString()==current).Select(w=>w.LogicalDc().Region.RowId).FirstOrDefault();
        return CharacterLoginWorldPolicy.Resolve(home,current,homeDc,currentDc,homeRegion,currentRegion);
    }
    private int GardenServiceAccount(SharedCharacter target)
    {
        var current=CurrentSharedCharacter();if(current==null)return -1;
        var from=CharacterSwitchScope(current.Id);var to=CharacterSwitchScope(target.Id);
        if(from.PersonId.Length==0||from.PersonId!=to.PersonId)return -1;
        if(LoginRelationship(target,from,to)==CharacterLoginRelationship.Unverified)return -1;
        if(from.AccountId==to.AccountId)return CurrentLoginServiceAccount();
        var key=SquareEnixLoginState.TargetAccountKey(config.SharedRoster,target,to.PersonId,to.AccountId);
        return RecordedCharacterServiceSlot(target);
    }
    private unsafe bool StartAssignedCharacterSwitch(SharedCharacter target,bool allowLinkedAccounts=false)
    {
        if(assignedLogin!=null)return assignedLogin.Target.Id==target.Id;
        var current=CurrentSharedCharacter();var index=GardenServiceAccount(target);
        if(current==null||index is <0 or >15||(!allowLinkedAccounts&&current.AccountId!=target.AccountId)||!DeliveryCharacters().Any(x=>x.Character.Id==target.Id&&x.Person.Characters.Any(c=>c.Id==current.Id))){characterTravelStatus="Verify this client's current service account before automatic switching.";return false;}
        if(nativeWorldTrip!=null||estateHomeReturn!=null||estateTrip!=null||LifestreamBusy()||followSession.Armed||TraderRunning||HelperInCutscene()){characterTravelStatus="Finish the active operation before switching.";return false;}
        try{
            var ui=UIModule.Instance();if(ui==null)throw new InvalidOperationException("Game menu unavailable.");
            assignedLogin=new(target,Player.ContentId,index,config.PairingKey,LastLoginWorld(target),DateTimeOffset.UtcNow.AddMinutes(5));
            assignedVisitingReturnFromDc=0;assignedDcSent=assignedServiceSent=assignedLoginSent=assignedLoginConfirmed=assignedLogoutConfirmed=assignedAccountReturnSent=false;assignedLoginNext=default;loginWorldChecked.Clear();loginWorldNext=default;loginWorldPending=loginWorldSample=0;assignedLoginError="";
            using var command=new Utf8String();command.SetString("/logout");ui->ProcessChatBoxEntry(&command,0,false);
            characterSwitchFrom=Player.ContentId;characterSwitchTarget=target;characterSwitchUntil=assignedLogin.Deadline;
            characterTravelStatus=$"Switching using this client's verified service account {index+1}: {target.Name} @ {target.World}.";return true;
        }catch(Exception){assignedLogin=null;characterTravelStatus="Native logout is unavailable; no automatic login was requested.";return false;}
    }
    private bool StartAssignedLoginFromList(SharedCharacter target)
    {
        var slot=CurrentLoginServiceAccount();
        if(slot is <0 or >15||assignedLogin!=null)return false;
        assignedLogin=new(target,0,slot,config.PairingKey,LastLoginWorld(target),DateTimeOffset.UtcNow.AddMinutes(5));
        assignedVisitingReturnFromDc=0;assignedDcSent=assignedServiceSent=assignedLoginSent=assignedLoginConfirmed=assignedLogoutConfirmed=assignedAccountReturnSent=false;
        assignedLoginNext=default;loginWorldChecked.Clear();loginWorldNext=default;loginWorldPending=loginWorldSample=0;assignedLoginError="";
        characterSwitchFrom=0;characterSwitchTarget=target;characterSwitchUntil=assignedLogin.Deadline;
        return true;
    }
    private unsafe bool BeginNativeCharacterLogin(SharedCharacter character,int slot)
    {
        var target=TitleLoginTarget(character.Id);var lobby=AgentLobby.Instance();var menu=SellerAddon("_CharaSelectListMenu");
        if(target==null||lobby==null||lobby->TemporaryLocked||lobby->ServiceAccountIndex!=slot||menu==null||
            SellerAddon("SelectYesno")!=null||SellerAddon("SelectOk")!=null||SellerAddon("SelectString")!=null)return false;
        var entries=lobby->LobbyData.CharaSelectEntries;if(entries.LongCount is <=0 or >512)return false;
        var index=-1;var current=0;var matches=0;
        foreach(var item in entries){
            var entry=item.Value;
            if(entry!=null&&entry->ContentId.ToString()==target.ContentId&&entry->NameString==target.Name&&WorldName(entry->HomeWorldId)==target.World&&
                (entry->LoginFlags&CharaSelectCharacterEntryLoginFlags.Locked)==0){
                var listDc=CharacterLoginWorldPolicy.ListDc(TravelDc(entry->HomeWorldId),TravelDc(entry->CurrentWorldId),TravelRegion(entry->HomeWorldId),TravelRegion(entry->CurrentWorldId));
                if(listDc==0||listDc!=TravelDc(lobby->WorldId))return false;
                index=current;matches++;
            }
            current++;
        }
        if(matches!=1||index<0||index>250)return false;
        ClickNativeLoginCharacter(menu,index);return true;
    }
    // The character-list UI tracks its displayed selection in HoveredCharacterContentId.
    // SelectedCharacterContentId is not the pre-login selection identity.
    private static unsafe void ClickNativeLoginCharacter(AtkUnitBase* menu,int index)
    {
        if(menu==null||index<0||index>250)throw new InvalidOperationException("Invalid character-list entry.");
        var eventIndex=(byte)(5+index);
        var evt=new AtkEvent{Listener=(AtkEventListener*)menu,Target=&AtkStage.Instance()->AtkEventTarget,
            State=new(){StateFlags=(AtkEventStateFlags)eventIndex}};
        var data=new AtkEventData();((byte*)&data)[6]=0;
        menu->ReceiveEvent(AtkEventType.MouseClick,eventIndex,&evt,&data);
    }
    private unsafe bool ConfirmNativeLogin(bool requested,ulong character,string homeWorld,int slot,string name,ushort? currentWorld=null)
    {
        var lobby=AgentLobby.Instance();var dialog=(AddonSelectYesno*)SellerAddon("SelectYesno");
        if(lobby==null||dialog==null||dialog->PromptText==null)return false;
        var entries=lobby->LobbyData.CharaSelectEntries;if(entries.LongCount is <=0 or >512)return false;
        var matches=0;
        foreach(var item in entries){
            var entry=item.Value;
            if(entry!=null&&entry->ContentId==character&&entry->NameString==name&&WorldName(entry->HomeWorldId)==homeWorld&&
                (currentWorld==null||entry->CurrentWorldId==currentWorld.Value)&&
                (entry->LoginFlags&CharaSelectCharacterEntryLoginFlags.Locked)==0)matches++;
        }
        if(!EstateTravelCommand.CanConfirmDisplayedLogin(requested,character,lobby->HoveredCharacterContentId,
            slot,lobby->ServiceAccountIndex,matches,name,TravelMenuText(dialog->PromptText->NodeText.StringPtr)??""))return false;
        return NativeEventButton((AtkUnitBase*)dialog,dialog->YesButton);
    }
    private unsafe object CaptureNativeLoginDiagnostic()
    {
        var lobby=AgentLobby.Instance();
        var target=assignedLogin?.Target is {} c?TitleLoginTarget(c.Id):null;
        var expected=target?.ContentId??estateHomeReturn?.Character.ToString()??"";
        var rows=new List<object>();
        if(lobby!=null&&lobby->LobbyData.CharaSelectEntries.LongCount is >0 and <=512)
            foreach(var item in lobby->LobbyData.CharaSelectEntries){
                var entry=item.Value;
                if(entry!=null&&(entry->ContentId.ToString()==expected||entry->ContentId==lobby->HoveredCharacterContentId))
                    rows.Add(new{character=entry->ContentId.ToString(),name=entry->NameString,homeWorld=entry->HomeWorldId,currentWorld=entry->CurrentWorldId,flags=(int)entry->LoginFlags});
            }
        var dialog=(AddonSelectYesno*)SellerAddon("SelectYesno");
        return new{status=characterTravelStatus,expectedCharacter=expected,expectedName=target?.Name??estateHomeReturn?.Name,
            expectedSlot=assignedLogin?.ServiceAccount??estateHomeReturn?.ServiceSlot,
            requested=assignedLogin!=null?assignedLoginSent:estateHomeReturn?.LoginSent??false,
            confirmed=assignedLogin!=null?assignedLoginConfirmed:estateHomeReturn?.LoginConfirmed??false,
            currentSlot=lobby==null?-1:(int)lobby->ServiceAccountIndex,
            displayedWorld=lobby==null?0:lobby->WorldId,
            lobbyCurrentWorld=lobby==null?0:lobby->LobbyData.CurrentWorldId,
            requestedWorld=assignedLogin?.World,waitingToLeaveDc=assignedVisitingReturnFromDc,
            displayedCharacter=lobby==null?"":lobby->HoveredCharacterContentId.ToString(),
            selectedCharacter=lobby==null?"":lobby->SelectedCharacterContentId.ToString(),
            locked=lobby!=null&&lobby->TemporaryLocked,
            prompt=dialog==null||dialog->PromptText==null?"":TravelMenuText(dialog->PromptText->NodeText.StringPtr),
            matchingEntries=rows.ToArray()};
    }
    private static unsafe void LoginCallback(AtkUnitBase* addon,params int[] args)
    {
        var values=stackalloc AtkValue[args.Length];for(var i=0;i<args.Length;i++)values[i]=new(){Type=AtkValueType.Int,Int=args[i]};addon->FireCallback((uint)args.Length,values,true);
    }
    private unsafe object? ReadLoginListEvidence()
    {
        var lobby=AgentLobby.Instance();
        if(lobby==null||lobby->TemporaryLocked||Player.IsLoaded)return null;
        var entries=lobby->LobbyData.CharaSelectEntries;
        if(entries.LongCount is <0 or >512)return null;
        var characters=new List<object>();
        foreach(var item in entries){
            var entry=item.Value;if(entry==null||entry->ContentId==0)continue;
            characters.Add(new{contentId=entry->ContentId.ToString(),name=entry->NameString,
                homeWorld=entry->HomeWorldId,homeName=WorldName(entry->HomeWorldId),
                currentWorld=entry->CurrentWorldId,currentName=WorldName(entry->CurrentWorldId),
                flags=(int)entry->LoginFlags});
            if(characters.Count>=64)break;
        }
        return new{displayedWorld=lobby->WorldId,displayedName=WorldName(lobby->WorldId),
            logicalDc=TravelDc(lobby->WorldId),dcName=TravelDcName(lobby->WorldId),
            serviceAccount=lobby->ServiceAccountIndex,characters,truncated=entries.LongCount>64};
    }
    private unsafe bool SelectLoginDataCenter(string world,ref bool sent)
    {
        var map=SellerAddon("TitleDCWorldMap");if(map==null)return false;
        if(sent)return true;
        var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().FirstOrDefault(w=>w.Name.ToString()==world);
        if(row.RowId==0||row.LogicalDc().RowId==0)throw new InvalidOperationException("The target data center could not be identified.");
        RecordTravelPrompt("Login data center requested",new{world,worldId=row.RowId,logicalDc=row.LogicalDc().RowId,dcName=row.LogicalDc().Name.ToString()});
        usingSharedTravel=true;
        try{LoginCallback(map,17,(int)row.LogicalDc().RowId);}finally{usingSharedTravel=false;}
        sent=true;return true;
    }
    private unsafe int[] AvailableLoginServiceSlots()
    {
        var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null)return [];
        var text=menu->GetTextNodeById(2);var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Lobby>().GetRowOrDefault(11)?.Text.ToString();
        if(text==null||string.IsNullOrWhiteSpace(expected)||TravelMenuText(text->NodeText.StringPtr)!=expected.Trim())return [];
        var list=menu->PopupMenu.PopupMenu.List;if(list==null||list->ListLength is <=0 or >16||!list->IsItemInteractionEnabled)return [];
        var slots=new List<int>();for(var i=0;i<list->ListLength;i++)if(!list->GetItemDisabledState(i))slots.Add(i);
        return slots.ToArray();
    }
    private unsafe bool SelectVerifiedServiceAccount(int index)
    {
        var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null)return false;
        var text=menu->GetTextNodeById(2);var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Lobby>().GetRowOrDefault(11)?.Text.ToString();
        if(text==null||string.IsNullOrWhiteSpace(expected)||TravelMenuText(text->NodeText.StringPtr)!=expected.Trim())throw new InvalidOperationException("An unrelated login dialog needs attention.");
        var list=menu->PopupMenu.PopupMenu.List;
        if(list==null||index<0||index>=list->ListLength||!list->IsItemInteractionEnabled||list->GetItemDisabledState(index))throw new InvalidOperationException("The verified service account is unavailable in this login.");
        list->DispatchItemEvent(index,AtkEventType.ListItemClick);return true;
    }
    // A ready empty world is a search result, not proof of the wrong account.
    private unsafe bool SelectNextKnownLoginWorld(string targetContent,string targetWorld,DateTimeOffset now)
    {
        if(now<loginWorldNext)return true;
        var lobby=AgentLobby.Instance();
        // This addon is loaded but normally hidden until World is clicked.
        // A ready character list and populated world data authorize selection.
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("_CharaSelectWorldServer").Address;
        if(lobby==null||addon==null||lobby->TemporaryLocked||SellerAddon("_CharaSelectListMenu")==null)return true;
        var available=lobby->LobbyData.LobbyUIClient.CurrentDataCenterWorlds;if(available.LongCount is <=0 or >32)return true;
        if(loginWorldPending!=0){if(lobby->WorldId!=loginWorldPending){if(now-loginWorldSentAt>TimeSpan.FromSeconds(15))throw new InvalidOperationException("World list change timed out; no login was requested.");return true;}loginWorldPending=0;loginWorldSample=lobby->WorldId;loginWorldStableAt=now;return true;}
        if(loginWorldSample!=lobby->WorldId){loginWorldSample=lobby->WorldId;loginWorldStableAt=now;return true;}
        // Populated, ready lists can advance promptly. Empty/unready lists
        // retain the longer settling window so loading is not mistaken for empty.
        var populated=TryReadReadyLoginList(out var worldEntries)&&worldEntries.Length>0;
        if(!CurrentLoginIdentification.WorldSettled(now-loginWorldStableAt,populated))return true;
        var targetAvailable=false;
        foreach(var candidate in available)if(candidate.NameString==targetWorld){targetAvailable=true;break;}
        if(!targetAvailable){
            RecordTravelPrompt("Login world search blocked",new{targetWorld,displayedWorld=lobby->WorldId,displayedDc=TravelDc(lobby->WorldId)});
            throw new InvalidOperationException("The loaded data center does not contain "+targetWorld+". Select its data center and retry; no unrelated worlds were searched.");
        }
        var module=FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule.Instance();
        if(module==null||module->AtkArrayDataHolder.StringArrayCount<=1||module->AtkArrayDataHolder.StringArrays==null)return true;
        var menuStrings=module->AtkArrayDataHolder.StringArrays[1];
        if(menuStrings==null||menuStrings->StringArray==null||menuStrings->Size<=0)return true;
        var menuNames=new List<string>();
        for(var i=0;i<Math.Min(menuStrings->Size,16);i++){
            var ptr=menuStrings->StringArray[i].Value;if(ptr==null)break;
            var name=TravelMenuText(ptr);if(name.Length==0)break;menuNames.Add(name);
        }
        if(menuNames.Count==0)return true;
        var preferred=(ushort)0;
        foreach(var c in lobby->LobbyData.LobbyUIClient.CurrentDataCenterCharacters)if(c.ContentId.ToString()==targetContent&&c.DeletedFlag==0&&(c.LoginFlags&CharaSelectCharacterEntryLoginFlags.Locked)==0)preferred=c.CurrentWorldId;
        var known=DeliveryCharacters().GroupBy(x=>x.Character.World).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Person.Id+"/"+x.Character.AccountId).Distinct().Count());
        var worlds=new List<(ushort Id,int Index,string Name,int Priority)>();
        foreach(var w in available){var name=w.NameString;var menuIndex=CurrentLoginIdentification.WorldMenuIndex(menuNames,name);if(menuIndex>=0)worlds.Add((w.Id,menuIndex,name,name==targetWorld?1000:w.Id==preferred?500:known.GetValueOrDefault(name)*10));}
        loginWorldChecked.Add(lobby->WorldId);
        var next=worlds.Where(w=>!loginWorldChecked.Contains(w.Id)).OrderByDescending(w=>w.Priority).ThenBy(w=>w.Index).FirstOrDefault();
        if(next.Id==0)return false;
        LoginCallback(addon,25,0,next.Index);loginWorldPending=next.Id;loginWorldSentAt=now;loginWorldChecked.Add(next.Id);loginWorldNext=now.AddMilliseconds(500);
        characterTravelStatus="Checking "+next.Name+" on the selected service account…";return true;
    }
    private void CancelAssignedLogin(string reason)
    {
        if(assignedLogin==null)return;assignedLogin=null;assignedLoginError=reason;characterSwitchTarget=null;characterSwitchUntil=default;
        characterTravelStatus=reason;
        if(bringTrip is not null)EndBring("Bring character stopped: "+reason);
    }
    private unsafe bool RedirectAssignedVisitingCharacter(AssignedLogin run,AgentLobby* lobby)
    {
        var displayedDc=TravelDc(lobby->WorldId);
        if(!CharacterLoginWorldPolicy.DisplayedListReady(lobby->WorldId,displayedDc,lobby->TemporaryLocked,SellerAddon("_CharaSelectListMenu")!=null)){
            characterTravelStatus=$"Waiting for the character world list to load for {run.Target.Name} @ {run.World}.";
            return true;
        }
        if(assignedVisitingReturnFromDc!=0){
            if(displayedDc==assignedVisitingReturnFromDc)return true;
            assignedVisitingReturnFromDc=0;
        }
        if(lobby->TemporaryLocked||SellerAddon("_CharaSelectListMenu")==null)return false;
        if(lobby->LobbyData.CharaSelectEntries.LongCount is <=0 or >512)return false;
        CharaSelectCharacterEntry* match=null;var count=0;
        foreach(var item in lobby->LobbyData.CharaSelectEntries){
            var entry=item.Value;
            if(entry!=null&&entry->ContentId.ToString()==run.Target.ContentId&&entry->NameString==run.Target.Name&&WorldName(entry->HomeWorldId)==run.Target.World){match=entry;count++;}
        }
        if(count!=1||match==null)return false;
        var visiting=WorldName(match->CurrentWorldId);var visitingDc=TravelDc(match->CurrentWorldId);
        var listWorld=CharacterLoginWorldPolicy.Resolve(run.Target.World,visiting,TravelDc(match->HomeWorldId),visitingDc,TravelRegion(match->HomeWorldId),TravelRegion(match->CurrentWorldId));
        var listDc=CharacterLoginWorldPolicy.ListDc(TravelDc(match->HomeWorldId),visitingDc,TravelRegion(match->HomeWorldId),TravelRegion(match->CurrentWorldId));
        if(visitingDc==0||displayedDc==0||string.IsNullOrEmpty(visiting))throw new InvalidOperationException("This character is visiting another data center, but its current world could not be verified. Export diagnostics or select its visiting data center manually.");
        if(listDc==displayedDc){
            if((match->LoginFlags&CharaSelectCharacterEntryLoginFlags.DCTraveling)!=0&&visitingDc==TravelDc(match->HomeWorldId))
                throw new InvalidOperationException("The game marks this character as visiting another data center, but has not supplied its visiting world. Select its visiting data center manually and retry; no home-world login was attempted.");
            if(run.World!=listWorld){assignedLogin=run with{World=listWorld};loginWorldChecked.Clear();loginWorldPending=loginWorldSample=0;loginWorldNext=default;return true;}
            return false;
        }
        if(SellerAddon("SelectYesno")!=null||SellerAddon("SelectOk")!=null||SellerAddon("ContextMenu")!=null)return true;
        if(!ClickLoginMenuButton("_CharaSelectReturn",4))return true;
        assignedLogin=run with{World=listWorld};assignedVisitingReturnFromDc=displayedDc;
        assignedDcSent=assignedServiceSent=assignedLoginSent=assignedLoginConfirmed=assignedAccountReturnSent=false;
        loginWorldChecked.Clear();loginWorldNext=default;loginWorldPending=loginWorldSample=0;
        characterTravelStatus=$"Selecting {listWorld} for {run.Target.Name} on the same service account.";
        RecordTravelPrompt("Visiting character login redirected",new{character=run.Target.ContentId,home=run.Target.World,visiting,listWorld,displayedDc,visitingDc,slot=run.ServiceAccount});
        return true;
    }
    private unsafe void UpdateAssignedLogin(DateTimeOffset now)
    {
        if(assignedLogin is not {} run||now<assignedLoginNext)return;assignedLoginNext=now.AddMilliseconds(500);
        try{
            if(config.PairingKey!=run.Pairing||now>=run.Deadline)throw new InvalidOperationException("Assigned login expired or pairing changed.");
            if(Player.IsLoaded){if(Player.ContentId==run.From){
                if(assignedLogoutConfirmed||Conditions[ConditionFlag.LoggingOut])return;
                var logout=(AddonSelectYesno*)SellerAddon("SelectYesno");
                var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(115)?.Text.ToString()??"";
                if(logout!=null&&logout->PromptText!=null&&NativeEventPolicy.Prompt(expected,TravelMenuText(logout->PromptText->NodeText.StringPtr)??"")&&NativeEventButton((AtkUnitBase*)logout,logout->YesButton))assignedLogoutConfirmed=true;
                return;
            }if(Player.ContentId.ToString()!=run.Target.ContentId)throw new InvalidOperationException("An unexpected character logged in.");assignedLogin=null;characterSwitchUntil=default;characterSwitchTarget=null;return;}
            if(assignedLoginConfirmed){characterTravelStatus=$"Login accepted for {run.Target.Name}; waiting for the queue or loading.";return;}
            if(Client.IsLoggedIn||LifestreamBusy())return;
            if(SellerAddon("SelectString")!=null){if(!assignedServiceSent)assignedServiceSent=SelectVerifiedServiceAccount(run.ServiceAccount);return;}
            if(SelectLoginDataCenter(run.World,ref assignedDcSent))return;
            if(SellerAddon("_TitleMenu")!=null){
                assignedDcSent=false;
                if(ClickLoginMenuButton("_TitleMenu",5))assignedAccountReturnSent=false;
                return;
            }
            if(SellerAddon("SelectOk")!=null)throw new InvalidOperationException("The game reported a login message. Review it before continuing.");
            var lobby=AgentLobby.Instance();if(lobby==null)return;
            if(lobby->ServiceAccountIndex!=run.ServiceAccount){
                if(lobby->TemporaryLocked)return;
                if(assignedServiceSent||assignedLoginSent)throw new InvalidOperationException("The game did not select the requested service account; no other account was used.");
                if(lobby->ServiceAccountIndex<0||SellerAddon("_CharaSelectListMenu")==null)return;
                if(SellerAddon("SelectYesno")!=null||SellerAddon("ContextMenu")!=null)return;
                if(!assignedAccountReturnSent){
                    assignedAccountReturnSent=ClickLoginMenuButton("_CharaSelectReturn",4);
                    if(assignedAccountReturnSent){
                        assignedDcSent=false;loginWorldChecked.Clear();loginWorldPending=loginWorldSample=0;loginWorldNext=default;
                        characterTravelStatus=$"Returning through Data Center to select verified service account {run.ServiceAccount+1} for {run.Target.Name}.";
                    }
                }
                return;
            }
            if(!assignedLoginSent&&RedirectAssignedVisitingCharacter(run,lobby))return;
            if(assignedLoginSent){
                var target=TitleLoginTarget(run.Target.Id);
                if(target!=null&&ulong.TryParse(target.ContentId,out var cid)&&ConfirmNativeLogin(true,cid,target.World,run.ServiceAccount,target.Name))
                    {assignedLoginConfirmed=true;characterTravelStatus=$"Login confirmed for {target.Name}; waiting for the game.";}
                else if(SellerAddon("SelectYesno")!=null)
                    characterTravelStatus=$"Waiting for the displayed character to match {run.Target.Name} on service account {run.ServiceAccount+1}.";
                return;
            }
            if(lobby->TemporaryLocked||SellerAddon("_CharaSelectListMenu")==null||now<loginWorldNext)return;
            if(WorldName(lobby->WorldId)==run.World&&TryReadReadyLoginList(out var entries)&&entries.FirstOrDefault(e=>!e.Locked&&e.ContentId==run.Target.ContentId&&e.World==run.Target.World) is {} verified){
                if(TryPrepareBringBeforeLogin(run))return;
                assignedLoginSent=BeginNativeCharacterLogin(run.Target,run.ServiceAccount);return;
            }
            if(!SelectNextKnownLoginWorld(run.Target.ContentId,run.World,now))throw new InvalidOperationException("No matching character found on the selected account and data center. No alternate account was tried.");
        }catch(Exception e){CancelAssignedLogin(e.Message);}
    }
}
