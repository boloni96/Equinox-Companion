using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class CharacterLoginAccount226Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Login account: " + why); }
        var pairing = new string('a', 64);
        SharedCharacter Character(string id, string account) => new(id, "Test Character", "Rafflesia", "Dynamis", "NA", account, [], AccountId: account, ContentId: id);
        var roster = new SharedRoster(1, DateTimeOffset.UtcNow, [
            new SharedPerson("sir", "Sir", [Character("101", "main"), Character("102", "main"), Character("103", "alt")]),
            new SharedPerson("goddess", "Goddess", [Character("201", "main")])]);
        var state = new CharacterLoginAccountState();
        Check(state.Resolve(roster, pairing, 0) is null, "a fresh title screen cannot infer an account from index zero");
        Check(state.Resolve(roster, pairing, 0, [999]) is null, "an unknown character list does not guess");
        Check(state.Resolve(roster, pairing, 0, [0]) is null, "empty native content IDs are not evidence");
        Check(state.Resolve(roster, pairing, -1, [101]) is null, "a loading or unset service-account index is not verified");
        Check(state.Resolve(roster, "", 0, [101]) is null, "an unpaired client cannot identify a shared account");
        var same = state.Resolve(roster, pairing, 0, [101, 102, 999]);
        Check(same == ("sir", "main"), "a known character proves the account of the loaded list");
        Check(same is {} known && !CharacterTravelPolicy.NeedsConfirmation(known.Person, known.Account, "sir", "main"), "same account proceeds without confirmation");
        Check(same is {} from && CharacterTravelPolicy.NeedsConfirmation(from.Person, from.Account, "goddess", "main"), "another Person's Main account still asks");
        Check(same is {} account && CharacterTravelPolicy.NeedsConfirmation(account.Person, account.Account, "sir", "alt"), "another service account still asks");
        Check(state.Resolve(roster, pairing, 0) == ("sir", "main"), "verified character-selection identity survives return to title");
        Check(state.Resolve(roster, pairing, 0, []) == ("sir", "main"), "an empty world list keeps existing session evidence");
        Check(state.Resolve(roster, pairing, 1) is null, "changing service accounts discards earlier evidence");
        Check(state.Resolve(roster, pairing, 0) is null, "returning to an old index alone does not restore trust");
        state.Remember(pairing, 0, 101);
        Check(state.Resolve(roster, pairing, 0) == ("sir", "main"), "a loaded in-game character identifies the session after logout");
        Check(new CharacterLoginAccountState().Resolve(roster, pairing, 0) is null, "a second client does not inherit the first client's identity");
        Check(state.Resolve(roster, new string('b', 64), 0) is null, "changing pairing invalidates remembered identity");
        Check(state.Resolve(roster, pairing, 0) is null, "restoring a pairing cannot revive discarded evidence");
        state.Remember(pairing, 0, 101);
        Check(state.Resolve(roster, pairing, 0, [101, 201]) is null, "conflicting Person mappings do not pick the first row");
        Check(state.Resolve(roster, pairing, 0) is null, "conflict invalidates a formerly trusted session");
        Check(state.Resolve(roster, pairing, 0, [101, 103]) is null, "conflicting accounts within one Person remain unknown");
        var incomplete = roster with { People = [new SharedPerson("sir", "Sir", [Character("101", "")])] };
        Check(state.Resolve(incomplete, pairing, 0, [101]) is null, "an incomplete account mapping cannot bypass confirmation");
        var duplicate = roster with { People = [roster.People[0], new SharedPerson("goddess", "Goddess", [Character("101", "main")])] };
        Check(state.Resolve(duplicate, pairing, 0, [101]) is null, "a content ID assigned to two Persons is ambiguous");
        state.Remember(pairing, 0, 101);
        Check(state.Resolve(roster with { People = [] }, pairing, 0) is null, "removed roster records cannot remain a trusted account");
        Check(state.Resolve(roster, pairing, 0, [201]) == ("goddess", "main"), "fresh known lobby evidence supersedes the earlier character");
        Console.WriteLine($"Login account regression checks passed: {checks}.");
    }
}
