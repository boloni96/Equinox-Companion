namespace EquinoxCompanion;

// Journal-scoped fingerprints only. A group records service accounts actually
// observed in one running game client, whose Square Enix login stays unchanged.
public sealed record SquareEnixLoginGroup(string PairingScope, string[] ServiceAccountKeys);
internal enum CharacterLoginRelationship { SameServiceAccount, SameSquareEnixLogin, Unverified }

internal sealed class SquareEnixLoginState
{
    private string pairingScope = "";
    private readonly HashSet<string> sessionAccounts = [];

    private static bool Valid(string? key) => key is { Length: 64 } && key.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static string AccountKey(SharedRoster? roster, string personId, string accountId)
    {
        if (roster is null || string.IsNullOrWhiteSpace(personId) || string.IsNullOrWhiteSpace(accountId)) return "";
        var people = roster.People.Where(p => p.Id == personId).Take(2).ToArray();
        if (people.Length != 1) return "";
        // One verified character identifies its service account. Other characters
        // in that exact Person/account may use the link without individual logins.
        // Conflicting observed identities must not be propagated to unknown ones.
        var keys = people[0].Characters.Where(c => c.AccountId == accountId)
            .Select(c => c.GameAccountKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().Take(2).ToArray();
        return keys.Length == 1 && Valid(keys[0]) ? keys[0] : "";
    }

    public static string TargetAccountKey(SharedRoster? roster, SharedCharacter character, string personId, string accountId)
    {
        if (!string.IsNullOrEmpty(character.GameAccountKey)) return Valid(character.GameAccountKey) ? character.GameAccountKey : "";
        if (roster is null || character.AccountId != accountId) return "";
        // Check current membership, so a moved/removed character cannot inherit
        // the account it used to belong to from a stale UI selection.
        var members = roster.People.Where(p => p.Id == personId).SelectMany(p => p.Characters)
            .Where(c => c.Id == character.Id).Take(2).ToArray();
        if (members.Length != 1 || members[0].AccountId != accountId) return "";
        if (!string.IsNullOrEmpty(members[0].GameAccountKey)) return Valid(members[0].GameAccountKey) ? members[0].GameAccountKey : "";
        return AccountKey(roster, personId, accountId);
    }

    private void UsePairing(string scope)
    {
        if (scope == pairingScope) return;
        pairingScope = scope;
        sessionAccounts.Clear();
    }

    public bool Observe(string scope, string serviceAccountKey, List<SquareEnixLoginGroup> groups)
    {
        UsePairing(scope);
        if (!Valid(scope) || !Valid(serviceAccountKey) || !sessionAccounts.Add(serviceAccountKey)) return false;
        if (sessionAccounts.Count < 2) return false;

        // Merge only with an observed account. A second client or a matching
        // service-account number is never evidence of a shared Square Enix login.
        var joined = new HashSet<string>(sessionAccounts);
        var matching = new HashSet<SquareEnixLoginGroup>();
        bool changed;
        do
        {
            changed = false;
            foreach (var group in groups)
            {
                if (group.PairingScope != scope || matching.Contains(group) || group.ServiceAccountKeys is null || !group.ServiceAccountKeys.Any(joined.Contains)) continue;
                matching.Add(group);
                joined.UnionWith(group.ServiceAccountKeys.Where(Valid));
                changed = true;
            }
        } while (changed);
        if (matching.Count == 1 && joined.SetEquals(matching.First().ServiceAccountKeys)) return false;
        groups.RemoveAll(matching.Contains);
        groups.Add(new(scope, joined.Order(StringComparer.Ordinal).ToArray()));
        return true;
    }

    public bool Recognizes(string scope, string targetServiceAccountKey, IEnumerable<SquareEnixLoginGroup> groups)
    {
        UsePairing(scope);
        if (!Valid(scope) || !Valid(targetServiceAccountKey) || sessionAccounts.Count == 0) return false;
        return sessionAccounts.Contains(targetServiceAccountKey) || groups.Any(g => g.PairingScope == scope
            && g.ServiceAccountKeys is not null && g.ServiceAccountKeys.Contains(targetServiceAccountKey)
            && g.ServiceAccountKeys.Any(sessionAccounts.Contains));
    }

    public CharacterLoginRelationship Relationship(string scope, string targetServiceAccountKey, IEnumerable<SquareEnixLoginGroup> groups,
        string currentPerson, string currentAccount, string targetPerson, string targetAccount)
    {
        UsePairing(scope);
        if (!CharacterTravelPolicy.NeedsConfirmation(currentPerson, currentAccount, targetPerson, targetAccount)) return CharacterLoginRelationship.SameServiceAccount;
        return Recognizes(scope, targetServiceAccountKey, groups) ? CharacterLoginRelationship.SameSquareEnixLogin : CharacterLoginRelationship.Unverified;
    }
}
