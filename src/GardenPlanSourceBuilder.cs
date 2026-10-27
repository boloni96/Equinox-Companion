namespace EquinoxCompanion;

// Display projection only. Never infers bed identities, ownership or actions.
public static class GardenPlanSourceBuilder
{
    private static int Capacity(string? size)=>size?.Trim().ToLowerInvariant() switch {"large"=>3,"medium"=>2,_=>1};
    public static SharedGardenPlan[] Build(SharedRoster? roster)
    {
        var saved=roster?.GardenPlans??[];var result=saved.ToList();
        var allHouses=(roster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses).Concat(roster?.GardenHouses??[]).ToArray();
        foreach(var group in allHouses.GroupBy(h=>h.Id))
        {
        var house=group.OrderByDescending(h=>Capacity(h.Size)).First();
        var capacity=Math.Max(group.Max(h=>Capacity(h.Size)),Math.Max(
            saved.Where(p=>p.HouseId==house.Id).Select(p=>Math.Max(p.Batch,p.Capacity)).DefaultIfEmpty(1).Max(),
            (roster?.GardenCare??[]).Where(c=>c.HouseId==house.Id).Select(c=>c.Batch).DefaultIfEmpty(1).Max()));
        capacity=Math.Clamp(capacity,1,3);
        for(var batch=1;batch<=capacity;batch++)
        {
            var care=roster?.GardenCare?.FirstOrDefault(c=>c.HouseId==house.Id&&c.Batch==batch);
            var existing=result.FindIndex(p=>p.HouseId==house.Id&&p.Batch==batch);
            var plan=existing>=0?result[existing]:new SharedGardenPlan(house.Id,house.Name,house.World,house.District,house.Ward,house.Plot,batch,"",DateTimeOffset.MinValue,[],house.GameHouseId,capacity,PhysicalPatch:care?.PhysicalPatch??roster?.GardenBatchOrders?.FirstOrDefault(o=>o.HouseId==house.Id)?.Order.ElementAtOrDefault(batch-1)??batch);
            var beds=Enumerable.Range(1,8).Select(number=>{
                var bed=plan.Beds.FirstOrDefault(b=>b.Bed==number);if(bed is not null)return bed;
                var b=care?.Beds.FirstOrDefault(b=>b.Bed==number);
                return new SharedGardenBed(number,"","","actual",b?.Empty==true?"Empty":b?.Crop is {Length:>0}?b.Crop:"Not synced yet",b?.Soil??"",b?.Planted,b?.Watered,0,b?.Ready??false,b?.NextTend,b?.HarvestAt,ObservedAt:b?.ObservedAt,TendedBy:b?.TendedBy??"",WiltHours:b?.WiltHours,DeadConfirmedAt:b?.DeadConfirmedAt,LastClearedAt:b?.LastClearedAt,LastFertilized:b?.LastFertilized,KeepMature:b?.KeepMature??false,WiltedAt:b?.WiltedAt,GrowingObservedAt:b?.GrowingObservedAt);
            }).ToArray();
            plan=plan with {Beds=beds,Capacity=capacity};if(existing>=0)result[existing]=plan;else result.Add(plan);
        }
        }
        return result.ToArray();
    }
}
