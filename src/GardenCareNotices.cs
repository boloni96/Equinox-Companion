using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong gardenNoticeCharacter;
    private DateTimeOffset nextGardenNotice;
    private readonly HashSet<string> gardenNotices = [];
    private void UpdateGardenCareNotices(DateTimeOffset now)
    {
        if (!Player.IsLoaded || Player.ContentId == 0) { gardenNoticeCharacter = 0; return; }
        if (gardenNoticeCharacter != Player.ContentId)
        {
            gardenNoticeCharacter = Player.ContentId; nextGardenNotice = now.AddSeconds(20); nextRosterRead = default; return;
        }
        if (!config.NotifyGardenCare || !config.SyncEnabled || now < nextGardenNotice || config.SharedRoster is not { } roster) return;
        nextGardenNotice = now.AddSeconds(30);
        var matches = roster.People.SelectMany(p => p.Characters).Where(c => string.Equals(c.Name, Player.CharacterName, StringComparison.OrdinalIgnoreCase) && string.Equals(c.World, WorldName(Player.HomeWorld.RowId), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (matches.Length != 1) return;
        var allPlans=GardenPlanSources();
        var notices = new List<GardenChatNotice>();
        foreach (var house in (roster.GardenCare ?? []).Where(g => g.CharacterIds.Contains(matches[0].Id)).GroupBy(g => g.HouseId))
        {
            var alerts = new List<(int Batch, string Kind, DateTimeOffset? DeathAt)>();
            foreach (var batch in house)
            {
                var source=allPlans.FirstOrDefault(p=>p.HouseId==batch.HouseId&&p.Batch==batch.Batch);
                var projection=source is null?null:EffectiveGardenPlan(source);
                var due = new List<(int Bed, string Kind, DateTimeOffset? DeathAt)>();
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
                    if(projected is not null)
                    {
                        if(projected.ActualCrop=="Empty")continue;
                        effective=effective with {Ready=projected.Ready,Watered=projected.Watered,NextTend=projected.NextTend,HarvestAt=projected.HarvestAt,WiltHours=projected.WiltHours,
                            DeadConfirmedAt=projected.DeadConfirmedAt,Planted=projected.Planted,
                            DeathAt=projected.Ready||GardenVisualState(projected,false,now)=="dead"?null:projected.Watered is {} care&&projected.WiltHours is {} wilt?care.AddHours(wilt+24):null};
                    }
                    else if (lastPlant > (bed.Watered ?? DateTimeOffset.MinValue)) effective = effective with { Ready = false, KeepMature = false, HarvestAt = null, DeathAt = null };
                    var kind = GardenCareStatus.Due(effective, now); if (kind is null || !GardenMessageEnabled(kind)) continue;
                    var left = effective.DeathAt - now;
                    var urgency = left is null ? "unknown" : left <= TimeSpan.Zero ? "risk" : left <= TimeSpan.FromHours(1) ? "1h" : left <= TimeSpan.FromHours(4) ? "4h" : "normal";
                    var key = $"{batch.HouseId}:{batch.Batch}:{bed.Bed}:{kind}:{effective.Watered:O}:{bed.HarvestAt:O}:{urgency}";
                    if (gardenNotices.Add(key)) due.Add((bed.Bed, kind, effective.DeathAt));
                }
                foreach (var group in due.GroupBy(x => x.Kind)) alerts.Add((batch.Batch, group.Key, group.Any(x => x.DeathAt is null) ? null : group.Min(x => x.DeathAt)));
            }
            var h = house.First();
            notices.AddRange(alerts.Select(x=>new GardenChatNotice(h.HouseId,h.HouseName,x.Batch,x.Kind,x.DeathAt)));
        }
        if(notices.Count==0)return;
        var summary=new SeStringBuilder().AddText("[Equinox] ");
        var segments=GardenCareStatus.ChatSummary(notices,now);
        for(var i=0;i<segments.Length;i++){
            if(i>0)summary.AddText(" · ");
            summary.AddUiForeground(segments[i].Color).AddText(segments[i].Text).AddUiForegroundOff();
        }
        if(notices.Select(x=>x.HouseId).Distinct().Count()>1)summary.AddText(" · /equinox for details");
        Chat.Print(summary.Build());
    }
}
