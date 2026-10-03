namespace EquinoxCompanion;
public static class GardenVisualState
{
    public static string For(SharedGardenBed b,bool planVisible,DateTimeOffset now)
    {
        if(planVisible&&b.Crop.Length>0&&b.Status is not ("confirmed" or "starter"))return "planned";
        if(b.Ready)return planVisible&&b.Crop.Length==0?"keep-mature":"ready";
        if(b.ActualCrop=="Empty")return "empty";
        var death=b.Watered is {} w&&b.WiltHours is {} hours?w.AddHours(hours+24):(DateTimeOffset?)null;
        if(GardenTiming.DeathRisk(b.Ready,death,b.HarvestAt,now))return "dead-estimated";
        if(b.HarvestAt<=now)return "check-maturity";
        if(death is {} risk&&risk>now&&risk-now<=TimeSpan.FromHours(4))return "at-risk";
        if(b.Watered is {} care&&b.WiltHours is {} wilt&&care.AddHours(wilt)<=now)return "wilt-estimated";
        if(b.Watered is {} water&&water<=now)return now-water<TimeSpan.FromHours(12)?"wet":"due";
        if(b.NextTend<=now)return "due";
        return b.ActualCrop.Length==0||b.ActualCrop=="Not synced yet"?"unknown":"growing";
    }
}
