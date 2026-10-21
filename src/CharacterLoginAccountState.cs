namespace EquinoxCompanion;

// Evidence is local to this game process, never persisted with the configuration.
// A service-account index alone cannot identify an account on another game login.
internal sealed class CharacterLoginAccountState
{
    private string pairing = "";
    private int serviceAccount = -1;
    private ulong[] verifiedCharacters = [];

    public void Remember(string pairingKey, int index, ulong contentId)
    {
        if (pairingKey.Length != 64 || index < 0 || contentId == 0) return;
        pairing = pairingKey;
        serviceAccount = index;
        verifiedCharacters = [contentId];
    }

    public (string Person, string Account)? Resolve(SharedRoster roster, string pairingKey, int index, ulong[]? loadedCharacters = null)
    {
        if (pairingKey != pairing || index != serviceAccount)
        {
            verifiedCharacters = [];
            pairing = pairingKey;
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
                verifiedCharacters = observed.Scope is null ? [] : loadedCharacters.ToArray();
                return observed.Scope;
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
