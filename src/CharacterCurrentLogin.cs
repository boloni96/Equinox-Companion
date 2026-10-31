using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly Queue<int> loginUncheckedAccounts=[];
    private bool loginDiscoverAccounts,loginReturnForAccount,loginAccountReturnSent;
    private void ResetServiceAccountDiscovery(){loginUncheckedAccounts.Clear();loginDiscoverAccounts=loginReturnForAccount=loginAccountReturnSent=false;}
    private void StartCurrentLoginIdentification(SharedCharacter character)
    {
        var now = DateTimeOffset.UtcNow;
        if (titleLoginProbe.Active || now < characterSwitchUntil || followSession.Armed || shoppingTravel is not null || bringTrip is not null) return;
        var mode = ReadCharacterLoginMode(true);
        var from = CurrentCharacterSwitchScope(mode, true);
        var to = CharacterSwitchScope(character.Id);
        var action = CharacterLoginPolicy.InitialAction(mode, LoginRelationship(character, from, to), from.PersonId.Length > 0 && from.AccountId.Length > 0);
        if (!CharacterLoginPolicy.IdentifyThroughDataCenter(mode, action)
                || Player.IsLoaded || Client.IsLoggedIn || LifestreamBusy()) return;
        var target = TitleLoginTarget(character.Id);
        if (target is null || !titleLoginProbe.Begin(target, config.PairingKey, now))
        { characterTravelStatus = "A paired account and complete character identity are needed."; return; }
        titleLoginOwnsLifestream = false;
        ResetServiceAccountDiscovery();currentLoginIdentification = new();identificationDcSent=false;loginWorldChecked.Clear();loginWorldNext=default;loginWorldPending=loginWorldSample=0;
        queuedLoginConfirmation = "";
        characterSwitchFrom = 0; characterSwitchTarget = character;
        characterSwitchUntil = titleLoginProbe.Deadline; nextTitleLoginCheck = default;
        characterTravelStatusOwner = character.Id;
        try
        {
            // Open Data Center explicitly, then select the target's recorded account slot.
            if (!VisibleFollowAddon("SelectString") && (VisibleFollowAddon("TitleDCWorldMap") || VisibleFollowAddon("TitleConnect") || !ClickLoginMenuButton("_TitleMenu", 5)))
            { StopTitleLoginCheck("Wait for the main title menu, then choose Log in as this character again."); return; }
            characterTravelStatus = "Identifying this game's login…";
        }
        catch { StopTitleLoginCheck("Could not open the current character list. Choose Data Center and your service account in game, then choose your character in Companion."); }
    }

    // Dispatch the button's own registered event, as the game's normal button
    // does. Node 4 is START on _TitleMenu and Return on _CharaSelectReturn.
    // These mappings are also used by ECommons _TitleMenu and Lifestream.
    private static unsafe bool ClickLoginMenuButton(string name, uint nodeId)
    {
        var addon = (AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
        if (addon == null || !addon->IsVisible || !addon->IsReady) return false;
        var button = addon->GetComponentButtonById(nodeId);
        if (button == null || button->OwnerNode == null || !button->IsEnabled || !button->OwnerNode->AtkResNode.IsVisible()) return false;
        var evt = button->OwnerNode->AtkResNode.AtkEventManager.Event;
        if (evt == null) return false;
        var click = *evt;
        var data = new AtkEventData();
        addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, &click, &data);
        return true;
    }

    private unsafe bool TryReadCurrentLoginCharacters(out ulong[] ids)
    {
        ids = [];
        var list = (AtkUnitBase*)GardenGui.GetAddonByName("_CharaSelectListMenu").Address;
        var lobby = AgentLobby.Instance();
        if (Player.IsLoaded || Client.IsLoggedIn || list == null || !list->IsVisible || !list->IsReady
            || lobby == null || lobby->TemporaryLocked || lobby->ServiceAccountIndex < 0
            || VisibleFollowAddon("SelectString") || VisibleFollowAddon("SelectYesno") || VisibleFollowAddon("SelectOk")) return false;
        // This list covers the connected data centre, including worlds other
        // than the displayed world. Both lists are read only after the UI is ready.
        var all = lobby->LobbyData.LobbyUIClient.CurrentDataCenterCharacters;
        var visible = lobby->LobbyData.CharaSelectEntries;
        if (all.LongCount is < 0 or > 512 || visible.LongCount is < 0 or > 512) return false;
        var found = new HashSet<ulong>();
        foreach (var entry in all)
            if (entry.ContentId != 0 && entry.DeletedFlag == 0 && (entry.LoginFlags & CharaSelectCharacterEntryLoginFlags.Locked) == 0) found.Add(entry.ContentId);
        foreach (var item in visible)
        {
            var entry = item.Value;
            if (entry != null && entry->ContentId != 0 && (entry->LoginFlags & CharaSelectCharacterEntryLoginFlags.Locked) == 0) found.Add(entry->ContentId);
        }
        ids = found.Order().ToArray();
        return true;
    }

    private void UpdateCurrentLoginIdentification(LoginProbeTarget target, DateTimeOffset now)
    {
        var probe = currentLoginIdentification!;
        if (LifestreamBusy())
        { StopTitleLoginCheck("Login identification stopped because another Lifestream trip started."); return; }
        var character = config.SharedRoster!.People.SelectMany(p => p.Characters).Single(c => c.Id == target.Id);
        if (probe.ReturningToTitle)
        {
            if (ReadCharacterLoginMode(true) == CharacterLoginMode.Title)
            {
                var from = CurrentCharacterSwitchScope(CharacterLoginMode.Title, true);
                var to = CharacterSwitchScope(character.Id);
                if (LoginRelationship(character, from, to) == CharacterLoginRelationship.Unverified)
                { StopTitleLoginCheck("Account evidence changed. Choose the character again."); return; }
                StopTitleLoginCheck("Current login identified.");
                SwitchSharedCharacter(character);
                return;
            }
            if (!probe.ReturnRequested && !VisibleFollowAddon("SelectYesno") && !VisibleFollowAddon("SelectOk")
                && !VisibleFollowAddon("ContextMenu") && !VisibleFollowAddon("_CharaSelectWorldServer"))
                probe.ReturnRequested = ClickLoginMenuButton("_CharaSelectReturn", 4);
            return;
        }
        if(loginReturnForAccount){
            if(VisibleFollowAddon("_TitleMenu")){
                if(ClickLoginMenuButton("_TitleMenu",5)){loginReturnForAccount=false;identificationDcSent=false;}
            }else if(!loginAccountReturnSent&&!VisibleFollowAddon("SelectYesno")&&!VisibleFollowAddon("SelectOk"))
                loginAccountReturnSent=ClickLoginMenuButton("_CharaSelectReturn",4);
            return;
        }
        // Handle the service-account popup before the DC map underneath it.
        if (VisibleFollowAddon("SelectString"))
        {
            if (probe.ServiceAccountSelected) return;
            var index = RecordedCharacterServiceSlot(character);
            if(index<0||loginDiscoverAccounts){
                if(!loginDiscoverAccounts){
                    foreach(var slot in AvailableLoginServiceSlots())loginUncheckedAccounts.Enqueue(slot);
                    loginDiscoverAccounts=true;titleLoginProbe.AllowAccountDiscovery(now);characterSwitchUntil=titleLoginProbe.Deadline;
                }
                if(!loginUncheckedAccounts.TryDequeue(out index)){StopTitleLoginCheck("This character was not found on the available service accounts. No character login was requested.");return;}
            }
            if (SelectVerifiedServiceAccount(index))
            {
                probe.Selected(index);
                characterTravelStatus = $"Checking service account {index + 1} for {target.Name}…";
            }
            return;
        }
        if(SelectLoginDataCenter(target.World,ref identificationDcSent))return;
        var ready = TryReadCurrentLoginCharacters(out var ids);
        if (ready && !probe.MatchesSelectedAccount(CurrentLoginServiceAccount()))
        { StopTitleLoginCheck("The game selected a different service account. No character login was requested."); return; }
        if(ready&&loginDiscoverAccounts&&!CharacterLoginProbe.DiscoveryContains(target,ids,TryReadReadyLoginList(out var discoveryEntries)?discoveryEntries:[])){
            if(SelectNextKnownLoginWorld(target.ContentId,target.World,now))return;
            if(loginUncheckedAccounts.Count==0){StopTitleLoginCheck("The requested character was not found on these service accounts. No character login was requested.");return;}
            currentLoginIdentification=new();loginReturnForAccount=true;loginAccountReturnSent=false;
            loginWorldChecked.Clear();loginWorldNext=default;loginWorldPending=loginWorldSample=0;
            characterTravelStatus="Checking the next service account through Data Center…";return;
        }
        if(ready&&ids.Length==0){if(!SelectNextKnownLoginWorld(target.ContentId,target.World,now))StopTitleLoginCheck("No known characters were found on this selected account and data center.");return;}
        if (!probe.Observe(CurrentLoginServiceAccount(), ids.Select(id => id.ToString(CultureInfo.InvariantCulture)), ready, now)) return;
        foreach(var id in ids)RememberCharacterServiceSlot(id.ToString(CultureInfo.InvariantCulture),CurrentLoginServiceAccount());
        ObserveLoginAccountList(ids);
        characterLoginAccount.Resolve(config.SharedRoster!, config.PairingKey, CurrentLoginServiceAccount(), ids);
        var source = CurrentCharacterSwitchScope(CharacterLoginMode.Title, true);
        var destination = CharacterSwitchScope(character.Id);
        if (LoginRelationship(character, source, destination) == CharacterLoginRelationship.Unverified)
        {
            StopTitleLoginCheck(source.PersonId.Length > 0
                ? $"Current login: {source.PersonName} / {source.AccountName}. The selected account needs confirmation."
                : "The current list does not identify a paired account. Confirm the selected account before continuing.");
            queuedLoginConfirmation = character.Id;
            return;
        }
        if (TryReadReadyLoginList(out var entries) && entries.Count(e => !e.Locked && CharacterLoginProbe.ExactCharacter(target, e)) == 1)
        {
            StopTitleLoginCheck("Current login identified.");
            SwitchSharedCharacter(character);
        }
        else
        {
            // The current native account identifies the selected character's
            // whole account or a learned service-account link. Only now may
            // Lifestream navigate to that verified target from the title screen.
            if(!SelectNextKnownLoginWorld(target.ContentId,target.World,now))StopTitleLoginCheck("Account identified, but the target character was not found on this data center. Check its visiting world.");
        }
    }
}

