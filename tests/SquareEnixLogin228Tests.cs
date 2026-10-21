using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class SquareEnixLogin228Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Square Enix login: " + why); }
        var pairing = new string('a', 64);
        var otherPairing = new string('b', 64);
        var main = new string('1', 64);
        var alt = new string('2', 64);
        var goddess = new string('3', 64);
        var third = new string('4', 64);
        var groups = new List<SquareEnixLoginGroup>();
        var client = new SquareEnixLoginState();
        Check(!client.Recognizes(pairing, main, groups), "a fresh game process has no active login evidence");
        Check(!client.Observe(pairing, main, groups) && groups.Count == 0, "one service account anchors the session without creating a group");
        Check(client.Recognizes(pairing, main, groups), "the service account observed in this client is recognized");
        Check(!client.Recognizes(pairing, alt, groups), "an unobserved account is not assumed to share a login");
        Check(client.Observe(pairing, alt, groups) && groups.Count == 1, "two service accounts observed in one client form a login group");
        Check(groups[0].ServiceAccountKeys.Length == 2 && groups[0].ServiceAccountKeys.Contains(main) && groups[0].ServiceAccountKeys.Contains(alt), "the group retains both verified fingerprints");
        Check(client.Relationship(pairing, alt, groups, "sir", "main", "sir", "alt") == CharacterLoginRelationship.SameSquareEnixLogin, "a service-account change under one login skips confirmation");
        Check(client.Relationship(pairing, main, groups, "sir", "main", "sir", "main") == CharacterLoginRelationship.SameServiceAccount, "same-account behavior remains direct");
        Check(client.Relationship(pairing, goddess, groups, "sir", "main", "goddess", "main") == CharacterLoginRelationship.Unverified, "another Person's Main label is not proof of the same login");
        Check(client.Relationship(pairing, goddess, groups, "sir", "main", "sir", "email-two") == CharacterLoginRelationship.Unverified, "different email accounts owned by one Person still need verification");
        Check(!client.Observe(pairing, alt, groups) && groups.Count == 1, "repeated observations do not rewrite saved groups");
        var saved = System.Text.Json.JsonSerializer.Deserialize<List<SquareEnixLoginGroup>>(System.Text.Json.JsonSerializer.Serialize(groups))!;
        var nextSession = new SquareEnixLoginState();
        Check(!nextSession.Recognizes(pairing, alt, saved), "saved groups alone do not identify the current game login");
        nextSession.Observe(pairing, main, saved);
        Check(nextSession.Recognizes(pairing, alt, saved), "a fresh verified anchor restores learned same-login service accounts after restart");
        var secondClient = new SquareEnixLoginState();
        secondClient.Observe(pairing, goddess, saved);
        Check(!secondClient.Recognizes(pairing, main, saved) && !secondClient.Recognizes(pairing, alt, saved), "a second client signed into another login does not inherit the first client's identity");
        Check(!client.Recognizes(otherPairing, alt, saved), "changing pairing clears session evidence");
        Check(!client.Recognizes(pairing, alt, saved), "returning to an old pairing needs a fresh active anchor");
        client.Observe(pairing, main, saved);
        Check(client.Observe(pairing, third, saved) && saved.Count == 1 && saved[0].ServiceAccountKeys.Length == 3, "a newly observed service account extends the existing verified group");
        Check(!client.Observe(pairing, third, saved), "an unchanged three-account group does not cause repeated saves");
        var bridgeGroups = new List<SquareEnixLoginGroup> { new(pairing, [main, alt]), new(pairing, [goddess, third]) };
        var bridge = new SquareEnixLoginState();
        bridge.Observe(pairing, alt, bridgeGroups);
        Check(bridge.Observe(pairing, goddess, bridgeGroups) && bridgeGroups.Count == 1 && bridgeGroups[0].ServiceAccountKeys.Length == 4, "two groups merge only when a game client actually observes an account from each");
        var invalid = new SquareEnixLoginState();
        Check(!invalid.Observe("", main, groups) && !invalid.Recognizes("", main, groups), "invalid pairing evidence cannot identify a login");
        Check(!invalid.Observe(pairing, "service account 1", groups) && !invalid.Recognizes(pairing, "service account 1", groups), "a service-account number or label is not an identity fingerprint");
        Check(!invalid.Observe(pairing, new string('z', 64), groups), "malformed fingerprints are ignored");
        var unrelated = new List<SquareEnixLoginGroup> { new(otherPairing, [main, goddess]) };
        invalid.Observe(pairing, main, unrelated);
        Check(!invalid.Recognizes(pairing, goddess, unrelated), "groups from another pairing cannot connect logins");
        Console.WriteLine($"Square Enix login regression checks passed: {checks}.");
    }
}
