namespace EquinoxCompanion;

// A local character observation links its whole Person/account. Store the CID
// witness so roster moves/removals cannot silently preserve an obsolete link.
public sealed record LoginAccountWitness(string PersonId, string AccountId, string ContentId);
public sealed record LoginAccountGroup(string PairingScope, LoginAccountWitness[] Accounts);

internal sealed class LoginAccountLinks
{
    private string pairing = "";
    private readonly List<LoginAccountWitness> session = [];
    private static bool ValidPairing(string scope) => scope.Length == 64 && scope.All(char.IsAsciiHexDigit);
    private static bool SameAccount(LoginAccountWitness a, LoginAccountWitness b) => a.PersonId == b.PersonId && a.AccountId == b.AccountId;

    private void UsePairing(string scope)
    {
        if (pairing == scope) return;
        pairing = scope;
        session.Clear();
    }

    private static LoginAccountWitness? Find(SharedRoster roster, string contentId)
    {
        if (!ulong.TryParse(contentId, out var id) || id == 0) return null;
        var matches = roster.People.SelectMany(p => p.Characters.Where(c => c.ContentId == contentId)
            .Select(c => new LoginAccountWitness(p.Id, c.AccountId, c.ContentId))).Take(2).ToArray();
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].PersonId) && !string.IsNullOrWhiteSpace(matches[0].AccountId)
            ? matches[0] : null;
    }

    private static bool ValidWitness(SharedRoster roster, LoginAccountWitness? witness) =>
        witness is not null && Find(roster, witness.ContentId) is {} current && current == witness;

    // Call only with a loaded local character or a current ready lobby list.
    // Clicking a confirmation, saved labels, native indices and paired remote
    // observations are never sufficient to create a link.
    public bool Observe(string scope, SharedRoster roster, string contentId, List<LoginAccountGroup> groups)
    {
        UsePairing(scope);
        if (!ValidPairing(scope)) return false;
        session.RemoveAll(w => !ValidWitness(roster, w));
        var observed = Find(roster, contentId);
        if (observed is null || session.Any(w => SameAccount(w, observed))) return false;
        session.Add(observed);
        if (session.Count < 2) return false;

        var joined = session.ToList();
        var matching = new HashSet<LoginAccountGroup>();
        bool added;
        do
        {
            added = false;
            foreach (var group in groups)
            {
                if (group.PairingScope != scope || group.Accounts is null || matching.Contains(group)) continue;
                var valid = group.Accounts.Where(w => w is not null && ValidWitness(roster, w)).ToArray();
                if (!valid.Any(w => joined.Any(j => SameAccount(j, w)))) continue;
                matching.Add(group);
                foreach (var witness in valid)
                    if (!joined.Any(j => SameAccount(j, witness))) joined.Add(witness);
                added = true;
            }
        } while (added);
        if (matching.Count == 1 && matching.First().Accounts.Length == joined.Count
            && matching.First().Accounts.All(w => ValidWitness(roster, w) && joined.Any(j => SameAccount(j, w)))) return false;
        groups.RemoveAll(matching.Contains);
        groups.Add(new(scope, joined.OrderBy(w => w.PersonId, StringComparer.Ordinal).ThenBy(w => w.AccountId, StringComparer.Ordinal).ToArray()));
        return true;
    }

    public bool Recognizes(string scope, SharedRoster? roster, SharedCharacter target, string personId, string accountId,
        IEnumerable<LoginAccountGroup> groups)
    {
        UsePairing(scope);
        if (!ValidPairing(scope) || roster is null || string.IsNullOrWhiteSpace(personId) || string.IsNullOrWhiteSpace(accountId)) return false;
        // Revalidate the actual target against the current roster, including a
        // stale UI row whose character has moved to a different account.
        var people = roster.People.Where(p => p.Id == personId).Take(2).ToArray();
        if (people.Length != 1 || target.AccountId != accountId) return false;
        var members = people[0].Characters.Where(c => c.Id == target.Id).Take(2).ToArray();
        if (members.Length != 1 || members[0].AccountId != accountId) return false;
        var active = session.Where(w => ValidWitness(roster, w)).ToArray();
        if (active.Length == 0) return false; // Saved groups cannot identify a new process.
        bool IsTarget(LoginAccountWitness w) => w.PersonId == personId && w.AccountId == accountId;
        if (active.Any(IsTarget)) return true;
        return groups.Any(g => g.PairingScope == scope && g.Accounts is not null
            && g.Accounts.Any(w => w is not null && IsTarget(w) && ValidWitness(roster, w))
            && g.Accounts.Any(w => w is not null && ValidWitness(roster, w) && active.Any(a => SameAccount(a, w))));
    }
}
