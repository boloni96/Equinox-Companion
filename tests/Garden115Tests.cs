using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class Garden115Tests
{
    [ModuleInitializer] public static void Run()
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception("Garden115: " + label); }
        var old = DateTimeOffset.Parse("2026-10-03T19:57:07Z");
        var now = DateTimeOffset.Parse("2026-10-09T08:47:02Z");
        Check(GardenTargetMap.CanRepeatObservation("garden.unmapped", now-old), "repeat maturity after replant must survive deduplication");
        Check(!GardenTargetMap.CanRepeatObservation("garden.unmapped", TimeSpan.FromMilliseconds(1999)), "duplicate callback remains suppressed");
        Check(GardenTargetMap.CanRepeatObservation("garden.unmapped", TimeSpan.FromSeconds(2)), "inspection refresh boundary");
        Check(!GardenTargetMap.CanRepeatObservation("garden.mapped", now-old), "unchanged mapping remains deduplicated");
        Check(!GardenTargetMap.CanRepeatObservation("character.updated", now-old), "unrelated discovery unchanged");

        var actor = new Actor("18014498577745692", "Leonis Verelle", 410, 57, "Rafflesia", "Siren");
        var address = new Address("0039015500100036", 57, 341, 17, 55, 0, false, false, "Siren", "The Goblet");
        var target = new GardenSnapshot(now, actor, address, "0000000040000B50", "", null, null, false, [],
            new(2003757, 1073744720, "EventObj", -711.26074f, -28, -568.0518f, 117454345, 0));
        var matcher = new CropChatMatcher();
        matcher.Add(new CropChat(now, target, "Curiel Root\nThis crop is ready to be harvested."));
        Check(matcher.Drain(now.AddSeconds(1), name => name=="Curiel Root", true).Count==0, "wait for possible numbered menu");
        var observed = matcher.Drain(now.AddSeconds(2), name => name=="Curiel Root", true).Single();
        Check(observed.Patch==0 && observed.Crop.Ready, "visitor maturity retained without menu");
        var mapped = new SharedGardenTarget(address.HouseId, "Siren", "The Goblet", 17, 55, 1, 8,
            117454345, -711.26074f, -28, -568.0518f, now);
        var fresh = new SyncEvent("fresh", "garden.unmapped", observed.At, actor, address, 0, 0,
            Crop: observed.Crop, GardenTarget: new(mapped.Argument, mapped.X, mapped.Y, mapped.Z));
        var resolver = GardenTargetMap.CreateResolver([mapped]);
        var plan = new SharedGardenPlan("house", "Garden", "Siren", "The Goblet", 17, 55, 1, "", old,
            [new SharedGardenBed(8, "Curiel Root", "", "planned", "Curiel Root", "", old.AddMinutes(2),
                null, 0, false, ObservedAt: old.AddDays(2))], address.HouseId);
        var stale = fresh with {Id="old", At=old};
        Check(!GardenLive.Apply(plan, [resolver(stale)], x=>x).Beds.Single().Ready, "prior crop maturity cannot override later growth");
        var updated = GardenLive.Apply(plan, [resolver(stale), resolver(fresh)], x=>x).Beds.Single();
        Check(updated.Ready && updated.ObservedAt==now && updated.Bed==8, "fresh visitor maturity reaches mapped bed eight");
        Check(resolver(fresh with {GardenTarget=new(mapped.Argument-16777216, mapped.X, mapped.Y, mapped.Z)}).Bed==0,
            "same patch coordinates cannot assign another target to bed eight");
    }
}
