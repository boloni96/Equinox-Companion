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
        squareEnixLogin.Relationship(LoginPairingScope(), character.GameAccountKey, config.SquareEnixLoginGroups,
            from.PersonId, from.AccountId, to.PersonId, to.AccountId);

    private bool CharacterLoginNeedsConfirmation(SharedCharacter character,
        (string PersonId, string PersonName, string AccountId, string AccountName) from,
        (string PersonId, string PersonName, string AccountId, string AccountName) to) =>
        LoginRelationship(character, from, to) == CharacterLoginRelationship.Unverified;

    private unsafe (string PersonId, string PersonName, string AccountId, string AccountName) CurrentCharacterSwitchScope(CharacterLoginMode mode, bool refresh = false)
    {
        if (Player.IsLoaded) return CharacterSwitchScope(CurrentSharedCharacter()?.Id ?? "");
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
        var scope = characterLoginAccount.Resolve(roster, config.PairingKey, lobby->ServiceAccountIndex, contentIds);
        if (scope is not {} known) return CharacterSwitchScope("");
        if (contentIds is not null)
        {
            var ids = contentIds.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
            var keys = roster.People.SelectMany(p => p.Characters).Where(c => ids.Contains(c.ContentId))
                .Select(c => c.GameAccountKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().Take(2).ToArray();
            if (keys.Length == 1) ObserveSquareEnixLogin(keys[0]);
        }
        var person = roster.People.FirstOrDefault(p => p.Id == known.Person);
        var account = person?.Characters.FirstOrDefault(c => c.AccountId == known.Account);
        return characterAccountScope = person is not null && account is not null
            ? (person.Id, person.Name, account.AccountId, account.Account)
            : CharacterSwitchScope("");
    }
}
