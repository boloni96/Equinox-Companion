using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class LoginAndShortcuts230Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Login/shortcuts 230: " + why); }
        var pairing = new string('a', 64);
        SharedCharacter Character(string id, string account) => new(id, "Test Character", "Rafflesia", "Dynamis", "NA", account, [], AccountId: account, ContentId: id);
        var roster = new SharedRoster(1, DateTimeOffset.UtcNow, [
            new SharedPerson("sir", "Sir", [Character("101", "main"), Character("102", "main"), Character("103", "alt")]),
            new SharedPerson("goddess", "Goddess", [Character("201", "main"), Character("202", "alt")])]);
        var state = new CharacterLoginAccountState();
        Check(state.ResolveTitle(roster, pairing) is null, "fresh title cannot infer login");
        state.Remember(pairing, 1, 103);
        Check(state.ResolveTitle(roster, pairing) == ("sir", "alt"), "loaded character is enough without a profile fingerprint");
        Check(state.Resolve(roster, pairing, -1) is null, "loading selection remains unverified");
        Check(state.ResolveTitle(roster, pairing) == ("sir", "alt"), "title retains identity after index resets to minus one");
        Check(state.Resolve(roster, pairing, 0) is null, "index zero alone cannot assert a selected service account");
        Check(state.ResolveTitle(roster, pairing) == ("sir", "alt"), "default lobby index cannot erase last authenticated identity");
        Check(state.Resolve(roster, pairing, 0, [101, 102]) == ("sir", "main"), "ready selection replaces earlier account evidence");
        Check(state.ResolveTitle(roster, pairing) == ("sir", "main"), "title follows newly verified selection");
        Check(state.Resolve(roster, pairing, 0, [999]) is null, "unknown third account is not the remembered account");
        Check(state.ResolveTitle(roster, pairing) is null, "unknown ready list does not retain an incorrect title label");
        state.Remember(pairing, -1, 201);
        Check(state.ResolveTitle(roster, pairing) == ("goddess", "main"), "native character proof is retained even before lobby index is available");
        Check(state.ResolveTitle(roster, new string('b', 64)) is null, "pairing changes clear title proof");
        Check(state.ResolveTitle(roster, pairing) is null, "returning to pairing cannot revive old proof");
        state.Remember(pairing, 0, 202);
        Check(state.ResolveTitle(roster, pairing) == ("goddess", "alt"), "Alt is recognized by its own character");
        Check(new CharacterLoginAccountState().ResolveTitle(roster, pairing) is null, "new client needs its own evidence");
        Check(state.ResolveTitle(roster with { People = [] }, pairing) is null, "removed roster character is not trusted");
        Check(state.Resolve(roster, pairing, 0, [101, 202]) is null, "ambiguous ready list stays unknown");
        Check(state.ResolveTitle(roster, pairing) is null, "ambiguous list clears title proof");
        state.Remember(pairing, 0, 101);
        var from = state.ResolveTitle(roster, pairing)!.Value;
        var logins = new SquareEnixLoginState();
        Check(logins.Relationship(pairing, "", [], from.Person, from.Account, "sir", "main") == CharacterLoginRelationship.SameServiceAccount,
            "returning to same account works before any cloud profile round trip");
        Check(logins.Relationship(pairing, "", [], from.Person, from.Account, "goddess", "main") == CharacterLoginRelationship.Unverified,
            "another Person's account still needs confirmation");

        for (var c = 'A'; c <= 'Z'; c++) Check(ShortcutKeyMap.VirtualKey(c.ToString()) == c, "letter mapping " + c);
        for (var f = 1; f <= 24; f++) Check(ShortcutKeyMap.VirtualKey("F" + f) == 0x70 + f - 1, "function mapping " + f);
        for (var d = 0; d <= 9; d++)
        {
            Check(ShortcutKeyMap.VirtualKey("Key" + d) == 0x30 + d, "number row " + d);
            Check(ShortcutKeyMap.VirtualKey("Keypad" + d) == 0x60 + d, "keypad " + d);
        }
        foreach (var (name, vk) in new (string, int)[] { ("Space",32), ("Tab",9), ("Backspace",8), ("Insert",45),
            ("Delete",46), ("Home",36), ("End",35), ("PageUp",33), ("PageDown",34),
            ("LeftArrow",37), ("RightArrow",39), ("UpArrow",38), ("DownArrow",40) })
            Check(ShortcutKeyMap.VirtualKey(name) == vk, "navigation mapping " + name);
        foreach (var invalid in new[] { "None", "", "F0", "F25", "Key10", "Keypad10", "Escape", "Mouse1" })
            Check(ShortcutKeyMap.VirtualKey(invalid) == 0, "unsupported key " + invalid);
        var binding = new Shortcut { Key = "F8", Ctrl = true };
        var activation = new ShortcutActivation();
        bool Tick(bool down, bool ctrl = true, bool blocked = false, bool alt = false, bool shift = false, bool super = false) =>
            activation.Step(binding, down, ctrl, alt, shift, super, blocked);
        Check(!Tick(false), "initial release arms binding");
        Check(Tick(true), "game key press fires without ImGui owning the key");
        Check(!Tick(true), "holding shortcut does not repeatedly toggle");
        Check(!Tick(false), "release does not fire");
        Check(!Tick(true, blocked:true), "typing or capture blocks the press");
        Check(!Tick(true), "ending typing cannot replay held press");
        Tick(false);
        Check(Tick(true), "fresh press after typing works");
        Tick(false);
        Check(!Tick(true, ctrl:false), "missing modifier does not fire");
        Check(!Tick(true), "adding modifier to held key does not fire");
        Tick(false);
        Check(!Tick(true, alt:true), "extra modifier excluded");
        Tick(false);
        Check(!Tick(true, shift:true), "extra shift excluded");
        Tick(false);
        Check(!Tick(true, super:true), "Windows key excluded");
        Tick(false);
        Check(Tick(true), "exact chord works after incorrect chord");
        activation.Reset();
        Check(!Tick(true), "resuming after cutscene while held does not fire");
        Tick(false);
        Check(Tick(true), "fresh press after cutscene works");
        binding.Key = "F9";
        Check(!Tick(true), "newly assigned held key does not activate");
        Tick(false);
        Check(Tick(true), "new assignment works after release");
        var other = new ShortcutActivation();
        Check(!other.Step(binding, true, true, false, false, false, false), "second client does not inherit key-edge state");
        Console.WriteLine($"Login and shortcut regression checks passed: {checks}.");
    }
}
