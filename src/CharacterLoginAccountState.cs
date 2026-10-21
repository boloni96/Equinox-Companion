namespace EquinoxCompanion;

// Evidence is local to this game process, never persisted with the configuration.
// A service-account index alone cannot identify an account on another game login.
internal sealed class CharacterLoginAccountState
{
    private string pairing = "";
    private int serviceAccount = -1;
    private ulong[] verifiedCharacters = [];
    private ulong[] titleCharacters = [];

    private void UsePairing(string pairingKey)
    {
        if (pairingKey == pairing) return;
        pairing = pairingKey;
        serviceAccount = -1;
        verifiedCharacters = [];
        titleCharacters = [];
    }

    public void Remember(string pairingKey, int index, ulong contentId)
    {
        UsePairing(pairingKey);
        if (pairingKey.Length != 64 || contentId == 0) return;
        if (serviceAccount == index && verifiedCharacters.Length == 1 && verifiedCharacters[0] == contentId
            && titleCharacters.Length == 1 && titleCharacters[0] == contentId) return;
        serviceAccount = index;
        titleCharacters = verifiedCharacters = [contentId];
    }

    // The title menu may reset the lobby index. It does not change the login of
    // this running client. Keep actual character evidence until another ready
    // list/character replaces it; never restore it from a previous game process.
    public (string Person, string Account)? ResolveTitle(SharedRoster roster, string pairingKey)
    {
        UsePairing(pairingKey);
        return pairingKey.Length == 64 ? Match(roster, titleCharacters).Scope : null;
    }

    public (string Person, string Account)? Resolve(SharedRoster roster, string pairingKey, int index, ulong[]? loadedCharacters = null)
    {
        UsePairing(pairingKey);
        if (index != serviceAccount)
        {
            verifiedCharacters = [];
            serviceAccount = index;
        }
        if (pairingKey.Length != 64 || index < 0) return null;

        // Only pass the current, ready character-selection list. Never use stale
        // lobby entries from the title menu or a loading screen as fresh proof.
        if (loadedCharacters is not null)
        {
            var observed = Match(roster, loadedCharacters);
            if (observed.Known)
            {
                // Conflicting or incomplete mappings invalidate earlier evidence.
                titleCharacters = verifiedCharacters = observed.Scope is null ? [] : loadedCharacters.ToArray();
                return observed.Scope;
            }
            if (loadedCharacters.Any(id => id != 0))
            {
                // A ready, entirely unknown list is not the previous account.
                titleCharacters = verifiedCharacters = [];
                return null;
            }
        }
        return Match(roster, verifiedCharacters).Scope;
    }

    private static (bool Known, (string Person, string Account)? Scope) Match(SharedRoster roster, ulong[] contentIds)
    {
        var ids = contentIds.Where(id => id != 0).Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
        if (ids.Count == 0) return (false, null);
        var matches = roster.People.SelectMany(p => p.Characters
            .Where(c => ids.Contains(c.ContentId))
            .Select(c => (Person: p.Id, Account: c.AccountId))).Distinct().Take(2).ToArray();
        if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].Person) || string.IsNullOrWhiteSpace(matches[0].Account))
            return (matches.Length != 0, null);
        return (true, matches[0]);
    }
}
