using EquinoxCompanion;
public static class GardenNotice420Tests
{
    public static void Run()
    {
        var now = DateTimeOffset.Parse("2026-11-05T22:00:00Z");
        var bed = new SharedGardenBed(1, "Curiel Root", "", "confirmed", "Curiel Root", "",
            now.AddDays(-4), now.AddHours(-13), 3, false, HarvestAt: now.AddHours(-3));
        var plan = new SharedGardenPlan("house", "House", "World", "District", 1, 1, 1, "", now, [bed]);
        void Check(string name, SharedGardenBed value, string? expected)
        {
            var actual = GardenLoginSummary.Kind(plan, value, now);
            if(actual != expected) throw new Exception(name + ": " + actual + " != " + expected);
            Console.WriteLine("PASS " + name);
        }
        Check("estimated mature crop does not request tending", bed, null);
        Check("confirmed mature crop does not request tending", bed with { Ready = true }, null);
        Check("empty bed does not request tending", bed with { ActualCrop = "Empty" }, null);
        Check("recent care remains quiet", bed with { Watered = now.AddMinutes(-31), HarvestAt = now.AddDays(2) }, null);
        Check("growing overdue crop still requests tending", bed with { HarvestAt = now.AddDays(2) }, "tend");
        Check("new growing observation invalidates maturity estimate", bed with { GrowingObservedAt = now.AddMinutes(-10) }, "tend");
        Check("confirmed wilting still requests checking", bed with { WiltedAt = now.AddMinutes(-10) }, "check");
        Check("confirmed dead crop still requests checking", bed with { DeadConfirmedAt = now.AddMinutes(-10) }, "check");
        Check("estimated death before maturity still requests checking", bed with { Watered = now.AddHours(-80), WiltHours = 24 }, "check");
        Check("unknown unsynced bed does not request tending", bed with { ActualCrop = "Not synced yet", Planted = null, Watered = null, HarvestAt = null, NextTend = null }, null);
    }
}
