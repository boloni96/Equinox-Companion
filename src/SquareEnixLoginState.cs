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
