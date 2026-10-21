using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly CharacterLoginAccountState characterLoginAccount = new();
    private readonly SquareEnixLoginState squareEnixLogin = new();
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
        RememberLoadedLoginAccount();
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
        (string PersonId, string PersonName, string AccountId, string AccountName) to) =>
        squareEnixLogin.Relationship(LoginPairingScope(), SquareEnixLoginState.TargetAccountKey(config.SharedRoster, character, to.PersonId, to.AccountId), config.SquareEnixLoginGroups,
            from.PersonId, from.AccountId, to.PersonId, to.AccountId);

    private bool CharacterLoginNeedsConfirmation(SharedCharacter character,
        (string PersonId, string PersonName, string AccountId, string AccountName) from,
        (string PersonId, string PersonName, string AccountId, string AccountName) to) =>
        LoginRelationship(character, from, to) == CharacterLoginRelationship.Unverified;

    private unsafe (string PersonId, string PersonName, string AccountId, string AccountName) CurrentCharacterSwitchScope(CharacterLoginMode mode, bool refresh = false)
    {
        if (Player.IsLoaded)
        {
            RememberLoadedLoginAccount();
            return CharacterSwitchScope(CurrentSharedCharacter()?.Id ?? "");
        }
        if (mode is not (CharacterLoginMode.Title or CharacterLoginMode.CharacterSelect) || config.SharedRoster is not {} roster)
            return CharacterSwitchScope("");
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
        }
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
