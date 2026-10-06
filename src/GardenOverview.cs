namespace EquinoxCompanion;

public sealed record GardenOverviewHouse(SharedPerson? Person, SharedCharacter? Character, SharedHouse? House, SharedGardenPlan[] Batches, string[] LinkedCharacters);
public sealed record GardenOverviewBed(int Bed, string Crop, string Status, string Care, string Harvest, string State, bool TendDue, DateTimeOffset? NextTend, DateTimeOffset? HarvestAt);

public static class GardenOverview
{
    public static string[] Indicators(IEnumerable<SharedGardenPlan> plans,DateTimeOffset now)
    {
        var beds=plans.SelectMany(p=>p.Beds.Select(b=>Bed(p,b,now))).ToArray();
        var icons=new List<string>();
        if(beds.Any(b=>b.State=="ready"))icons.Add("ready");
        if(beds.Any(b=>b.TendDue))icons.Add("tend");
        if(beds.Any(b=>b.State is "dead" or "wilted" or "wilt-estimated" or "at-risk" or "dead-estimated"))icons.Add("at-risk");
        if(beds.Any(b=>b.State=="check-maturity"))icons.Add("check-maturity");
        if(beds.Any(b=>b.State=="unknown"||b.Status=="Growing"&&b.NextTend is null))icons.Add("unknown");
        if(icons.Count==0&&beds.Any(b=>b.State is "wet" or "keep-mature"))icons.Add("matched");
        return icons.ToArray();
    }
    public static string Attention(IEnumerable<SharedGardenPlan> plans,DateTimeOffset now)
    {
        var beds=plans.SelectMany(p=>p.Beds.Select(b=>Bed(p,b,now))).ToArray();
        if(beds.Any(b=>b.State=="dead"))return "dead";
        if(beds.Any(b=>b.State is "wilted" or "wilt-estimated" or "at-risk" or "dead-estimated"))return "risk";
        if(beds.Any(b=>b.TendDue))return "tend";
        if(beds.Any(b=>b.State=="ready"))return "harvest";
        if(beds.Any(b=>b.State=="check-maturity"))return "check";
        if(beds.Any(b=>b.State is "wet" or "keep-mature"))return "cared";
        return "none";
    }
    public static string CombineAttention(IEnumerable<string> states)
    {
        var set=states.ToHashSet();
        foreach(var priority in new[]{"dead","risk","tend","harvest","check","cared"})if(set.Contains(priority))return priority;
        return "none";
    }
    public static GardenOverviewHouse[] Houses(SharedPerson[] people, IEnumerable<SharedGardenPlan> plans)
    {
        var links = people.SelectMany(p => p.Characters.SelectMany(c => c.Houses.Select(h => (Person:p,Character:c,House:h)))).ToArray();
        return plans.GroupBy(p => p.HouseId).Select(group =>
        {
            var candidates = links.Where(l => l.House.Id == group.Key).ToArray();
            // Prefer the actual owner/FC master, then a confirmed FC member, then a shared link.
            int Rank((SharedPerson Person, SharedCharacter Character, SharedHouse House) l) =>
                string.Equals(l.Character.World,l.House.World,StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(l.House.OwnerName) && string.Equals(l.Character.Name.Trim(),l.House.OwnerName.Trim(),StringComparison.OrdinalIgnoreCase) ? 0 :
                l.House.Type == "Free Company house" && l.House.FcId.Length>0 && l.Character.FcId==l.House.FcId && string.Equals(l.Character.World,l.House.World,StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            var owner = candidates.OrderBy(Rank).ThenBy(l=>l.Person.Id).ThenBy(l=>l.Character.Id).FirstOrDefault();
            return new GardenOverviewHouse(owner.Person,owner.Character,owner.House,
                group.GroupBy(p=>p.Batch).Select(g=>g.MaxBy(p=>p.At)!).OrderBy(p=>p.Batch).ToArray(),
                candidates.Select(l=>l.Character.Name+" @ "+l.Character.World).Distinct().ToArray());
        }).ToArray();
    }

    public static string Remaining(DateTimeOffset at, DateTimeOffset now)
    {
        var minutes=(long)Math.Ceiling(Math.Max(0,(at-now).TotalMinutes));
        return minutes==0?"now":minutes>=1440?$"{minutes/1440}d {minutes%1440/60}h {minutes%60}m":$"{minutes/60}h {minutes%60}m";
    }

    public static GardenOverviewBed Bed(SharedGardenPlan plan,SharedGardenBed bed,DateTimeOffset now)
    {
        var state=GardenVisualState.For(bed,false,now);
        var crop=GardenVisualState.HasActualCrop(bed)?bed.ActualCrop:state=="empty"?"Empty":"Not identified";
        GardenOverviewBed Row(string status,string care,string harvest,bool due=false,DateTimeOffset? next=null,DateTimeOffset? ready=null) => new(bed.Bed,crop,status,care,harvest,state,due,next,ready);
        if(state=="empty")return Row("Empty","—","—");
        if(state is "ready" or "keep-mature")return Row(state=="ready"?"Ready to harvest":"Kept mature","Not needed",state=="ready"?"Ready":"Kept mature");
        if(state=="dead")return Row("Dead · confirmed","—","Remove crop");
        if(plan.CompletedAt is null && GardenPlantRequirement.SuppressTending(bed))return Row(bed.Status=="replant"?"Replace starter":"Temporary starter","Not needed","Will be replaced");
        if(state=="unknown")return Row("Awaiting sync","Check in game","Unknown");
        var first=GardenTiming.FirstTendDue(bed.Planted,bed.Watered,now);
        var next=first?bed.Planted:bed.Watered?.AddHours(12)??bed.NextTend;
        var due=next<=now || state is "wilted" or "wilt-estimated" or "at-risk" or "dead-estimated";
        var care=first?"First tend due":next is null?"No tend recorded":next<=now?"Due now":"In "+Remaining(next.Value,now);
        var harvest=bed.HarvestAt is null?"Unknown":bed.HarvestAt<=now?(GardenTiming.MaturityEstimateDue(bed.HarvestAt,bed.GrowingObservedAt,now)?"Check maturity":"Timing needs checking"):"~"+Remaining(bed.HarvestAt.Value,now);
        var status=state switch {"wet"=>"Cared for","due"=>"Tending due","wilted"=>"Wilting · confirmed","wilt-estimated"=>"Wilting · estimated","dead-estimated"=>"Death risk · check in game","at-risk"=>"At risk","check-maturity"=>"Check maturity",_=>"Growing"};
        if(plan.CompletedAt is null&&bed.Status=="different")status+=" · differs from plan";
        return Row(status,care,harvest,due,next,bed.HarvestAt);
    }

    public static string HeaderSummary(IEnumerable<SharedGardenPlan> plans,DateTimeOffset now)
    {
        var beds=plans.SelectMany(p=>p.Beds.Select(b=>Bed(p,b,now))).ToArray();
        if(beds.Length==0)return "No gardens";
        if(beds.All(b=>b.State=="empty"))return "Empty";
        var parts=new List<string>();
        void Count(string text,Func<GardenOverviewBed,bool> match){var n=beds.Count(match);if(n>0)parts.Add($"{n} {text}");}
        Count("dead",b=>b.State=="dead");
        Count("at risk",b=>b.State is "wilted" or "wilt-estimated" or "at-risk" or "dead-estimated");
        Count("replace",b=>b.Status=="Replace starter");
        Count("ready",b=>b.State=="ready");
        Count("tend due",b=>b.TendDue);
        Count("check maturity",b=>b.State=="check-maturity");
        Count("unsynced",b=>b.State=="unknown");
        var tend=beds.Where(b=>b.NextTend>now).Select(b=>b.NextTend).Min();
        var harvest=beds.Where(b=>b.HarvestAt>now).Select(b=>b.HarvestAt).Min();
        if(!beds.Any(b=>b.TendDue)&&tend is {} next)parts.Add("T "+Remaining(next,now));
        if(harvest is {} ready)parts.Add("H ~"+Remaining(ready,now));
        if(parts.Count==0)return beds.Any(b=>b.State=="keep-mature")?"Kept mature":"No care due";
        return string.Join(" · ",parts);
    }

    public static string Summary(IEnumerable<SharedGardenPlan> plans,DateTimeOffset now)
    {
        var beds=plans.SelectMany(p=>p.Beds.Select(b=>Bed(p,b,now))).ToArray();
        var parts=new List<string>();
        void Count(string text,Func<GardenOverviewBed,bool> match){var n=beds.Count(match);if(n>0)parts.Add($"{n} {text}");}
        Count("replace starter",b=>b.Status=="Replace starter");Count("dead",b=>b.State=="dead");Count("tend due",b=>b.TendDue);Count("ready",b=>b.State=="ready");Count("kept mature",b=>b.State=="keep-mature");
        Count("check maturity",b=>b.State=="check-maturity");Count("awaiting sync",b=>b.State=="unknown");Count("empty",b=>b.State=="empty");
        var next=beds.Where(b=>b.NextTend>now).Select(b=>b.NextTend).Min();
        var harvest=beds.Where(b=>b.HarvestAt>now).Select(b=>b.HarvestAt).Min();
        if(next is {} tend)parts.Add("Tend in "+Remaining(tend,now));
        if(harvest is {} ready)parts.Add("Harvest ~"+Remaining(ready,now));
        return parts.Count>0?string.Join(" · ",parts):"No care action due";
    }
}
