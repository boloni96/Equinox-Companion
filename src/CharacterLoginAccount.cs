using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly CharacterLoginAccountState characterLoginAccount = new();
    private readonly SquareEnixLoginState squareEnixLogin = new();
    private readonly LoginAccountLinks loginAccountLinks = new();
    private SharedRoster? loginWitnessRoster;
    private ulong loginWitnessCharacter;
    private string loginWitnessPairing = "";
    private SharedRoster? loginResolvedRoster;
    private string loginResolvedPairing = "";
    private ulong[] loginLobbyCharacters = [];
    private SharedRoster? loginLobbyRoster;
    private string loginLobbyPairing = "";

    private void RefreshLoginAccountWitnesses()
    {
        var roster = config.SharedRoster;
        var scope = LoginPairingScope();
        if (ReferenceEquals(roster, loginResolvedRoster) && scope == loginResolvedPairing) return;
        loginResolvedRoster = roster; loginResolvedPairing = scope;
        if (loginAccountLinks.Refresh(scope, roster, config.LoginAccountGroups)) SaveConfiguration();
    }

    private void ObserveLoginAccountWitness(ulong contentId)
    {
        var roster = config.SharedRoster;
        if (contentId == 0) return;
        var scope = LoginPairingScope();
        if (contentId == loginWitnessCharacter && ReferenceEquals(roster, loginWitnessRoster) && scope == loginWitnessPairing) return;
        loginWitnessCharacter = contentId; loginWitnessRoster = roster; loginWitnessPairing = scope;
        if (loginAccountLinks.Observe(scope, roster, contentId.ToString(System.Globalization.CultureInfo.InvariantCulture), config.LoginAccountGroups))
            SaveConfiguration();
    }

    private void ObserveLoginAccountList(ulong[] contentIds)
    {
        var roster = config.SharedRoster;
        var scope = LoginPairingScope();
        if (ReferenceEquals(roster, loginLobbyRoster) && scope == loginLobbyPairing && contentIds.SequenceEqual(loginLobbyCharacters)) return;
        loginLobbyCharacters = contentIds; loginLobbyRoster = roster; loginLobbyPairing = scope;
        if (loginAccountLinks.ObserveMany(scope, roster,
            contentIds.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)), config.LoginAccountGroups)) SaveConfiguration();
    }
    private string loginPairingKey = "", loginPairingScope = "";
    private DateTimeOffset characterAccountProbeAt;
    private SharedRoster? characterAccountRoster;
    private string characterAccountPairing = "";
    private int characterAccountIndex = -1;
    private CharacterLoginMode characterAccountMode;
    private (string PersonId, string PersonName, string AccountId, string AccountName) characterAccountScope;
    private DateTimeOffset nextLoginObservation;
    private string observedLoginFingerprintInput = "", observedLoginFingerprintPairing = "", observedLoginFingerprint = "";

    private void UpdateLoginAccount(DateTimeOffset now)
    {
        if (now < nextLoginObservation) return;
        nextLoginObservation = now.AddMilliseconds(250);
        RefreshLoginAccountWitnesses();
        RememberLoadedLoginAccount();
        UpdateTitleLoginCheck(now);
        // A ready character list can identify a fresh client before the first
        // Switch button click. This path is cached and never initiates travel.
        if (!Player.IsLoaded && !Client.IsLoggedIn) CurrentCharacterSwitchScope(ReadCharacterLoginMode());
    }

    private unsafe string RememberLoadedLoginAccount()
    {
        // Capture session identity independently of profile scans, network sync,
        // window visibility and the 15-second character-progress grace period.
        var local = Objects.LocalPlayer;
        if (!Client.IsLoggedIn || !Player.IsLoaded || Player.ContentId == 0 || local is null) return "";
        var native = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)local.Address;
        if (native == null || native->ContentId != Player.ContentId) return "";
        var index = CurrentLoginServiceAccount();
        characterLoginAccount.Remember(config.PairingKey, index, native->ContentId);
        ObserveLoginAccountWitness(native->ContentId);
        if (config.PairingKey.Length != 64 || native->AccountId == 0 || index < 0) return "";
        var identity = $"equinox-account-v1:{native->AccountId}:{index}";
        if (identity != observedLoginFingerprintInput || config.PairingKey != observedLoginFingerprintPairing)
        {
            observedLoginFingerprintInput = identity;
            observedLoginFingerprintPairing = config.PairingKey;
            observedLoginFingerprint = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(config.PairingKey), System.Text.Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        }
        ObserveSquareEnixLogin(observedLoginFingerprint);
        return observedLoginFingerprint;
    }

    private static unsafe int CurrentLoginServiceAccount()
    {
        var lobby = AgentLobby.Instance();
        return lobby != null ? lobby->ServiceAccountIndex : -1;
    }

    private string LoginPairingScope()
    {
        if (loginPairingKey == config.PairingKey) return loginPairingScope;
        loginPairingKey = config.PairingKey;
        return loginPairingScope = config.PairingKey.Length == 64
            ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.PairingKey))).ToLowerInvariant() : "";
    }

    private void ObserveSquareEnixLogin(string accountKey)
    {
        if (squareEnixLogin.Observe(LoginPairingScope(), accountKey, config.SquareEnixLoginGroups)) SaveConfiguration();
    }

    private CharacterLoginRelationship LoginRelationship(SharedCharacter character,
        (string PersonId, string PersonName, string AccountId, string AccountName) from,
        (string PersonId, string PersonName, string AccountId, string AccountName) to)
    {
        var relationship = squareEnixLogin.Relationship(LoginPairingScope(), SquareEnixLoginState.TargetAccountKey(config.SharedRoster, character, to.PersonId, to.AccountId), config.SquareEnixLoginGroups,
            from.PersonId, from.AccountId, to.PersonId, to.AccountId);
        if (relationship != CharacterLoginRelationship.Unverified) return relationship;
        return loginAccountLinks.Recognizes(LoginPairingScope(), config.SharedRoster, character, to.PersonId, to.AccountId, config.LoginAccountGroups)
            ? CharacterLoginRelationship.SameSquareEnixLogin : CharacterLoginRelationship.Unverified;
    }

    private bool CharacterLoginNeedsConfirmation(SharedCharacter character,
        (string PersonId, string PersonName, string AccountId, string AccountName) from,
        (string PersonId, string PersonName, string AccountId, string AccountName) to) =>
        LoginRelationship(character, from, to) == CharacterLoginRelationship.Unverified;

    private unsafe (string PersonId, string PersonName, string AccountId, string AccountName) CurrentCharacterSwitchScope(CharacterLoginMode mode, bool refresh = false)
    {
        RefreshLoginAccountWitnesses();
        if (Player.IsLoaded)
        {
            RememberLoadedLoginAccount();
            return CharacterSwitchScope(CurrentSharedCharacter()?.Id ?? "");
        }
        if (mode is not (CharacterLoginMode.Title or CharacterLoginMode.CharacterSelect))
            return CharacterSwitchScope("");
        var roster = config.SharedRoster;
        var lobby = AgentLobby.Instance();
        if (lobby == null) return CharacterSwitchScope("");
        var now = DateTimeOffset.UtcNow;
        if (!refresh && now < characterAccountProbeAt && ReferenceEquals(roster, characterAccountRoster)
            && config.PairingKey == characterAccountPairing && lobby->ServiceAccountIndex == characterAccountIndex && mode == characterAccountMode)
            return characterAccountScope;
        characterAccountProbeAt = now.AddMilliseconds(500);
        characterAccountRoster = roster;
        characterAccountPairing = config.PairingKey;
        characterAccountIndex = lobby->ServiceAccountIndex;
        characterAccountMode = mode;
        characterAccountScope = CharacterSwitchScope("");

        ulong[]? contentIds = null;
        if (mode == CharacterLoginMode.CharacterSelect)
        {
            var entries = lobby->LobbyData.CharaSelectEntries;
            // Bound the native read, and ignore characters locked to this account.
            if (entries.LongCount is < 0 or > 512) return CharacterSwitchScope("");
            var ids = new List<ulong>();
            foreach (var entry in entries)
            {
                var character = entry.Value;
                if (character != null && character->ContentId != 0 && (character->LoginFlags & CharaSelectCharacterEntryLoginFlags.Locked) == 0)
                    ids.Add(character->ContentId);
            }
            contentIds = ids.ToArray();
            ObserveLoginAccountList(contentIds);
        }
        if (roster is null) return characterAccountScope;
        var scope = mode == CharacterLoginMode.Title
            ? characterLoginAccount.ResolveTitle(roster, config.PairingKey)
            : characterLoginAccount.Resolve(roster, config.PairingKey, lobby->ServiceAccountIndex, contentIds);
        if (scope is not {} known) return CharacterSwitchScope("");
        if (contentIds is not null)
        {
            var ids = contentIds.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
            var keys = roster.People.SelectMany(p => p.Characters).Where(c => ids.Contains(c.ContentId))
                .Select(c => c.GameAccountKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().Take(2).ToArray();
            if (keys.Length == 1) ObserveSquareEnixLogin(keys[0]);
            else if (keys.Length == 0) ObserveSquareEnixLogin(SquareEnixLoginState.AccountKey(roster, known.Person, known.Account));
        }
        var person = roster.People.FirstOrDefault(p => p.Id == known.Person);
        var account = person?.Characters.FirstOrDefault(c => c.AccountId == known.Account);
        return characterAccountScope = person is not null && account is not null
            ? (person.Id, person.Name, account.AccountId, account.Account)
            : CharacterSwitchScope("");
    }
}

