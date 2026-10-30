namespace EquinoxCompanion;

// UI projection only. Observations and uploads never read this cache.
public sealed class GardenPendingDisplay
{
 private readonly UiProjectionCache<SyncEvent[]> pendingCache=new(maxAgeMs:0);
 private SyncEvent[]? previousPending;
 private readonly Dictionary<SharedGardenPlan,SharedGardenPlan> plans=new(ReferenceEqualityComparer.Instance);
 public SharedGardenPlan Get(SharedGardenPlan plan,object? roster,string version,long now,Func<IEnumerable<SyncEvent>> actions,Func<IEnumerable<string>> sent)
 {
  var pending=pendingCache.Get(roster,version,now,()=>{var acknowledged=sent().ToHashSet();return actions().Where(e=>!acknowledged.Contains(e.Id)&&e.Address is not null).ToArray();});
  if(!ReferenceEquals(previousPending,pending)){previousPending=pending;plans.Clear();}
  if(plans.TryGetValue(plan,out var cached))return cached;
  var queued=pending.Where(e=>e.Patch==(plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch)&&SharedGardenLocation.Match(e.Address,[plan])==plan.HouseId).Select(e=>e.Bed).ToHashSet();
  var result=plan.Beds.Any(b=>b.Queued!=queued.Contains(b.Bed))?plan with {Beds=plan.Beds.Select(b=>b with {Queued=queued.Contains(b.Bed)}).ToArray()}:plan;
  if(plans.Count>=128)plans.Clear();plans[plan]=result;return result;
 }
}

