using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly CharacterLoginProbe titleLoginProbe = new();
    private DateTimeOffset nextTitleLoginCheck;
    private bool titleLoginOwnsLifestream;
    private CurrentLoginIdentification? currentLoginIdentification;
    private string queuedLoginConfirmation = "";

    private LoginProbeTarget? TitleLoginTarget(string id)
    {
        var matches = (config.SharedRoster?.People ?? []).SelectMany(p => p.Characters
            .Where(c => c.Id == id).Select(c => (Person: p, Character: c))).Take(2).ToArray();
        if (matches.Length != 1) return null;
        var (person, character) = matches[0];
        var observed = config.Discoveries.LastOrDefault(e => e.Actor.ContentId == character.ContentId)?.Actor;
        return new(character.Id, person.Id, character.AccountId, character.ContentId,
            observed?.Name ?? character.Name,
            string.IsNullOrWhiteSpace(observed?.HomeWorldName) ? character.World : observed.HomeWorldName);
    }

    private void StartTitleLoginCheck(SharedCharacter character)
    {
        if (titleLoginProbe.Active || DateTimeOffset.UtcNow < characterSwitchUntil) return;
        var mode = ReadCharacterLoginMode(true);
        var from = CurrentCharacterSwitchScope(mode, true);
        var to = CharacterSwitchScope(character.Id);
        if (mode != CharacterLoginMode.Title || CharacterLoginPolicy.Action(mode, LoginRelationship(character, from, to),
                true, CharacterSwitchConsentCurrent(character, from, to)) != CharacterLoginAction.CheckList)
        { characterTravelStatus = "Confirm the current login before checking another account. No Lifestream request was sent."; return; }
        var target = TitleLoginTarget(character.Id);
        if (target is null) { characterTravelStatus = "This character is no longer in the paired roster. Choose it again."; return; }
        if (followSession.Armed || shoppingTravel is not null || bringTrip is not null)
        { characterTravelStatus = "Finish or stop the current trip before logging in."; return; }
        try
        {
            if (Player.IsLoaded || Client.IsLoggedIn || LifestreamBusy()
                || !Pi.GetIpcSubscriber<bool>("Lifestream.CanAutoLogin").InvokeFunc())
            { characterTravelStatus = "Wait for the title screen and the current trip to finish."; return; }
            if (!titleLoginProbe.Begin(target, config.PairingKey, DateTimeOffset.UtcNow))
            { characterTravelStatus = "A paired account and complete in-game character name and home world are needed."; return; }
            currentLoginIdentification = null;
            titleLoginOwnsLifestream = true;
            // This IPC only connects and opens the list. It does not select a
            // character for login, accept a login dialog, or change launcher login.
            if (!Pi.GetIpcSubscriber<string, string, bool>("Lifestream.ConnectAndOpenCharaSelect").InvokeFunc(target.Name, target.World))
            { titleLoginProbe.Cancel(); titleLoginOwnsLifestream = false; characterTravelStatus = "Lifestream could not open the character list. Wait and try again."; return; }
            characterSwitchFrom = 0; characterSwitchTarget = character;
            nextTitleLoginCheck = default;
            characterSwitchUntil = titleLoginProbe.Deadline;
            characterTravelStatusOwner = character.Id;
            characterTravelStatus = "Checking this login for " + target.Name + "…";
        }
        catch
        {
            StopTitleLoginCheck("Character-list checking is unavailable. Enable or update Lifestream and try again.");
        }
    }

    private unsafe bool TryReadReadyLoginList(out LoginProbeEntry[] entries)
    {
        entries = [];
        if (Player.IsLoaded || Client.IsLoggedIn
            || !Pi.GetIpcSubscriber<bool>("Lifestream.CanInitiateTravelFromCharaSelectList").InvokeFunc()) return false;
        var lobby = AgentLobby.Instance();
        if (lobby == null || lobby->TemporaryLocked || lobby->ServiceAccountIndex < 0) return false;
        var native = lobby->LobbyData.CharaSelectEntries;
        if (native.LongCount is <= 0 or > 512) return false;
        var result = new List<LoginProbeEntry>();
        foreach (var item in native)
        {
            var entry = item.Value;
            if (entry == null || entry->ContentId == 0) continue;
            result.Add(new(entry->ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry->NameString, WorldName(entry->HomeWorldId) ?? "",
                (entry->LoginFlags & CharaSelectCharacterEntryLoginFlags.Locked) != 0));
        }
        entries = result.ToArray();
        return entries.Length > 0;
    }

    private void StopTitleLoginCheck(string message)
    {
        var active = titleLoginProbe.Active;
        var owned = active && titleLoginOwnsLifestream;
        titleLoginProbe.Cancel();
        titleLoginOwnsLifestream = false;
        currentLoginIdentification = null;
        if (active)
        {
            if (owned) try { Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction(); } catch { }
            characterSwitchUntil = default; characterSwitchTarget = null;
        }
        characterTravelStatus = message;
    }

    private void UpdateTitleLoginCheck(DateTimeOffset now)
    {
        if (titleLoginProbe.Target is not {} target) return;
        if (now < nextTitleLoginCheck && now < titleLoginProbe.Deadline) return;
        nextTitleLoginCheck = now.AddMilliseconds(250);
        try
        {
            var current = TitleLoginTarget(target.Id);
            var result = titleLoginProbe.CheckState(now, config.PairingKey, current, Client.IsLoggedIn || Player.IsLoaded);
            LoginProbeEntry[] entries = [];
            if (result == LoginProbeResult.Waiting)
            {
                if (currentLoginIdentification is not null)
                {
                    UpdateCurrentLoginIdentification(target, now);
                    return;
                }
                var busy = LifestreamBusy();
                var ready = TryReadReadyLoginList(out entries);
                result = titleLoginProbe.Inspect(now, config.PairingKey, current,
                    Client.IsLoggedIn || Player.IsLoaded, busy, ready, entries);
            }
            if (result == LoginProbeResult.Waiting) return;
            if (result != LoginProbeResult.Verified)
            {
                StopTitleLoginCheck(result switch
                {
                    LoginProbeResult.Cancelled => "Login check stopped because the character, pairing or game state changed.",
                    LoginProbeResult.NotFound => "This character is not available in the current list. Check Lifestream’s service-account selection, or launch the character’s Square Enix login.",
                    _ => "Could not verify this character within 45 seconds. Check the game’s connection message and Lifestream’s service-account selection, then try again."
                });
                return;
            }
            var character = config.SharedRoster!.People.SelectMany(p => p.Characters).Single(c => c.Id == target.Id);
            // Observe only current unlocked characters; this also restores the
            // already learned Main/Alt links for the rest of this game process.
            ObserveLoginAccountList(entries.Where(e => !e.Locked).Select(e => ulong.Parse(e.ContentId)).ToArray());
            var from = CurrentCharacterSwitchScope(CharacterLoginMode.CharacterSelect, true);
            var to = CharacterSwitchScope(character.Id);
            titleLoginProbe.Cancel(); titleLoginOwnsLifestream = false; characterSwitchUntil = default; characterSwitchTarget = null;
            characterSwitchConfirmFrom = 0; characterSwitchConfirmServiceAccount = CurrentLoginServiceAccount();
            characterSwitchConfirmKey = config.PairingKey; characterSwitchConfirmTarget = character.Id;
            characterSwitchConfirmScope = SwitchScopeKey(from) + ">" + SwitchScopeKey(to);
            try { SwitchSharedCharacter(character, true); }
            finally { characterSwitchConfirmTarget = ""; }
        }
        catch
        {
            StopTitleLoginCheck("The character-list check stopped. Wait for the list, then try again.");
        }
    }
}

