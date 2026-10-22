using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class TitleLogin233Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception("Title login 233: " + reason); }
        var now = DateTimeOffset.Parse("2026-10-22T13:10:00Z");
        var pair = new string('a', 64);
        var target = new LoginProbeTarget("main-character", "person-one", "main", "1001", "Test Character", "Rafflesia");
        var exact = new LoginProbeEntry("1001", "Test Character", "Rafflesia", false);
        CharacterLoginProbe Start()
        {
            var probe = new CharacterLoginProbe();
            Check(probe.Begin(target, pair, now), "first click starts a check without prior game login");
            return probe;
        }
        LoginProbeResult Tick(CharacterLoginProbe probe, int ms = 1000, bool busy = false, bool ready = true,
            LoginProbeEntry[]? entries = null, string? pairing = null, LoginProbeTarget? selected = null, bool loggedIn = false) =>
            probe.Inspect(now.AddMilliseconds(ms), pairing ?? pair, selected ?? target, loggedIn, busy, ready, entries ?? [exact]);

        var first = Start();
        Check(!first.Begin(target, pair, now), "duplicate clicks cannot enqueue a second connection");
        Check(Tick(first, busy:true) == LoginProbeResult.Waiting, "Lifestream must finish opening the list before login");
        Check(Tick(first, ready:false) == LoginProbeResult.Waiting, "stale native entries behind title/loading are ignored");
        Check(Tick(first) == LoginProbeResult.Waiting, "first ready sample does not immediately log in");
        Check(Tick(first, 1100) == LoginProbeResult.Waiting, "rapid repeat still waits");
        Check(Tick(first, 1250) == LoginProbeResult.Verified, "fresh launch verifies exact character and continues without confirmation");
        first.Cancel();
        Check(!first.Active && Tick(first) == LoginProbeResult.Cancelled, "consumed check cannot later authorize another login");

        var interrupted = Start();
        Tick(interrupted);
        Check(Tick(interrupted, 1250, ready:false) == LoginProbeResult.Waiting, "loading interrupts the stable-list evidence");
        Check(Tick(interrupted, 1500) == LoginProbeResult.Waiting && Tick(interrupted, 1750) == LoginProbeResult.Verified,
            "two new ready samples are needed after loading");

        Check(Tick(Start(), entries:[exact with { ContentId = "9999" }]) == LoginProbeResult.NotFound, "same name and world cannot replace a known different CID");
        Check(Tick(Start(), entries:[exact with { World = "Siren" }]) == LoginProbeResult.NotFound, "same name on another world is not the selected character");
        Check(Tick(Start(), entries:[exact with { Locked = true }]) == LoginProbeResult.NotFound, "character locked to another service account cannot log in");
        Check(Tick(Start(), entries:[exact, exact]) == LoginProbeResult.NotFound, "ambiguous duplicate entries cannot log in");
        Check(Tick(Start(), entries:[]) == LoginProbeResult.Waiting, "empty list while loading is not proof of the wrong login");
        Check(Tick(Start(), entries:[exact with { Name = "Another Character", ContentId = "2001" }], busy:true) == LoginProbeResult.Waiting,
            "world search may show unrelated characters without failing before it finishes");
        Check(Tick(Start(), entries:[exact with { Name = "Another Character", ContentId = "2001" }]) == LoginProbeResult.NotFound,
            "different email's completed list cannot authorize the target");
        Check(Tick(Start(), pairing:new string('b', 64)) == LoginProbeResult.Cancelled, "pairing change cancels request");
        Check(Tick(Start(), selected:target with { AccountId = "alt" }) == LoginProbeResult.Cancelled, "roster move cancels request");
        Check(Tick(Start(), selected:target with { ContentId = "2001" }) == LoginProbeResult.Cancelled, "changed character identity cancels request");
        Check(Tick(Start(), loggedIn:true) == LoginProbeResult.Cancelled, "manual login during check never causes a second switch");
        Check(Tick(Start(), 45000, busy:true) == LoginProbeResult.TimedOut, "a missing target cannot leave Lifestream scanning indefinitely");
        var cancelled = Start(); cancelled.Cancel();
        Check(Tick(cancelled) == LoginProbeResult.Cancelled, "Stop check prevents a delayed automatic login");

        var noCid = target with { ContentId = "" };
        Check(CharacterLoginProbe.ExactCharacter(noCid, exact), "newly registered character can match unique actual name and home world");
        Check(!CharacterLoginProbe.ExactCharacter(noCid, exact with { ContentId = "0" }), "missing native identity cannot authorize login");
        Check(!new CharacterLoginProbe().Begin(target, "", now), "unpaired configuration cannot start");
        Check(!new CharacterLoginProbe().Begin(target with { Name = "/logout" }, pair, now), "invalid target cannot reach Lifestream");

        // Opening the list gives the existing relationship model actual local
        // evidence, restoring linked service accounts but not another login.
        SharedCharacter Character(string id, string account) => new(id, "Test Character", "Rafflesia", "Dynamis", "NA", account, [], AccountId:account, ContentId:id);
        var main = Character("1001", "main"); var alt = Character("2001", "alt"); var other = Character("3001", "main");
        var roster = new SharedRoster(1, now, [new SharedPerson("person-one", "Sir", [main, alt]), new SharedPerson("person-two", "Goddess", [other])]);
        var groups = new List<LoginAccountGroup>(); var yesterday = new LoginAccountLinks();
        yesterday.Observe(pair, roster, main.ContentId, groups); yesterday.Observe(pair, roster, alt.ContentId, groups);
        var freshClient = new LoginAccountLinks();
        Check(!freshClient.Recognizes(pair, roster, alt, "person-one", "alt", groups), "saved groups alone do not identify today's client");
        freshClient.ObserveMany(pair, roster, [main.ContentId], groups);
        Check(freshClient.Recognizes(pair, roster, alt, "person-one", "alt", groups), "automatic ready-list evidence restores the learned second service account");
        Check(!freshClient.Recognizes(pair, roster, other, "person-two", "main", groups), "paired other login remains distinct");
        Console.WriteLine($"Automatic title login checks passed: {checks}.");
    }
}
