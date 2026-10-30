namespace EquinoxCompanion;
public sealed record GardenTender(string Id,int Region);
public static class GardenAdventureJobs
{
    public static bool AllowsTending(AdventureTaskRule rule)=>rule.Kind=="growingcycle"||
        rule.Kind=="care"||rule.Kind=="garden"&&(rule.Jobs==null||rule.Jobs.Contains("care"));
    public static bool InspectionDue(AdventureTaskRule rule,bool enabled,bool unknownBeds,DateTimeOffset lastInspection,DateTimeOffset now)
    {
        var scope=rule.Jobs??new[]{rule.Kind};
        return enabled&&(unknownBeds||scope.Contains("inspect")||scope.Contains("inspecthouse"))&&
            (lastInspection==default||now-lastInspection>=TimeSpan.FromMinutes(10));
    }
    // Caller supplies only characters from the starting person/account. Prefer known
    // same-region travel; no inferred cross-region or alternate service-account login.
    public static string Tender(string main,string assigned,int destination,IEnumerable<GardenTender> candidates)
    {
        if(destination is <1 or >4)return "";
        var reachable=candidates.Where(c=>c.Region==destination).ToArray();
        return reachable.FirstOrDefault(c=>c.Id==main)?.Id??reachable.FirstOrDefault(c=>c.Id==assigned)?.Id??reachable.FirstOrDefault()?.Id??"";
    }
}
