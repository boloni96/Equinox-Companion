using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class GardenMarkerSession221Tests
{
    [ModuleInitializer] public static void Run()
    {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("GardenMarkerSession221: " + why); }
        var start = DateTimeOffset.Parse("2026-10-20T20:30:00Z");
        var actor = new Actor("12345", "Example Gardener", 57, 57);
        var address = new Address("house-a", 57, 339, 1, 1, 0, false, false);
        GardenSnapshot Current(int seconds) => new(start.AddSeconds(seconds), actor, address,
            "bed-a", "Garden", null, null, true, [], new(2003757, 2, "EventObj", 10, 0, 20, 7));
        var menu = new GardenMenu(1, start, Current(0), "1st Bed, 1st Patch", ["Plant Seeds", "Quit"]);
        var session = new GardenMarkerSession();
        bool Open(int seconds, GardenMenu? selected = null, GardenSnapshot? target = null, nint addon = 10, uint id = 20) =>
            session.Matches(selected ?? menu, target ?? Current(seconds), start.AddSeconds(seconds), addon, id, true);

        Check(Open(1), "fresh numbered empty bed binds to the native planting window");
        Check(Open(59), "borders remain before the former timeout");
        Check(Open(61), "same open picker retains borders after one minute");
        Check(Open(600), "time spent choosing items does not expire the same live display");
        Check(!menu.Matches(start.AddSeconds(61), Current(61)), "action evidence still expires after one minute");
        Check(!Open(600, target: Current(590)), "stale target cannot draw");
        Check(!Open(600, target: Current(601)), "future target cannot draw");
        Check(!session.Matches(menu, null, start.AddSeconds(600), 10, 20, true), "missing target cannot draw");
        Check(Open(601), "fresh context resumes the same still-open window after a sample gap");
        Check(!Open(601, target: Current(601) with { TargetId = "bed-b" }), "another bed cannot inherit the marker");
        Check(!Open(601, target: Current(601) with { Address = address with { Plot = 2 } }), "another plot cannot inherit the marker");
        Check(!Open(601, target: Current(601) with { Address = address with { WorldId = 58 } }), "another world cannot inherit the marker");
        Check(!Open(601, target: Current(601) with { Address = null }), "unknown address cannot draw");
        Check(!Open(601, target: Current(601) with { Actor = actor with { ContentId = "other" } }), "another character cannot inherit the marker");
        Check(!session.Matches(menu, Current(602), start.AddSeconds(602), 10, 20, false), "closing the native planting window clears the binding");
        Check(!Open(603), "reopening cannot reuse an expired menu");
        var fresh = menu with { OpenedAt = start.AddSeconds(604), Target = Current(604) };
        Check(Open(605, fresh), "a new numbered menu starts a new display session");
        Check(Open(700, fresh), "new session also survives the timeout");
        Check(!Open(701, fresh, addon: 11), "another native window cannot reuse expired context");
        fresh = fresh with { OpenedAt = start.AddSeconds(702), Target = Current(702) };
        Check(Open(703, fresh), "fresh menu binds again after a window change");
        Check(!Open(800, fresh, id: 21), "reused native address with a new addon ID clears the binding");
        Check(!Open(801, menu with { OpenedAt = start.AddSeconds(800), Options = ["Tend Crop", "Quit"] }), "occupied bed cannot start planting borders");
        Check(!Open(801, menu with { OpenedAt = start.AddSeconds(800), Title = "Garden" }), "unnumbered menu cannot choose a bed");
        Check(!Open(801, menu with { OpenedAt = start.AddSeconds(800) }, addon: 0), "missing native window cannot draw");
        session.Reset();
        Check(!Open(900, fresh), "closing the Companion guide clears its display session");
        Console.WriteLine($"PASS 221 garden marker lifetime and identity checks: {checks}");
    }
}
