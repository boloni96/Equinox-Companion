using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nextGardenNotice;
    private readonly GardenNoticeSession gardenNotices = new();
    private void OnGardenNoticeLogin() => gardenNotices.Login();
    private void UpdateGardenCareNotices(DateTimeOffset now)
    {
        if (!Player.IsLoaded || Player.ContentId == 0) return;
        if (gardenNotices.ObserveCharacter(Player.ContentId))
        {
            nextGardenNotice = now.AddSeconds(20); nextRosterRead = default; return;
        }
        if (!config.NotifyGardenCare || !config.SyncEnabled || now < nextGardenNotice || config.SharedRoster is not { } roster) return;
        nextGardenNotice = now.AddSeconds(30);
        if (roster.GardenCare is null || !gardenNotices.TrySummary()) return;
        var allPlans=GardenPlanSources();
        var notices = new List<GardenPersonNotice>();
        foreach (var house in roster.GardenCare.GroupBy(g => g.HouseId))
        {
            var person = GardenLoginSummary.Person(roster.People, house.First());
            if (person is null) continue;
            foreach (var batch in house)
            {
                var source=allPlans.FirstOrDefault(p=>p.HouseId==batch.HouseId&&p.Batch==batch.Batch);
                var projection=source is null?null:EffectiveGardenPlan(source);
                foreach (var bed in batch.Beds)
                {
                    // Local confirmed care already happened, even if the browser has not saved it yet.
                    var lastPlant = config.Planting.Where(t => t.Address.HouseId == batch.GameHouseId && t.Patch == (batch.PhysicalPatch>0?batch.PhysicalPatch:batch.Batch) && t.Bed == bed.Bed).Select(t => t.ConfirmedAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                    var lastTend = config.Tending.Where(t => t.Address.HouseId == batch.GameHouseId && t.Patch == (batch.PhysicalPatch>0?batch.PhysicalPatch:batch.Batch) && t.Bed == bed.Bed).Select(t => t.ConfirmedAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                    var localCare = lastPlant > lastTend ? lastPlant : lastTend;
                    var empty = config.Discoveries.Where(e => e.Kind == "garden.empty" && e.Address?.HouseId == batch.GameHouseId && e.Patch == (batch.PhysicalPatch>0?batch.PhysicalPatch:batch.Batch) && e.Bed == bed.Bed).Select(e => e.At).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                    if (empty > (bed.Watered ?? DateTimeOffset.MinValue) && empty >= localCare) continue;
                    var effective = localCare > (bed.Watered ?? DateTimeOffset.MinValue) ? bed with { Watered = localCare, NextTend = localCare.AddHours(12), DeathAt = bed.WiltHours is > 0 ? localCare.AddHours(bed.WiltHours.Value+24) : null } : bed;
                    var projected=projection?.Beds.FirstOrDefault(b=>b.Bed==bed.Bed);
                    if(projection?.CompletedAt is null&&projected is not null&&GardenPlantRequirement.SuppressTending(projected))continue;
                    if(projected is not null)
                    {
                        if(projected.ActualCrop=="Empty")continue;
                        effective=effective with {Empty=false,Ready=projected.Ready,KeepMature=projected.KeepMature,Watered=projected.Watered,NextTend=projected.NextTend,HarvestAt=projected.HarvestAt,WiltHours=projected.WiltHours,
                            DeadConfirmedAt=projected.DeadConfirmedAt,WiltedAt=projected.WiltedAt,Planted=projected.Planted,GrowingObservedAt=projected.GrowingObservedAt,
                            DeathAt=projected.Ready||GardenVisualState(projected,false,now)=="dead"?null:projected.Watered is {} care&&projected.WiltHours is {} wilt?care.AddHours(wilt+24):null};
                    }
                    else if (lastPlant > (bed.Watered ?? DateTimeOffset.MinValue)) effective = effective with { Ready = false, KeepMature = false, HarvestAt = null, DeathAt = null };
                    var kind = GardenLoginSummary.Kind(effective, now);
                    if (kind == "tend" && config.NotifyGardenTending || kind == "check" && config.NotifyGardenRisk)
                        notices.Add(new(person.Id, person.Name, kind!));
                }
            }
        }
        foreach(var message in GardenLoginSummary.Messages(notices))
            Chat.Print(new SeStringBuilder().AddText("[Equinox] ").AddUiForeground(message.Color).AddText(message.Text).AddUiForegroundOff().Build());
    }
}
