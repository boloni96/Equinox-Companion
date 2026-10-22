using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class LoginBoundary234Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception("Login boundary 234: " + reason); }
        var now = DateTimeOffset.Parse("2026-10-22T19:06:25Z");
        var pair = new string('a', 64);
        SharedCharacter Character(string cid, string account) => new(cid, "Test Character", "Siren", "Aether", "NA", account, [], AccountId:account, ContentId:cid);
        var main = Character("1001", "main"); var alt = Character("2001", "alt");
        var goddessMain = Character("3001", "main"); var goddessAlt = Character("4001", "alt");
        var roster = new SharedRoster(1, now,
            [new SharedPerson("sir", "Sir", [main, alt]), new SharedPerson("goddess", "EQUINOX", [goddessMain, goddessAlt])]);
        var groups = new List<LoginAccountGroup>();
        var learned = new LoginAccountLinks();
        learned.Observe(pair, roster, main.ContentId, groups);
        learned.Observe(pair, roster, alt.ContentId, groups);

        // Reproduce both service accounts under the user's login, at title,
        // character selection and in game. No target action is allowed merely
        // because a paired character shares the MAIN/ALT account label.
        foreach (var local in new[] { main, alt })
        {
            var client = new LoginAccountLinks();
            client.Observe(pair, roster, local.ContentId, groups);
            foreach (var mode in new[] { CharacterLoginMode.Title, CharacterLoginMode.CharacterSelect, CharacterLoginMode.InGame })
            {
                foreach (var remote in new[] { goddessMain, goddessAlt })
                {
                    var relationship = client.Recognizes(pair, roster, remote, "goddess", remote.AccountId, groups)
                        ? CharacterLoginRelationship.SameSquareEnixLogin : CharacterLoginRelationship.Unverified;
                    Check(CharacterLoginPolicy.Action(mode, relationship) == CharacterLoginAction.Confirm,
                        $"{local.AccountId} to Goddess {remote.AccountId} at {mode} warns BEFORE search, logout or login");
                }
                foreach (var own in new[] { main, alt })
                {
                    var relationship = client.Recognizes(pair, roster, own, "sir", own.AccountId, groups)
                        ? CharacterLoginRelationship.SameSquareEnixLogin : CharacterLoginRelationship.Unverified;
                    Check(CharacterLoginPolicy.Action(mode, relationship) == CharacterLoginAction.Proceed,
                        $"verified {local.AccountId} to own {own.AccountId} at {mode} stays direct");
                }
            }
        }

        var fresh = new LoginAccountLinks();
        Check(!fresh.Recognizes(pair, roster, main, "sir", "main", groups), "saved groups alone cannot prove the login of a new game process");
        Check(CharacterLoginPolicy.Action(CharacterLoginMode.Title, CharacterLoginRelationship.Unverified) == CharacterLoginAction.Confirm,
            "an unidentified fresh client does not search an unverified target automatically");
        Check(CharacterLoginPolicy.Action(CharacterLoginMode.Title, CharacterLoginRelationship.Unverified, true, false) == CharacterLoginAction.Confirm,
            "stale confirmation after account, pairing or target changes cannot start a check");
        Check(CharacterLoginPolicy.Action(CharacterLoginMode.Title, CharacterLoginRelationship.Unverified, false, true) == CharacterLoginAction.Confirm,
            "remembered consent fields alone cannot start a check");
        Check(CharacterLoginPolicy.Action(CharacterLoginMode.Title, CharacterLoginRelationship.Unverified, true, true) == CharacterLoginAction.CheckList,
            "explicit same-login confirmation permits only exact-character verification at title");
        foreach (var relationship in Enum.GetValues<CharacterLoginRelationship>())
            Check(CharacterLoginPolicy.Action(CharacterLoginMode.Unavailable, relationship, true, true) == CharacterLoginAction.Wait,
                "confirmation cannot act during loading or another trip");

        var target = new LoginProbeTarget("3001", "goddess", "main", "3001", "Test Character", "Siren");
        var probe = new CharacterLoginProbe();
        Check(probe.Begin(target, pair, now), "an explicitly approved check starts");
        Check(probe.CheckState(now.AddSeconds(44), pair, target, false) == LoginProbeResult.Waiting, "check remains bounded while the connection is pending");
        Check(probe.CheckState(now.AddSeconds(45), pair, target, false) == LoginProbeResult.TimedOut, "deadline is decidable before optional IPC or native reads");
        Check(probe.Inspect(now.AddSeconds(72), pair, target, false, true, false, []) == LoginProbeResult.TimedOut,
            "screenshot timing cannot extend the check while Lifestream remains busy");
        probe.Cancel();
        Check(probe.CheckState(now.AddSeconds(73), pair, target, false) == LoginProbeResult.Cancelled,
            "stopped or timed-out request cannot resume on a late list");
        Console.WriteLine($"Login boundary checks passed: {checks}.");
    }
}
