using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class CharacterLogin225Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Character login: " + why); }
        var calls = new List<string>();
        bool Request(CharacterLoginMode mode, bool accept = true, string name = "Leonis Verelle", string world = "Rafflesia")
        {
            calls.Clear();
            return CharacterLoginPolicy.Request(mode, name, world,
                command => calls.Add("switch:" + command),
                (n, w) => { calls.Add("title:" + n + "@" + w); return accept; },
                (n, w) => { calls.Add("select:" + n + "@" + w); return accept; });
        }
        Check(Request(CharacterLoginMode.Title) && calls.SequenceEqual(["title:Leonis Verelle@Rafflesia"]), "title login uses the home identity without a logout command");
        Check(Request(CharacterLoginMode.CharacterSelect) && calls.SequenceEqual(["select:Leonis Verelle@Rafflesia"]), "character selection uses its own login entry point");
        Check(Request(CharacterLoginMode.InGame) && calls.SequenceEqual(["switch:Leonis Verelle@Rafflesia"]), "existing in-game switch command remains unchanged");
        Check(!Request(CharacterLoginMode.Title, false) && calls.SequenceEqual(["title:Leonis Verelle@Rafflesia"]), "a refused title login does not become an accepted switch");
        Check(!Request(CharacterLoginMode.CharacterSelect, false) && calls.Count == 1, "missing character does not invoke a fallback on another screen");
        Check(!Request(CharacterLoginMode.Unavailable) && calls.Count == 0, "loading or queue state cannot request login");
        Check(!Request(CharacterLoginMode.Title, name: "Leonis Verelle@Other") && calls.Count == 0, "reject a decorated or command-like identity before IPC");
        Check(!Request(CharacterLoginMode.CharacterSelect, world: "Rafflesia extra") && calls.Count == 0, "reject an invalid home world before IPC");
        Check(CharacterLoginPolicy.SameSource(0, false, 0), "offline consent remains usable while logged out");
        Check(!CharacterLoginPolicy.SameSource(0, true, 123), "offline consent cannot log out a newly loaded character");
        Check(CharacterLoginPolicy.SameSource(123, true, 123), "current character consent remains valid");
        Check(!CharacterLoginPolicy.SameSource(123, true, 456), "changed character requires new consent");
        Check(!CharacterLoginPolicy.SameSource(123, false, 0), "logout invalidates in-game consent");
        Console.WriteLine($"Character login regression checks passed: {checks}.");
    }
}
