using System.Runtime.CompilerServices;
using System.Text.Json;
using EquinoxCompanion;

internal static class LoginLinksAndCapture231Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Login links/capture 231: " + why); }
        var pair = new string('a', 64);
        SharedCharacter Character(string id, string account, string world = "Rafflesia") =>
            new(id, "Test Character", world, "Dynamis", "NA", account, [], AccountId: account, ContentId: id);
        var main = Character("101", "main");
        var mainOther = Character("102", "main", "Siren");
        var alt = Character("201", "alt");
        var altOther = Character("202", "alt", "Cuchulainn");
        var goddessMain = Character("301", "main");
        var goddessAlt = Character("401", "alt");
        var roster = new SharedRoster(1, DateTimeOffset.UtcNow, [
            new SharedPerson("sir", "Sir", [main, mainOther, alt, altOther]),
            new SharedPerson("goddess", "Goddess", [goddessMain, goddessAlt])]);
        var groups = new List<LoginAccountGroup>();
        var client = new LoginAccountLinks();
        bool Known(SharedCharacter target, string person = "sir", SharedRoster? current = null, LoginAccountLinks? session = null, List<LoginAccountGroup>? saved = null) =>
            (session ?? client).Recognizes(pair, current ?? roster, target, person, target.AccountId, saved ?? groups);
        Check(!Known(main), "fresh process has no identity");
        Check(!client.Observe(pair, roster, "101", groups), "first account is an active anchor without a shared group");
        Check(Known(mainOther), "one local character identifies siblings on other worlds without GameAccountKey");
        Check(!Known(alt), "Main alone is not evidence about Alt");
        Check(client.Observe(pair, roster, "201", groups), "successful Alt login in same process links both accounts without cloud round trip");
        Check(Known(main) && Known(mainOther) && Known(altOther), "linked account applies to visited and unvisited characters");
        Check(groups.Count == 1 && groups[0].Accounts.Length == 2, "two witnesses saved");
        Check(!client.Observe(pair, roster, "202", groups), "another character on the same account does not repeatedly save");
        Check(!Known(goddessMain, "goddess") && !Known(goddessAlt, "goddess"), "same Main/Alt labels on another Person cannot borrow trust");
        var configuration = new Configuration { LoginAccountGroups = groups };
        var restored = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(configuration))!.LoginAccountGroups;
        Check(restored.Count == 1 && restored[0].Accounts.Length == 2, "links survive configuration serialization");
        var restarted = new LoginAccountLinks();
        Check(!Known(alt, session:restarted, saved:restored), "saved links alone do not identify the next game process");
        restarted.Observe(pair, roster, "102", restored);
        Check(Known(altOther, session:restarted, saved:restored), "any Main character restores the Alt link after restart");
        var otherClient = new LoginAccountLinks();
        otherClient.Observe(pair, roster, "301", restored);
        Check(!Known(main, session:otherClient, saved:restored), "simultaneous Goddess client is separate");
        Check(!Known(goddessAlt, "goddess", session:otherClient, saved:restored), "Goddess's other email is not inferred from Person name");
        var herOtherEmail = new LoginAccountLinks();
        herOtherEmail.Observe(pair, roster, "401", restored);
        Check(Known(goddessAlt, "goddess", session:herOtherEmail, saved:restored), "her Alt process identifies Alt");
        Check(!Known(goddessMain, "goddess", session:herOtherEmail, saved:restored), "separate email sessions never merge Main and Alt");
        Check(!new LoginAccountLinks().Observe(pair, roster, "999", restored), "unknown third account supplies no group membership");
        Check(!client.Observe("", roster, "101", groups), "invalid pairing not remembered");
        Check(!Known(alt), "returning pairing needs fresh local evidence");
        client.Observe(pair, roster, "101", groups);
        Check(Known(alt), "fresh evidence restores saved group");
        var renamed = roster with { People = [roster.People[0] with { Name = "Sir renamed", Characters = [main with { Account = "Service one" }, mainOther, alt, altOther] }, roster.People[1]] };
        Check(Known(altOther, current:renamed), "display-name edits preserve stable account links");
        var added = Character("203", "alt", "Coeurl");
        var expanded = roster with { People = [roster.People[0] with { Characters = [main, mainOther, alt, altOther, added] }, roster.People[1]] };
        Check(Known(added, current:expanded), "new account member inherits learned link");
        var staleTarget = roster with { People = [roster.People[0] with { Characters = [main, mainOther, alt, altOther with { AccountId = "third" }] }, roster.People[1]] };
        Check(!Known(altOther, current:staleTarget), "stale UI row cannot use account it left");
        var removedTarget = roster with { People = [roster.People[0] with { Characters = [main, mainOther, alt] }, roster.People[1]] };
        Check(!Known(altOther, current:removedTarget), "removed target remains unavailable");
        var movedWitness = roster with { People = [roster.People[0] with { Characters = [main, mainOther, alt with { AccountId = "third" }, altOther] }, roster.People[1]] };
        Check(!Known(altOther, current:movedWitness), "moving the saved witness invalidates its old account link");
        var duplicateTarget = roster with { People = [roster.People[0] with { Characters = [main, alt, altOther, altOther] }, roster.People[1]] };
        Check(!Known(altOther, current:duplicateTarget), "duplicate target is ambiguous");
        var duplicateWitness = roster with { People = [roster.People[0], roster.People[1] with { Characters = [goddessMain, main] }] };
        var unknown = new LoginAccountLinks();
        Check(!unknown.Observe(pair, duplicateWitness, "101", groups), "duplicate CID cannot establish current account");
        Check(!Known(alt, current:duplicateWitness, session:unknown), "ambiguous current identity cannot use saved groups");
        var next = new LoginAccountLinks();
        next.Observe(pair, roster, "101", restored);
        Check(!next.Observe(pair, roster, "201", restored), "revisiting a learned pair does not rewrite configuration");

        // The same physical-key map is used for capture and activation.
        var held = new HashSet<int>();
        string[] ReadKeys() => new[] { "F8", "A", "Key1", "Keypad1" }.Where(k => held.Contains(ShortcutKeyMap.VirtualKey(k))).ToArray();
        var capture = new ShortcutCapture();
        held.Add(0x77);
        Check(capture.Step(ReadKeys(), true, false, false, false) is null && capture.Pending is { Key:"F8", Ctrl:true }, "capture receives a physical F8 without ImGui key events");
        held.Clear();
        Check(capture.Step(ReadKeys(), true, false, false, false) is null, "capture waits for Ctrl release");
        var captured = capture.Step(ReadKeys(), false, false, false, false);
        Check(captured is { Key:"F8", Ctrl:true }, "release saves same captured chord");
        var activation = new ShortcutActivation();
        activation.Step(captured!, false, false, false, false, false, false);
        Check(activation.Step(captured!, true, true, false, false, false, false), "captured chord activates using same input values");
        Check(!activation.Step(captured!, true, true, false, false, false, false), "hold fires only once");
        activation.Reset();
        Check(!activation.Step(captured!, true, true, false, false, false, false), "refocus while holding cannot fire");
        Check(!ShortcutKeyboard.Down(0x77, false), "background client never reads or activates physical shortcut");
        Check(!ShortcutKeyboard.Down(0, true) && !ShortcutKeyboard.Down(255, true), "invalid physical key codes ignored");
        held.UnionWith([0x41, 0x31]);
        capture = new ShortcutCapture();
        Check(capture.Step(ReadKeys(), false, false, false, false) is null && capture.Error.Length > 0, "multiple main keys rejected during physical capture");
        held.Clear(); capture.Step(ReadKeys(), false, false, false, false);
        held.Add(0x61); capture.Step(ReadKeys(), false, true, true, false);
        held.Clear();
        Check(capture.Step(ReadKeys(), false, false, false, false) is { Key:"Keypad1", Alt:true, Shift:true }, "keypad and multiple modifiers capture correctly");
        Console.WriteLine($"Local login links and physical shortcut checks passed: {checks}.");
    }
}
