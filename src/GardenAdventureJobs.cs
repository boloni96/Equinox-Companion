namespace EquinoxCompanion;
public sealed record GardenTender(string Id,int Region);
public static class GardenAdventureJobs
{
    public static bool FullBatchHarvest(SharedGardenPlan plan, AdventureTaskRule rule, bool enabled, DateTimeOffset now)
    {
        var scope = rule.Jobs ?? new[] { rule.Kind };
        if (!enabled || !(rule.Kind == "growingcycle" || scope.Contains("harvest") || rule.Kind == "garden" && rule.Jobs == null)) return false;
        var active = plan.Beds.Where(GardenVisualState.HasActualCrop).ToArray();
        return active.Length > 0 && active.All(b => GardenCarePolicy.MatureForCare(b, now))
            && active.Any(b => GardenCarePolicy.Eligible(plan, b, GardenCareAction.Harvest, now));
    }
    public static GardenCareAction? Action(SharedGardenPlan plan, AdventureTaskRule rule, bool plant, bool harvest, bool tend, DateTimeOffset now)
    {
        if (FullBatchHarvest(plan, rule, harvest, now)) return GardenCareAction.Harvest;
        var scope = rule.Jobs ?? new[] { rule.Kind };
        var cycle = rule.Kind == "growingcycle";
        if (plant && !(cycle && rule.Phase is "waiting" or "review") &&
            (cycle || scope.Contains("planplant") || scope.Contains("plant") || rule.Kind == "garden" && rule.Jobs == null))
        {
            // An occupied or invalid next step blocks only planting in this batch.
            try { if (GardenAutoPlant.Next(plan) != null) return GardenCareAction.Plant; }
            catch (InvalidOperationException) { }
        }
        if (tend && AllowsTending(rule) && plan.Beds.Any(b => GardenCarePolicy.Eligible(plan,b,GardenCareAction.Tend,now)))
            return GardenCareAction.Tend;
        return null;
    }
    public static bool AllowsTending(AdventureTaskRule rule)=>rule.Kind=="growingcycle"||
        rule.Kind=="care"||rule.Kind=="garden"&&(rule.Jobs==null||rule.Jobs.Contains("care"));
    public static bool InspectionDue(AdventureTaskRule rule,bool enabled,bool unknownBeds,DateTimeOffset lastInspection,DateTimeOffset now)
    {
        var scope=rule.Jobs??new[]{rule.Kind};
        return enabled&&(unknownBeds||scope.Contains("inspect")||scope.Contains("inspecthouse"))&&
            (lastInspection==default||now-lastInspection>=TimeSpan.FromMinutes(10));
    }
    public static bool NeedsAssignedWork(AdventureTaskRule rule,bool plant,bool harvest)
    {
        var all=rule.Kind=="growingcycle"||rule.Kind=="garden"&&rule.Jobs==null;
        var jobs=rule.Jobs??new[]{rule.Kind};
        return plant&&(all||jobs.Contains("plant")||jobs.Contains("planplant"))||harvest&&(all||jobs.Contains("harvest"));
    }
    // Preserve the user's first-visit order, but finish the active plot and each
    // gardener's work together instead of alternating plots or login characters.
    public static T[] OrderForVisit<T>(IEnumerable<T> work,Func<T,string> house,Func<T,string> character,string currentHouse,string currentCharacter)=>
        work.GroupBy(house).OrderBy(g=>g.Key==currentHouse?0:1)
            .SelectMany(g=>g.GroupBy(character).OrderBy(c=>c.Key==currentCharacter?0:1).SelectMany(c=>c)).ToArray();
    public static bool ReuseArrival(string visitedHouse,ulong visitedCharacter,string nextHouse,ulong currentCharacter,bool exactOutdoorPlot)=>
        exactOutdoorPlot&&visitedHouse.Length>0&&visitedHouse==nextHouse&&visitedCharacter!=0&&visitedCharacter==currentCharacter;
    // Candidates are already restricted to the starting person and verified login.
    // Region is the character's home region: visitors can return home from Materia.
    public static bool Reachable(int homeRegion,int destination)=>homeRegion is >=1 and <=4&&
        destination is >=1 and <=4&&(homeRegion==destination||destination==4);
    public static string Tender(string main,string assigned,int destination,IEnumerable<GardenTender> candidates,bool preferAssigned=false)
    {
        if(destination is <1 or >4)return "";
        var reachable=candidates.Where(c=>Reachable(c.Region,destination)).ToArray();
        if(preferAssigned&&reachable.FirstOrDefault(c=>c.Id==assigned) is {} gardener)return gardener.Id;
        return reachable.FirstOrDefault(c=>c.Id==main)?.Id??reachable.FirstOrDefault(c=>c.Id==assigned)?.Id??reachable.FirstOrDefault()?.Id??"";
    }
}

