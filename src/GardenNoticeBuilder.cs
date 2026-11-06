using System.Text.Json;
namespace EquinoxCompanion;

// Only managed snapshots enter this worker. Native lookups and chat remain on the framework.
public static class GardenNoticeBuilder
{
    public static (string Text, ushort Color)[] Build(SharedRoster roster, PlantingRecord[] planting,
        TendingRecord[] tending, SyncEvent[] history, SyncEvent[] local, SyncEvent[] mappings,
        Dictionary<string,DateTimeOffset> completed, string cataloguePath, DateTimeOffset now,
        bool notifyTending, bool notifyRisk)
    {
        var times=new Dictionary<string,(string Crop,double Days,double? Wilt)>(StringComparer.OrdinalIgnoreCase);
        var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        using(var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(cataloguePath,"timings.json"))))
            foreach(var t in data.RootElement.EnumerateArray())times[t.GetProperty("seed").GetString()!]=(t.GetProperty("crop").GetString()!,t.GetProperty("days").GetDouble(),t.GetProperty("wilt").ValueKind==JsonValueKind.Number?t.GetProperty("wilt").GetDouble():null);
        using(var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(cataloguePath,"crops.json"))))
            foreach(var row in data.RootElement.EnumerateArray()){
                var name=row.GetProperty("cropLabel").GetString()!;names[name]=name;
                names[row.GetProperty("plantingMaterialLabel").GetString()!]=name;
                foreach(var alias in row.GetProperty("aliases").EnumerateArray())names[alias.GetString()!]=name;
            }
        // Ignore unrelated inventory payloads before indexing or projecting garden history.
        var garden=history.Where(e=>e.Kind.StartsWith("garden.",StringComparison.Ordinal)).ToArray();
        var reads=new DiscoveryReadIndex();reads.Refresh(garden);
        var allPlans=GardenPlanSourceBuilder.Build(roster).Select(p=>{
            var revision=roster.GardenPlanRevisions?.FirstOrDefault(r=>r.HouseId==p.HouseId&&r.Batch==p.Batch);
            if(p.At==DateTimeOffset.MinValue&&revision is not null)p=p with {At=revision.At};
            var assigned=roster.GardenBatchOrders?.FirstOrDefault(o=>o.HouseId==p.HouseId)?.At;
            return GardenPlanEditing.Apply(p,garden.Where(e=>e.Kind=="garden.plan"&&(assigned is null||e.At>assigned)));
        }).ToArray();
        allPlans=GardenBatchOrdering.Apply(allPlans,roster.GardenBatchOrders??[],garden.Where(e=>e.Kind=="garden.batch-order"));
        var resolve=GardenTargetMap.CreateResolver((roster.GardenTargets??[]).Concat(garden.Concat(mappings).Select(GardenTargetMap.FromNumbered).OfType<SharedGardenTarget>()));
        var actions=local.Concat(garden.Where(e=>e.Kind is "garden.empty" or "garden.empty.unmapped" or "garden.unmapped" or "garden.dead" or "garden.ready" or "garden.observed" or "garden.fertilized" or "garden.status" or "garden.status.unmapped")).Select(resolve).ToArray();
        SharedGardenPlan Project(SharedGardenPlan plan){
            var key=plan.HouseId+":"+plan.Batch+":"+plan.At.ToUnixTimeMilliseconds();
            if(completed.TryGetValue(key,out var at))plan=plan with {CompletedAt=at};
            var result=GardenLive.Apply(plan,actions,n=>times.TryGetValue(n,out var t)?t.Crop:names.GetValueOrDefault(n,n),n=>times.TryGetValue(n,out var t)?t.Days:0,n=>times.TryGetValue(n,out var t)?t.Wilt:null);
            return result with {Beds=result.Beds.Select(b=>b.DeadConfirmedAt is null&&b.Planted is {} planted&&b.HarvestAt is null&&times.TryGetValue(b.ActualCrop,out var timing)?b with {Days=timing.Days,WiltHours=timing.Wilt,HarvestAt=timing.Days>0?planted.AddDays(timing.Days):null}:b).ToArray()};
        }
        var plants=planting.GroupBy(t=>(t.Address.HouseId,t.Patch,t.Bed)).ToDictionary(g=>g.Key,g=>g.Max(t=>t.ConfirmedAt));
        var tends=tending.GroupBy(t=>(t.Address.HouseId,t.Patch,t.Bed)).ToDictionary(g=>g.Key,g=>g.Max(t=>t.ConfirmedAt));
        var notices = new List<GardenPersonNotice>();
        foreach (var house in roster.GardenCare.GroupBy(g => g.HouseId))
        {
            var person = GardenLoginSummary.Person(roster.People, house.First());
            if (person is null) continue;
            foreach (var batch in house)
            {
                var source=allPlans.FirstOrDefault(p=>p.HouseId==batch.HouseId&&p.Batch==batch.Batch);
                var projection=source is null?null:Project(source);
                foreach (var bed in batch.Beds)
                {
                    // Local confirmed care already happened, even if the browser has not saved it yet.
                    var key=(batch.GameHouseId,(batch.PhysicalPatch>0?batch.PhysicalPatch:batch.Batch),bed.Bed);
                    var lastPlant = plants.GetValueOrDefault(key,DateTimeOffset.MinValue);
                    var lastTend = tends.GetValueOrDefault(key,DateTimeOffset.MinValue);
                    var localCare = lastPlant > lastTend ? lastPlant : lastTend;
                    var empty = reads.EmptyAt(key.GameHouseId,key.Item2,key.Bed);
                    if (empty > (bed.Watered ?? DateTimeOffset.MinValue) && empty >= localCare) continue;
                    var effective = localCare > (bed.Watered ?? DateTimeOffset.MinValue) ? bed with { Watered = localCare, NextTend = localCare.AddHours(12), DeathAt = bed.WiltHours is > 0 ? localCare.AddHours(bed.WiltHours.Value+24) : null } : bed;
                    var projected=projection?.Beds.FirstOrDefault(b=>b.Bed==bed.Bed);
                    if(projection?.CompletedAt is null&&projected is not null&&GardenPlantRequirement.SuppressTending(projected))continue;
                    if(projected is not null)
                    {
                        if(projected.ActualCrop=="Empty")continue;
                        effective=effective with {Empty=false,Ready=projected.Ready,KeepMature=projected.KeepMature,Watered=projected.Watered,NextTend=projected.NextTend,HarvestAt=projected.HarvestAt,WiltHours=projected.WiltHours,
                            DeadConfirmedAt=projected.DeadConfirmedAt,WiltedAt=projected.WiltedAt,Planted=projected.Planted,GrowingObservedAt=projected.GrowingObservedAt,
                            DeathAt=projected.Ready||GardenVisualState.For(projected,false,now)=="dead"?null:projected.Watered is {} care&&projected.WiltHours is {} wilt?care.AddHours(wilt+24):null};
                    }
                    else if (lastPlant > (bed.Watered ?? DateTimeOffset.MinValue)) effective = effective with { Ready = false, KeepMature = false, HarvestAt = null, DeathAt = null };
                    var kind = projection is not null && projected is not null
                        ? GardenLoginSummary.Kind(projection, projected, now)
                        : GardenLoginSummary.Kind(effective, now);
                    if (kind == "tend" && notifyTending || kind == "check" && notifyRisk)
                        notices.Add(new(person.Id, person.Name, kind!));
                }
            }
        }
        return GardenLoginSummary.Messages(notices);
    }
}
