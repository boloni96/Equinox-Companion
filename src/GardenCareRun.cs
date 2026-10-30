namespace EquinoxCompanion;

public enum GardenCareAction { Tend, Fertilize, Harvest, Plant, RemoveStarter, Inspect }
public static class GardenCarePolicy
{
    public static bool TargetAllowed(ulong expected,ulong current,bool sent,GardenCareAction? action)=>expected==0||expected==current||current==0&&sent&&action is GardenCareAction.Harvest or GardenCareAction.RemoveStarter;
    public static SharedGardenPlan[] HouseHarvest(IEnumerable<SharedGardenPlan> plans,string house,DateTimeOffset now)=>HouseCare(plans,house,GardenCareAction.Harvest,now);
    public static SharedGardenPlan[] HouseCare(IEnumerable<SharedGardenPlan> plans,string house,GardenCareAction action,DateTimeOffset now)=>plans.Where(p=>p.HouseId==house&&p.Beds.Any(b=>Eligible(p,b,action,now))).OrderBy(p=>p.Batch).ToArray();
    public const string KeepGuide = "A cross is determined when the new seed is planted beside a compatible crop. Harvesting later collects that bed's possible crossbred seeds. Keep a standing neighbour only until all planting steps that need it are confirmed. Keep for repeated crosses is optional; release it when you no longer need that standing plant. Needing seeds for a later stage does not mean its parent must stay planted.";
    public static string Protection(SharedGardenPlan plan, SharedGardenBed bed)
    {
        if(bed.KeepMature)return "Kept for repeated crosses · release Keep mature in the Journal when finished.";
        if(plan.CompletedAt!=null)return "";
        var required=(plan.Definition?.Steps??[]).Where(s=>s.Neighbour==bed.Bed && plan.Beds.FirstOrDefault(b=>b.Bed==s.Bed)?.Status!="confirmed").Select(s=>s.Bed).Distinct().ToArray();
        return required.Length>0?"Needed beside Bed "+string.Join(", ",required)+" until its planting is confirmed.":"";
    }
    public static bool Eligible(SharedGardenPlan plan,SharedGardenBed bed,GardenCareAction action,DateTimeOffset now)
    {
        if(!GardenVisualState.HasActualCrop(bed)||bed.DeadConfirmedAt!=null||bed.Queued)return false;
        if(action==GardenCareAction.Harvest)return Protection(plan,bed).Length==0 && (bed.Ready || GardenTiming.MaturityEstimateDue(bed.HarvestAt,bed.GrowingObservedAt,now));
        if(bed.Ready || bed.KeepMature || GardenPlantRequirement.SuppressTending(bed))return false;
        return action==GardenCareAction.Tend ? bed.NextTend==null||bed.NextTend<=now : bed.LastFertilized==null||now-bed.LastFertilized>=TimeSpan.FromHours(1);
    }
    public static string Certainty(SharedGardenBed bed) => bed.Ready?"Confirmed ready":bed.Planted==null?"Planting time unknown":bed.HarvestAt is {} at?$"Estimated maturity from {at.LocalDateTime:g} · check in game; actual readiness can be later":"Harvest timing unknown";
}
public interface IGardenCarePort
{
    void Validate();
    bool Open(int bed);
    bool Act(int bed,GardenCareAction action);
    bool Confirmed(int bed,GardenCareAction action,DateTimeOffset sent);
    bool Close(int bed);
    void Release();
}
// Each action is sent once; no unconfirmed action is retried.
public sealed class GardenCareRun(IGardenCarePort port)
{
    private int[] beds=[];private int index,phase;private DateTimeOffset deadline,sent;
    public bool Running{get;private set;}
    public int Completed{get;private set;}
    public int Total=>beds.Length;
    public GardenCareAction Action{get;private set;}
    public string Status{get;private set;}="";
    public void Start(IEnumerable<int> selected,GardenCareAction action,DateTimeOffset now)
    {
        if(Running)throw new InvalidOperationException("Finish or stop garden care first.");
        beds=selected.Where(b=>b is >=1 and <=8).Distinct().ToArray();index=phase=Completed=0;Action=action;
        Running=beds.Length>0;deadline=now.AddSeconds(20);Status=Running?$"{action} · 0/{Total}":"No eligible beds.";
    }
    public void Stop(string reason){Running=false;Status=reason;port.Release();}
    public void Tick(DateTimeOffset now)
    {
        if(!Running)return;
        try{
            port.Validate();if(now>=deadline)throw new InvalidOperationException("Timed out waiting for the game; the last action was not retried.");
            var bed=beds[index];Status=$"{Action} · Bed {bed} · {Completed}/{Total}";
            bool advance=phase switch{0=>port.Open(bed),1=>port.Act(bed,Action),2=>port.Confirmed(bed,Action,sent),_=>port.Close(bed)};
            if(!advance)return;
            if(phase==1)sent=now;
            if(phase==2)Completed++;
            if(++phase==4){phase=0;index++;if(index==beds.Length){Stop($"Complete · {Completed}/{Total} beds confirmed.");return;}}
            deadline=now.AddSeconds(20);
        }catch(Exception e){Stop("Stopped: "+e.Message);}
    }
}

