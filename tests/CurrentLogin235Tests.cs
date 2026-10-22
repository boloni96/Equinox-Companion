using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class CurrentLogin235Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception("Current login 235: " + reason); }
        var now = DateTimeOffset.Parse("2026-10-22T20:00:00Z");
        var pair = new string('a', 64);
        SharedCharacter Character(string cid, string account) => new(cid, "Test Character", "Siren", "Aether", "NA", account, [], AccountId: account, ContentId: cid);
        var main = Character("1001", "main"); var otherMain = Character("1002", "main"); var alt = Character("2001", "alt");
        var goddessMain = Character("3001", "main"); var goddessAlt = Character("4001", "alt");
        var roster = new SharedRoster(1, now,
            [new SharedPerson("sir", "Sir", [main, otherMain, alt]), new SharedPerson("goddess", "EQUINOX", [goddessMain, goddessAlt])]);
        var groups = new List<LoginAccountGroup>();
        var learned = new LoginAccountLinks();
        learned.Observe(pair, roster, main.ContentId, groups);
        learned.Observe(pair, roster, alt.ContentId, groups);

        // A process restart preserves account links, but requires current game
        // evidence before using them. The initial action has no target search.
        var fresh = new LoginAccountLinks();
        Check(!fresh.Recognizes(pair, roster, main, "sir", "main", groups), "saved groups do not identify the newly launched client");
        Check(CharacterLoginPolicy.InitialAction(CharacterLoginMode.Title, CharacterLoginRelationship.Unverified, false) == CharacterLoginAction.IdentifyLogin,
            "fresh title opens the current login list instead of warning or searching a paired target");
        Check(CharacterLoginPolicy.InitialAction(CharacterLoginMode.Title, CharacterLoginRelationship.Unverified, true) == CharacterLoginAction.Confirm,
            "known login and unverified target still warn before any target search");
        foreach (var mode in new[] { CharacterLoginMode.InGame, CharacterLoginMode.CharacterSelect })
            Check(CharacterLoginPolicy.InitialAction(mode, CharacterLoginRelationship.Unverified, false) == CharacterLoginAction.Confirm,
                "neutral START path is not used from " + mode);
        Check(CharacterLoginPolicy.InitialAction(CharacterLoginMode.Unavailable, CharacterLoginRelationship.Unverified, false) == CharacterLoginAction.Wait,
            "cannot start identification during loading");

        var read = new CurrentLoginIdentification();
        Check(!read.Observe(0, [main.ContentId, otherMain.ContentId], true, now), "first ready list waits for stability");
        Check(!read.Observe(0, [otherMain.ContentId, main.ContentId, main.ContentId], true, now.AddMilliseconds(199)), "reordered list does not bypass minimum time");
        Check(read.Observe(0, [otherMain.ContentId, main.ContentId], true, now.AddMilliseconds(250)), "same native list stabilizes regardless of order");
        fresh.ObserveMany(pair, roster, [main.ContentId, otherMain.ContentId], groups);
        foreach (var target in new[] { main, otherMain, alt })
        {
            Check(fresh.Recognizes(pair, roster, target, "sir", target.AccountId, groups), "current native list restores the whole account and learned service-account links");
            Check(CharacterLoginPolicy.InitialAction(CharacterLoginMode.Title, CharacterLoginRelationship.SameSquareEnixLogin, true) == CharacterLoginAction.Proceed,
                "recognized login continues directly");
        }
        foreach (var target in new[] { goddessMain, goddessAlt })
            Check(!fresh.Recognizes(pair, roster, target, "goddess", target.AccountId, groups), "matching MAIN/ALT labels do not identify another login");

        // Launching the other person's login must not reuse the previous process.
        var otherProcess = new LoginAccountLinks();
        otherProcess.ObserveMany(pair, roster, [goddessMain.ContentId], groups);
        Check(otherProcess.Recognizes(pair, roster, goddessMain, "goddess", "main", groups), "new login is identified by its own native character");
        Check(!otherProcess.Recognizes(pair, roster, main, "sir", "main", groups), "new process does not inherit last login");
        Check(!otherProcess.Recognizes(pair, roster, goddessAlt, "goddess", "alt", groups), "same person does not imply same login");

        Check(!read.Observe(1, [main.ContentId], true, now.AddSeconds(1)), "service-account change requires a new stable sample");
        Check(!read.Observe(1, [main.ContentId], false, now.AddSeconds(2)), "loading invalidates the sample");
        Check(!read.Observe(1, [main.ContentId], true, now.AddSeconds(3)), "ready list after loading must stabilize again");
        Check(read.Observe(1, [main.ContentId], true, now.AddSeconds(4)), "subsequent stable sample can identify");
        Check(!read.Observe(1, ["0", "not-a-cid"], true, now.AddSeconds(5)), "invalid or empty evidence cannot identify");
        Check(!read.Observe(-1, [main.ContentId], true, now.AddSeconds(6)), "unset native service account cannot identify");
        var unknown = new LoginAccountLinks();
        unknown.ObserveMany(pair, roster, ["9999"], groups);
        Check(!unknown.Recognizes(pair, roster, main, "sir", "main", groups), "unpaired game list does not restore saved login");
        Check(!fresh.Recognizes(new string('b', 64), roster, main, "sir", "main", groups), "pairing change invalidates native identification");

        // The existing request guard surrounds native reads as well as IPC. A
        // late list cannot resume a stopped request or a different target.
        var targetIdentity = new LoginProbeTarget(main.Id, "sir", "main", main.ContentId, main.Name, main.World);
        var request = new CharacterLoginProbe();
        Check(request.Begin(targetIdentity, pair, now), "neutral identification has a bounded request");
        Check(request.CheckState(now.AddSeconds(45), pair, targetIdentity, false) == LoginProbeResult.TimedOut, "connection timeout remains 45 seconds");
        request.Cancel();
        Check(request.CheckState(now.AddSeconds(46), pair, targetIdentity, false) == LoginProbeResult.Cancelled, "Stop prevents acting on a late ready list");
        request.Begin(targetIdentity, pair, now);
        Check(request.CheckState(now.AddSeconds(1), pair, targetIdentity with { AccountId = "moved" }, false) == LoginProbeResult.Cancelled, "roster account change cancels the request");
        Check(request.CheckState(now.AddSeconds(1), pair, targetIdentity, true) == LoginProbeResult.Cancelled, "manual login cancels the pending identification");
        Console.WriteLine($"Current login checks passed: {checks}.");
    }
}
