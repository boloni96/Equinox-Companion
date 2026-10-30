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
    // Candidates are already restricted to the starting person and verified login.
    // Region is the character's home region: visitors can return home from Materia.
    public static bool Reachable(int homeRegion,int destination)=>homeRegion is >=1 and <=4&&
        destination is >=1 and <=4&&(homeRegion==destination||destination==4);
    public static string Tender(string main,string assigned,int destination,IEnumerable<GardenTender> candidates)
    {
        if(destination is <1 or >4)return "";
        var reachable=candidates.Where(c=>Reachable(c.Region,destination)).ToArray();
        return reachable.FirstOrDefault(c=>c.Id==main)?.Id??reachable.FirstOrDefault(c=>c.Id==assigned)?.Id??reachable.FirstOrDefault()?.Id??"";
    }
}
