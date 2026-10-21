using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class FirstUseLogin232Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("First-use login 232: " + why); }
        var pair = new string('b', 64);
        SharedCharacter Character(string id, string account) =>
            new(id, "Any Player", "Siren", "Aether", "NA", "Any account label", [], AccountId: account, ContentId: id);
        var first = Character("5001", "account-a");
        var sibling = Character("5002", "account-a");
        var second = Character("6001", "account-b");
        var otherSibling = Character("6002", "account-b");
        var separate = Character("7001", "account-c");
        var roster = new SharedRoster(1, DateTimeOffset.UtcNow, [
            new SharedPerson("person-any", "Any person label", [first, sibling, second, otherSibling]),
            new SharedPerson("person-other", "Any person label", [separate])]);
        var empty = roster with { People = [] };
        var partial = roster with { People = [roster.People[0] with { Characters = [first, sibling] }] };
        var groups = new List<LoginAccountGroup>();
        var client = new LoginAccountLinks();
        bool Known(LoginAccountLinks process, SharedCharacter target, SharedRoster? current = null, string person = "person-any") =>
            process.Recognizes(pair, current ?? roster, target, person, target.AccountId, groups);

        Check(!client.Observe(pair, null, first.ContentId, groups), "first local login retained before any shared roster");
        Check(!client.Observe(pair, empty, second.ContentId, groups), "second service account retained before character registration sync");
        Check(!client.Refresh(pair, empty, groups) && groups.Count == 0, "empty roster does not invent an account");
        Check(!client.Refresh(pair, partial, groups), "partial roster resolves only the available account");
        Check(Known(client, sibling, partial), "first account siblings work on first click without fingerprint or previous switch");
        Check(!Known(client, second), "unresolved second account is not guessed from labels");
        Check(client.Refresh(pair, roster, groups), "delayed roster links both earlier local logins without visiting either again");
        Check(Known(client, otherSibling) && Known(client, sibling), "both whole accounts recognized after delayed sync, including at title screen");
        Check(!Known(client, separate, person:"person-other"), "paired person's account is not local evidence");
        Check(!client.Refresh(pair, roster with { Revision = 2 }, groups), "unchanged relationships do not repeatedly save");

        var remoteOnly = new LoginAccountLinks();
        Check(!remoteOnly.Refresh(pair, roster, groups), "roster arrival alone cannot establish this client's login");
        Check(!Known(remoteOnly, first), "another client cannot use saved links without local identity");
        remoteOnly.Observe(pair, null, separate.ContentId, groups);
        remoteOnly.Refresh(pair, roster, groups);
        Check(Known(remoteOnly, separate, person:"person-other") && !Known(remoteOnly, first), "a different game client's delayed identity stays separate");
        var restarted = new LoginAccountLinks();
        restarted.Observe(pair, null, sibling.ContentId, groups);
        restarted.Refresh(pair, roster, groups);
        Check(Known(restarted, otherSibling), "first normal login after restart restores learned links after roster arrival");

        var changedPairing = new LoginAccountLinks();
        changedPairing.Observe(pair, null, first.ContentId, groups);
        changedPairing.Refresh(new string('c', 64), null, groups);
        changedPairing.Refresh(pair, roster, groups);
        Check(!Known(changedPairing, second), "pairing changes clear unresolved process evidence");
        var invalid = new LoginAccountLinks();
        invalid.Observe("", null, first.ContentId, groups);
        invalid.Observe(pair, null, "0", groups);
        invalid.Observe(pair, null, "not-an-id", groups);
        invalid.Refresh(pair, roster, groups);
        Check(!Known(invalid, first), "invalid pairing and IDs cannot create deferred evidence");

        var duplicate = roster with { People = [roster.People[0], roster.People[1] with { Characters = [separate, first] }] };
        var uncertain = new LoginAccountLinks();
        uncertain.Observe(pair, null, first.ContentId, groups);
        uncertain.Refresh(pair, duplicate, groups);
        Check(!Known(uncertain, sibling, duplicate), "ambiguous registration remains unverified");
        uncertain.Refresh(pair, roster, groups);
        Check(Known(uncertain, sibling), "correcting ambiguous registration resolves actual local evidence automatically");
        var moved = roster with { People = [roster.People[0] with { Characters = [first with { AccountId = "moved" }, sibling, second, otherSibling] }, roster.People[1]] };
        uncertain.Refresh(pair, moved, groups);
        Check(!Known(uncertain, sibling, moved) && !Known(uncertain, first with { AccountId = "moved" }, moved), "resolved old evidence cannot silently rebind when a character moves accounts");

        // Ready lobby-list evidence uses the same path, even before this process
        // has entered the world or the user has clicked a Switch button.
        var title = new LoginAccountLinks();
        title.ObserveMany(pair, null, [first.ContentId, sibling.ContentId, "0"], groups);
        title.Refresh(pair, partial, groups);
        Check(Known(title, sibling, partial), "a ready native list identifies an account before first button use");
        Check(!title.ObserveMany(pair, partial, [first.ContentId, sibling.ContentId], groups), "repeated ready lists do not save duplicate links");
        Console.WriteLine($"First-use login and delayed-roster checks passed: {checks}.");
    }
}
