namespace EquinoxCompanion;

public sealed record ObservationPrunePlan(HashSet<string> Houses,HashSet<string> Tending,HashSet<string> Planting,HashSet<string> Discoveries)
{
    public int Count=>Houses.Count+Tending.Count+Planting.Count+Discoveries.Count;
}
public static class ObservationRetention
{
    // Same 60-day/current-state rules; run over immutable snapshots off the game thread.
    public static ObservationPrunePlan Plan(HouseObservation[] houses,TendingRecord[] tends,PlantingRecord[] plants,SyncEvent[] discoveries,string[] acknowledged,DateTimeOffset cutoff)
    {
        var sent=acknowledged.ToHashSet();
        var latestHouse=houses.GroupBy(x=>(x.Actor.ContentId,x.Address.HouseId,x.Kind)).Select(g=>g.MaxBy(x=>x.ObservedAt)!.EventId).ToHashSet();
        var latestTend=tends.GroupBy(x=>(x.Actor.ContentId,x.Address.HouseId,x.Patch,x.Bed)).Select(g=>g.MaxBy(x=>x.ConfirmedAt)!.EventId).ToHashSet();
        var latestPlant=plants.GroupBy(x=>(x.Actor.ContentId,x.Address.HouseId,x.Patch,x.Bed)).Select(g=>g.MaxBy(x=>x.ConfirmedAt)!.EventId).ToHashSet();
        var latestDetail=discoveries.GroupBy(x=>(x.Actor.ContentId,x.Kind,x.Address?.HouseId,x.Patch,x.Bed,x.Collection?.Category,x.Collection?.Segment,x.GardenTarget?.Argument,x.Storage?.Key,x.Company?.Id,x.Company?.Profile?.Source)).Select(g=>g.MaxBy(x=>x.At)!.Id).ToHashSet();
        return new(houses.Where(x=>x.ObservedAt<cutoff&&sent.Contains(x.EventId)&&!latestHouse.Contains(x.EventId)).Select(x=>x.EventId).ToHashSet(),
            tends.Where(x=>x.ConfirmedAt<cutoff&&sent.Contains(x.EventId)&&!latestTend.Contains(x.EventId)).Select(x=>x.EventId).ToHashSet(),
            plants.Where(x=>x.ConfirmedAt<cutoff&&sent.Contains(x.EventId)&&!latestPlant.Contains(x.EventId)).Select(x=>x.EventId).ToHashSet(),
            discoveries.Where(x=>x.At<cutoff&&sent.Contains(x.Id)&&!latestDetail.Contains(x.Id)).Select(x=>x.Id).ToHashSet());
    }
}
