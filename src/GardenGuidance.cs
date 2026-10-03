namespace EquinoxCompanion;
public static class GardenGuidance
{
    public static bool ValidOrder(SharedGardenPlan plan)
    {
        var beds=plan.Beds.Where(b=>b.Crop.Length>0).OrderBy(b=>b.Order).ToArray();
        if(beds.Length==1)return true;
        if(beds.Length==0||beds.Any(b=>b.Order<=0)||beds.Select(b=>b.Order).Distinct().Count()!=beds.Length)return false;
        return beds.Length==1 || beds[0].Bed==1&&beds[0].Order==1;
    }
    public static SharedGardenBed? Next(SharedGardenPlan plan)=>!ValidOrder(plan)||plan.CompletedAt is not null?null:
        plan.Beds.Where(b=>b.Crop.Length>0&&b.Status is not ("confirmed" or "starter"))
        .OrderBy(b=>b.Status=="replant"?b.ReplantOrder:b.Order).FirstOrDefault();
}
