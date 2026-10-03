namespace EquinoxCompanion;
public static class GardenVisualState
{
    public static bool HasActualCrop(SharedGardenBed b) => !string.IsNullOrWhiteSpace(b.ActualCrop) && b.ActualCrop is not ("Empty" or "Not synced yet" or "Crop not identified");
    public static string DisplayCrop(SharedGardenBed b,bool planVisible) => HasActualCrop(b)?b.ActualCrop:planVisible&&b.ActualCrop is "Empty" or "Not synced yet"?b.Crop:b.ActualCrop;
    // Illustrative first 33% of the recorded growth estimate, not an observed game stage.
    public static bool SeedlingEstimate(SharedGardenBed b,DateTimeOffset now) => HasActualCrop(b) && For(b,false,now) is "wet" or "due" or "growing" && !b.Ready && b.DeadConfirmedAt is null && b.Planted is {} start && b.HarvestAt is {} end && end>start && now>=start && now<start+(end-start)*.33;
    public static double? GrowthPercent(SharedGardenBed b,DateTimeOffset now) => b.Planted is {} start&&b.HarvestAt is {} end&&end>start&&!(b.GrowingObservedAt is {} observed&&observed<=now&&observed>=end)?Math.Clamp((now-start).TotalSeconds/(end-start).TotalSeconds*100,0,100):null;
    public static bool EffectVisible(DateTimeOffset now) => now.ToUnixTimeSeconds()%2==0;
    public static string SoilArtwork(SharedGardenBed b,bool planVisible,DateTimeOffset now)
    {
        var state=For(b,planVisible,now);
        if(state is "dead" or "dead-estimated")return "dead";
        return state is not ("planned" or "empty" or "wilted") && b.Watered is {} water && water<=now && now-water<TimeSpan.FromHours(12) && !GardenTiming.FirstTendDue(b.Planted,b.Watered,now)?"wet":"normal";
    }
    public static bool NeedsWater(SharedGardenBed b,bool planVisible,DateTimeOffset now) => !(planVisible&&GardenPlantRequirement.SuppressTending(b)) && For(b,false,now) is "due" or "wilted" or "wilt-estimated" or "at-risk";
    public static string CropArtwork(SharedGardenBed b,bool planVisible,DateTimeOffset now,bool animate=false) => For(b,planVisible,now) switch
    {
        "planned" => "plantLive",
        "ready" or "keep-mature" => "plantMature",
        "dead" or "dead-estimated" => "plantDead",
        "wilt-estimated" or "wilted" or "at-risk" => "plantWilted",
        _ => SeedlingEstimate(b,now)?"plantSeedling":"plantGrowing"
    };
    public static string For(SharedGardenBed b,bool planVisible,DateTimeOffset now)
    {

        if(b.DeadConfirmedAt is {} deadAt && deadAt<=now && (b.Watered is null||b.Watered<=deadAt) && (b.Planted is null||b.Planted<=deadAt) && b.ActualCrop!="Empty" && !b.Ready)return "dead";
        if(planVisible&&!HasActualCrop(b)&&!b.Ready&&b.Crop.Length>0&&b.Status is not ("confirmed" or "starter"))return "planned";
        if(b.Ready)return b.KeepMature?"keep-mature":"ready";
        if(b.ActualCrop=="Empty")return "empty";
        if(b.WiltedAt is {} wilted&&wilted<=now&&(b.Watered is null||b.Watered<=wilted)&&(b.Planted is null||b.Planted<=wilted))return "wilted";
        var death=b.Watered is {} w&&b.WiltHours is {} hours?w.AddHours(hours+24):(DateTimeOffset?)null;
        if(!(death<=b.GrowingObservedAt)&&GardenTiming.DeathRisk(b.Ready,death,b.HarvestAt,now))return "dead-estimated";
        if(GardenTiming.MaturityEstimateDue(b.HarvestAt,b.GrowingObservedAt,now))return "check-maturity";
        if(death is {} risk&&risk>now&&risk-now<=TimeSpan.FromHours(4))return "at-risk";
        if(b.Watered is {} care&&b.WiltHours is {} wilt&&care.AddHours(wilt)<=now&&!(care.AddHours(wilt)<=b.GrowingObservedAt))return "wilt-estimated";
        if(HasActualCrop(b)&&GardenTiming.FirstTendDue(b.Planted,b.Watered,now))return "due";
        if(b.Watered is {} water&&water<=now)return now-water<TimeSpan.FromHours(12)?"wet":"due";
        if(b.NextTend<=now)return "due";
        return b.ActualCrop.Length==0||b.ActualCrop is "Not synced yet" or "Crop not identified"?"unknown":"growing";
    }
}
