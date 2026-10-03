namespace EquinoxCompanion;
// Confirmed actions only. The selected UI tab never supplies physical identity.
public static class GardenLive
{
    public static SharedGardenPlan Apply(SharedGardenPlan plan, IEnumerable<SyncEvent> events, Func<string,string> cropName, Func<string,double>? cropDays=null, Func<string,double?>? cropWilt=null)
    {
        var beds=plan.Beds.ToDictionary(b=>b.Bed);
        var complete=plan.CompletedAt;
        foreach(var e in events.OrderBy(e=>e.At))
        {
            if(e.Address is null || SharedGardenLocation.Match(e.Address,[plan])!=plan.HouseId || e.Patch!=(plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch) || e.Bed is not (>=1 and <=8))continue;
            if(!beds.TryGetValue(e.Bed.Value,out var b))continue;
            if(b.Watered is {} latestCare && e.At<latestCare)continue;
            if(b.ObservedAt is {} observed && e.At<=observed || b.Planted is {} planted && e.At<planted)continue;
            if(b.ActualCrop=="Empty" && e.Kind is "garden.tended" or "garden.dead" or "garden.ready" or "garden.observed")b=b with {ActualCrop="Crop not identified"};
            if(e.Kind=="garden.planted" && e.Plant is {} p)
                b=b with {ActualCrop=cropName(p.SeedName),ActualSoil=p.SoilName,Planted=e.At,Watered=e.At,NextTend=e.At,Ready=false,KeepMature=false,PlantEvent=e.Id,TendedBy="",HarvestAt=(cropDays?.Invoke(p.SeedName)??0)>0?e.At.AddDays(cropDays!(p.SeedName)):null,Days=cropDays?.Invoke(p.SeedName)??0,WiltHours=cropWilt?.Invoke(p.SeedName),LastFertilized=null,ObservedAt=e.At};
            else if(e.Kind=="garden.tended")
                b=b with {Watered=e.At,NextTend=b.Ready?null:e.At.AddHours(12),TendedBy=e.Actor.Name+(string.IsNullOrWhiteSpace(e.Actor.HomeWorldName)?"":" @ "+e.Actor.HomeWorldName),ObservedAt=e.At};
            else if(e.Kind=="garden.fertilized" && !b.Ready && b.HarvestAt is {} harvest && e.At<harvest && (b.LastFertilized is null || e.At-b.LastFertilized>=TimeSpan.FromHours(1)))
                b=b with {HarvestAt=harvest-TimeSpan.FromTicks((harvest-e.At).Ticks/100),LastFertilized=e.At,ObservedAt=e.At};
            else if(e.Kind=="garden.status"&&e.Crop is {Ready:false,Status:"growing" or "wilting" or "dead"} status)
            {
                if(GardenVisualState.HasActualCrop(b)&&!string.Equals(b.ActualCrop,status.CropName,StringComparison.OrdinalIgnoreCase))
                    b=b with {Planted=null,Watered=null,NextTend=null,HarvestAt=null,Days=0,ActualSoil="",PlantEvent="",LastFertilized=null,TendedBy="",WiltHours=null};
                b=b with {ActualCrop=status.CropName,Ready=false,KeepMature=false,GrowingObservedAt=status.Status=="growing"?e.At:null,WiltedAt=status.Status=="wilting"?e.At:null,DeadConfirmedAt=status.Status=="dead"?e.At:null,NextTend=status.Status=="dead"?null:b.NextTend,HarvestAt=status.Status=="dead"?null:b.HarvestAt,ObservedAt=e.At};
            }
            else if(e.Kind=="garden.dead")
                b=b with {Ready=false,KeepMature=false,DeadConfirmedAt=e.At,NextTend=null,HarvestAt=null,ObservedAt=e.At};
            else if(e.Kind=="garden.empty")
                b=b with {ActualCrop="Empty",ActualSoil="",Planted=null,Watered=null,NextTend=null,HarvestAt=null,Ready=false,KeepMature=false,PlantEvent="",LastClearedAt=e.At,TendedBy="",LastFertilized=null,WiltHours=null,Days=0,ObservedAt=e.At};
            else if(e.Kind is "garden.ready" or "garden.observed")
                b=b with {ActualCrop=e.Crop?.CropName??b.ActualCrop,Ready=true,NextTend=null,ObservedAt=e.At};
            else continue;
            if(e.Kind is "garden.planted" or "garden.tended" or "garden.empty" or "garden.ready" or "garden.observed")b=b with {DeadConfirmedAt=null,WiltedAt=null,GrowingObservedAt=null};
            else if(e.Kind=="garden.dead")b=b with {WiltedAt=null,GrowingObservedAt=null};
            beds[b.Bed]=b;
            var interim=Progress(beds.Values.ToArray(),plan.At);
            if(complete is null && interim.Any(x=>x.Crop.Length>0)&&interim.Where(x=>x.Crop.Length>0).All(x=>x.Status=="confirmed"))complete=e.At;
        }
        return plan with {Beds=Progress(beds.Values.ToArray(),plan.At),CompletedAt=complete};
    }
    public static SharedGardenBed[] Progress(SharedGardenBed[] beds,DateTimeOffset planAt)
    {
        var result=beds.Select(b=>b.Crop.Length==0?b with {Status="actual"}:b with {Status=b.PlantEvent.Length==0||b.Planted<planAt||b.Planted is null||b.LastClearedAt>=b.Planted?"planned":b.ActualCrop!=b.Crop?"different":b.ActualSoil==b.Soil?"confirmed":b.ReplantOrder>0&&b.ActualSoil==b.StarterSoil?"starter":"different"}).ToArray();
        for(var i=0;i<result.Length;i++)
        {
            var b=result[i];if(b.ReplantOrder==0)continue;
            var others=result.Where(x=>x.Crop.Length>0&&x.Bed!=b.Bed).ToArray();
            var neighbours=others.All(x=>x.Status=="confirmed");
            if(b.Status=="confirmed"&&(!neighbours||others.Any(x=>x.Planted>b.Planted)))result[i]=b with {Status=neighbours?"replant":"starter"};
            else if(neighbours&&(b.Status=="starter"||b.ActualCrop=="Empty"&&b.LastClearedAt>planAt))result[i]=b with {Status="replant"};
        }
        return result;
    }
}
