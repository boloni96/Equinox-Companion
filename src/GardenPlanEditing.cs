namespace EquinoxCompanion;

public sealed record GardenPlanDraft(string Target,string Mode,string Recipe="",string StartCrop="",string Beds="all",string FirstBed="replant",string Step="",Dictionary<string,string>? Choices=null);
public sealed record GardenPlanStep(int Order,int Bed,string Crop,string Soil,int? Neighbour=null,string[]? Results=null,bool Replaces=false,bool CheckExisting=false,int ReplantOrder=0,string StarterSoil="");
public sealed record GardenPlanDefinition(GardenPlanDraft Draft,GardenPlanStep[] Steps);
public sealed record GardenPlanChange(string HouseId,int Batch,DateTimeOffset? BaseAt,GardenPlanDefinition? Plan);
public sealed record GardenPlanRevision(string HouseId,int Batch,DateTimeOffset At);
public sealed record GardenFavourite(string Id,string PersonId,string PersonName,string Label,string Target,string CropA,string CropB,string[] Results);

public static class GardenPlanEditing
{
    public static GardenPlanDefinition Build(GardenFavourite favourite,SharedGardenPlan actual,bool swapped=false)
    {
        var cross=!string.IsNullOrWhiteSpace(favourite.CropB);
        var a=swapped&&cross?favourite.CropB:favourite.CropA;var b=swapped&&cross?favourite.CropA:favourite.CropB;
        var soil=cross?"Grade 3 Thanalan Topsoil":"Potting Soil";
        var steps=new List<GardenPlanStep>();
        // Bed order is clockwise; an empty-batch starter is replaced after its neighbours.
        for(var bed=1;bed<=8;bed++)
        {
            var crop=cross&&bed%2==0?b:a;var old=actual.Beds.FirstOrDefault(x=>x.Bed==bed);
            var neighbour=cross&&bed>1?bed-1:(int?)null;
            steps.Add(new(bed,bed,crop,soil,neighbour,cross?favourite.Results:[],old is not null&&(GardenVisualState.HasActualCrop(old)||old.Ready),old?.ActualCrop is "Not synced yet" or "Crop not identified"));
        }
        if(cross)steps[0]=steps[0] with {Neighbour=2,ReplantOrder=9,StarterSoil="Potting Soil"};
        return new(new(favourite.Target,cross?"cross":"grow",cross?favourite.Id:"",a),steps.ToArray());
    }
    public static GardenPlanDefinition? Swap(GardenPlanDefinition? plan)
    {
        if(plan is null)return null;var names=plan.Steps.Select(s=>GardenCropIdentity.Canonical(s.Crop)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(names.Length!=2)return null;
        string SwapName(string name)=>GardenCropIdentity.Same(name,names[0])?names[1]:GardenCropIdentity.Same(name,names[1])?names[0]:name;
        return plan with {Draft=plan.Draft with {StartCrop=SwapName(plan.Draft.StartCrop.Length>0?plan.Draft.StartCrop:plan.Steps.OrderBy(s=>s.Bed).First().Crop)},Steps=plan.Steps.Select(s=>s with {Crop=SwapName(s.Crop)}).ToArray()};
    }
    public static SharedGardenPlan Apply(SharedGardenPlan source,IEnumerable<SyncEvent> events)
    {
        var plan=source;
        foreach(var e in events.Where(e=>e.Kind=="garden.plan"&&e.PlanEdit?.HouseId==source.HouseId&&e.PlanEdit.Batch==source.Batch).OrderBy(e=>e.At))
        {
            var edit=e.PlanEdit!;var baseAt=plan.At==DateTimeOffset.MinValue?(DateTimeOffset?)null:plan.At;
            if(baseAt!=edit.BaseAt)continue;
            plan=WithDefinition(plan,edit.Plan,e.At);
        }
        return plan;
    }
    public static SharedGardenPlan WithDefinition(SharedGardenPlan source,GardenPlanDefinition? definition,DateTimeOffset at)
    {
        var beds=source.Beds.Select(b=>{
            var s=definition?.Steps.FirstOrDefault(s=>s.Bed==b.Bed);
            return b with {Crop=s?.Crop??"",Soil=s?.Soil??"",Order=s?.Order??0,ReplantOrder=s?.ReplantOrder??0,StarterSoil=s?.StarterSoil??"",CheckExisting=s?.CheckExisting??false,Status=s is null?"actual":"planned"};
        }).ToArray();
        return source with {At=at,Definition=definition,Target=definition?.Draft.Target??"",CompletedAt=null,Beds=beds};
    }
    public static bool Valid(GardenPlanChange? change) => change is {Batch:>=1 and <=3}&&change.HouseId.Length is >0 and <=100&&
        (change.Plan is null || change.Plan is {Draft:{} d,Steps:{Length:<=8} s}&&d.Target.Length is >0 and <=100&&d.Mode is "grow" or "cross" or "plan"&&s.Select(x=>x.Bed).Distinct().Count()==s.Length&&s.All(x=>x.Bed is >=1 and <=8&&x.Order is >=0 and <=16&&x.Crop.Length is >0 and <=100&&x.Soil.Length is >0 and <=100));
}

public sealed record SharedGardenBatchOrder(string HouseId,int[] Order,DateTimeOffset? At);
public sealed record GardenBatchOrderChange(string HouseId,int[] Order,DateTimeOffset? BaseAt);
public static class GardenBatchOrdering
{
    public static bool Valid(GardenBatchOrderChange? x)=>x is {HouseId.Length:>0 and <=100,Order.Length:>=1 and <=3}&&x.Order.Distinct().Count()==x.Order.Length&&x.Order.All(n=>n>=1&&n<=x.Order.Length);
    public static int[] Swap(int[] order,int source,int destination){var result=order.ToArray();if(source<1||source>result.Length||destination<1||destination>result.Length)return result;(result[source-1],result[destination-1])=(result[destination-1],result[source-1]);return result;}
    public static SharedGardenPlan[] Apply(SharedGardenPlan[] plans,SharedGardenBatchOrder[] orders,IEnumerable<SyncEvent> events)
    {
        var state=orders.Where(x=>x is not null&&Valid(new(x.HouseId,x.Order,x.At))).GroupBy(x=>x.HouseId).ToDictionary(g=>g.Key,g=>g.First());
        foreach(var e in events.Where(e=>e.Kind=="garden.batch-order").OrderBy(e=>e.At)){
            var x=e.BatchOrder;if(!Valid(x))continue;state.TryGetValue(x!.HouseId,out var old);
            if(old?.At!=x.BaseAt)continue;
            var prior=old?.Order??Enumerable.Range(1,x.Order.Length).ToArray();if(prior.Length!=x.Order.Length)continue;
            plans=plans.Select(p=>p.HouseId==x.HouseId&&p.Batch>=1&&p.Batch<=prior.Length?p with {Batch=Array.IndexOf(x.Order,prior[p.Batch-1])+1}:p).ToArray();
            state[x.HouseId]=new(x.HouseId,x.Order,e.At);
        }
        return plans;
    }
}
