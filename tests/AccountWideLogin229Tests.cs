using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class AccountWideLogin229Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Account-wide login: " + why); }
        var mainKey = new string('1', 64);
        var altKey = new string('2', 64);
        var otherKey = new string('3', 64);
        var pairing = new string('a', 64);
        SharedCharacter Character(string id, string account, string key = "", string world = "Rafflesia") =>
            new(id, "Test Character", world, "Dynamis", "NA", account, [], AccountId: account, GameAccountKey: key, ContentId: id);
        var main = Character("101", "main", mainKey);
        var knownAlt = Character("201", "alt", altKey);
        var unvisited = Character("202", "alt");
        var anotherWorld = Character("203", "alt", world: "Siren");
        var roster = new SharedRoster(1, DateTimeOffset.UtcNow, [
            new SharedPerson("sir", "Sir", [main, knownAlt, unvisited, anotherWorld]),
            new SharedPerson("goddess", "Goddess", [Character("301", "alt", otherKey), Character("302", "alt")])]);
        string Key(SharedCharacter c, SharedRoster? source = null, string person = "sir") =>
            SquareEnixLoginState.TargetAccountKey(source ?? roster, c, person, c.AccountId);
        Check(Key(unvisited) == altKey, "one synced character identifies an unvisited sibling in the same Person/account");
        Check(Key(anotherWorld) == altKey, "world and data center do not split an account's login link");
        Check(Key(main) == mainKey, "a character's observed identity remains authoritative");
        Check(Key(roster.People[1].Characters[1], person: "goddess") == otherKey, "another Person's identically named Alt account stays separate");
        Check(Key(unvisited, person: "goddess") == "", "a character cannot borrow another Person's account identity");
        var later = Character("204", "alt");
        var expanded = roster with { People = [roster.People[0] with { Characters = [main, knownAlt, unvisited, anotherWorld, later] }, roster.People[1]] };
        Check(Key(later, expanded) == altKey, "newly added characters inherit the account link without their own login");
        var renamed = roster with { People = [roster.People[0] with { Name = "Renamed Person", Characters = [main, knownAlt with { Account = "Second service" }, unvisited with { Account = "Second service" }] }] };
        Check(Key(unvisited, renamed) == altKey, "renaming account labels does not break stable Person/account IDs");
        var mixed = roster with { People = [roster.People[0] with { Characters = [knownAlt, unvisited, Character("205", "alt", otherKey)] }] };
        Check(Key(unvisited, mixed) == "", "conflicting observed identities are not propagated to unvisited characters");
        Check(Key(knownAlt, mixed) == altKey, "a conflict does not discard an individual character's confirmed identity");
        var moved = roster with { People = [roster.People[0] with { Characters = [main, knownAlt, unvisited with { AccountId = "main" }] }] };
        Check(Key(unvisited, moved) == "", "a stale selection cannot inherit an account after the character moved");
        var removed = roster with { People = [roster.People[0] with { Characters = [main, knownAlt] }] };
        Check(Key(unvisited, removed) == "", "removed characters cannot inherit a former account's identity");
        var duplicate = roster with { People = [roster.People[0] with { Characters = [knownAlt, unvisited, unvisited] }] };
        Check(Key(unvisited, duplicate) == "", "ambiguous character membership remains unverified");
        Check(Key(unvisited with { GameAccountKey = "invalid" }) == "", "invalid explicit identity is not replaced by an inferred one");
        var noProof = roster with { People = [roster.People[0] with { Characters = [unvisited, anotherWorld] }] };
        Check(Key(unvisited, noProof) == "", "an account needs at least one observed identity");
        var newlyObserved = roster with { People = [roster.People[0] with { Characters = [knownAlt, unvisited with { GameAccountKey = otherKey }] }] };
        Check(Key(unvisited, newlyObserved) == otherKey, "a fresh individual observation overrides account inference from an older UI selection");
        var groups = new List<SquareEnixLoginGroup>();
        var session = new SquareEnixLoginState();
        session.Observe(pairing, mainKey, groups);
        session.Observe(pairing, altKey, groups);
        Check(session.Relationship(pairing, Key(unvisited), groups, "sir", "main", "sir", "alt") == CharacterLoginRelationship.SameSquareEnixLogin,
            "logging into one character per service account unlocks direct switches for the other characters");
        var restarted = new SquareEnixLoginState();
        restarted.Observe(pairing, mainKey, groups);
        Check(restarted.Relationship(pairing, Key(later, expanded), groups, "sir", "main", "sir", "alt") == CharacterLoginRelationship.SameSquareEnixLogin,
            "saved login links still cover new account members after restart");
        Check(restarted.Relationship(pairing, Key(roster.People[1].Characters[1], person: "goddess"), groups, "sir", "main", "goddess", "alt") == CharacterLoginRelationship.Unverified,
            "the other login remains unverified even with account-wide propagation");
        Console.WriteLine($"Account-wide login regression checks passed: {checks}.");
    }
}