public static class GardenAutoPlant
{
    public static SharedGardenPlan[] HousePlans(IEnumerable<SharedGardenPlan> plans,string house)=>plans.Where(p=>p.HouseId==house&&p.Definition?.Steps.Length>0&&p.CompletedAt==null&&p.Beds.Any(b=>b.Crop.Length>0&&b.Status is not ("confirmed" or "starter"))).OrderBy(p=>p.Batch).ToArray();
    public static (int Bed,GardenCareAction Action)? Next(SharedGardenPlan plan)
    {
        if(!GardenGuidance.ValidOrder(plan))throw new InvalidOperationException("Saved planting order is invalid. Review this batch before auto planting.");
        var next=GardenGuidance.Next(plan);if(next==null)return null;
        if(GardenPlantRequirement.RemoveStarter(next)){
            if(next.KeepMature||next.Planted==null||next.Planted<plan.At||!GardenCropIdentity.Same(next.ActualCrop,next.Crop)||next.ActualSoil!=next.StarterSoil)
                throw new InvalidOperationException("The recorded crop is not the plan's confirmed temporary starter. Inspect it before continuing.");
            return(next.Bed,GardenCareAction.RemoveStarter);
        }
        if(GardenVisualState.HasActualCrop(next)&&next.ActualCrop!="Empty")throw new InvalidOperationException("The next bed contains an existing crop. Auto plant only removes a verified temporary starter.");
        return(next.Bed,GardenCareAction.Plant);
    }
    public static bool MatchesPlantConfirmation(string prompt,string seed,string soil)
    {
        static string Clean(string text)=>string.Join(" ",text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        var text=Clean(prompt);var requiredSeed=Clean(seed);var requiredSoil=Clean(soil);
        return text.Length<=600&&requiredSeed.Length>0&&requiredSoil.Length>0&&text.EndsWith("?")&&
            (text.StartsWith("prepare the bed with ")||text.StartsWith("plant "))&&text.Contains(requiredSeed)&&text.Contains(requiredSoil);
    }
    public static bool MatchesConfirmation(GardenCareAction action,string prompt)
    {
        var text=prompt.Trim().ToLowerInvariant();if(text.Length>600)return false;
        return action switch{
            GardenCareAction.RemoveStarter=>text.Contains("remove")&&text.Contains("crop"),
            GardenCareAction.Harvest=>text.Contains("harvest")&&text.Contains("crop"),
            GardenCareAction.Plant=>text.Contains("plant")&&(text.Contains("seed")||text.Contains("soil")),
            GardenCareAction.Fertilize=>text.Contains("fertiliz")&&(text.Contains("crop")||text.Contains("fishmeal")),_=>false};
    }
    public static bool PartialHarvest(SharedGardenPlan plan)=>plan.Beds.Any(b=>GardenVisualState.HasActualCrop(b)&&!b.Ready&&b.DeadConfirmedAt==null);
}
