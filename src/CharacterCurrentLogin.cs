using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private void StartCurrentLoginIdentification(SharedCharacter character)
    {
        var now = DateTimeOffset.UtcNow;
        if (titleLoginProbe.Active || now < characterSwitchUntil || followSession.Armed || shoppingTravel is not null || bringTrip is not null) return;
        var mode = ReadCharacterLoginMode(true);
        var from = CurrentCharacterSwitchScope(mode, true);
        var to = CharacterSwitchScope(character.Id);
        if (CharacterLoginPolicy.InitialAction(mode, LoginRelationship(character, from, to), from.PersonId.Length > 0 && from.AccountId.Length > 0)
                != CharacterLoginAction.IdentifyLogin || Player.IsLoaded || Client.IsLoggedIn || LifestreamBusy()) return;
        var target = TitleLoginTarget(character.Id);
        if (target is null || !titleLoginProbe.Begin(target, config.PairingKey, now))
        { characterTravelStatus = "A paired account and complete character identity are needed."; return; }
        titleLoginOwnsLifestream = false;
        currentLoginIdentification = new();
        queuedLoginConfirmation = "";
        characterSwitchFrom = 0; characterSwitchTarget = character;
        characterSwitchUntil = titleLoginProbe.Deadline; nextTitleLoginCheck = default;
        characterTravelStatusOwner = character.Id;
        try
        {
            // Open the current login's normal START path. Do not send the clicked
            // character, world, Person or account to Lifestream for identification.
            if (VisibleFollowAddon("TitleDCWorldMap") || VisibleFollowAddon("TitleConnect") || !ClickLoginMenuButton("_TitleMenu", 4))
            { StopTitleLoginCheck("Wait for the main title menu, then choose Log in as this character again."); return; }
            characterTravelStatus = "Identifying this game's login…";
        }
        catch { StopTitleLoginCheck("Could not open the current character list. Use START in game, then choose your character in Companion."); }
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

    private unsafe bool SelectFirstLoginServiceAccount()
    {
        // Inspect one service account actually offered by this authenticated
        // game. Index zero is only a menu choice, never account identity proof.
        var menu = (AddonSelectString*)GardenGui.GetAddonByName("SelectString").Address;
        if (menu == null || !menu->IsVisible || !menu->IsReady) return false;
        var text = menu->GetTextNodeById(2);
        var expected = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Lobby>().GetRowOrDefault(11)?.Text.ToString();
        if (text == null || string.IsNullOrWhiteSpace(expected) || TravelMenuText(text->NodeText.StringPtr) != expected.Trim()) return false;
        var list = menu->PopupMenu.PopupMenu.List;
        if (list == null || list->ListLength is < 1 or > 16 || !list->IsItemInteractionEnabled || list->GetItemDisabledState(0)) return false;
        list->DispatchItemEvent(0, AtkEventType.ListItemClick);
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
        return ids.Length > 0;
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
        if (!probe.ServiceAccountSelected && SelectFirstLoginServiceAccount())
        { probe.ServiceAccountSelected = true; return; }
        var ready = TryReadCurrentLoginCharacters(out var ids);
        if (!probe.Observe(CurrentLoginServiceAccount(), ids.Select(id => id.ToString(CultureInfo.InvariantCulture)), ready, now)) return;
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
            probe.ReturnToTitle();
            characterTravelStatus = "Login identified · opening the selected character…";
        }
    }
}
