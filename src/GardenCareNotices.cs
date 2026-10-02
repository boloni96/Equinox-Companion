namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong gardenNoticeCharacter;
    private DateTimeOffset nextGardenNotice;
    private readonly HashSet<string> gardenNotices = [];
    private void UpdateGardenCareNotices(DateTimeOffset now)
    {
        if (!Player.IsLoaded || Player.ContentId == 0) { gardenNoticeCharacter = 0; gardenNotices.Clear(); return; }
        if (gardenNoticeCharacter != Player.ContentId)
        {
            gardenNoticeCharacter = Player.ContentId; gardenNotices.Clear(); nextGardenNotice = now.AddSeconds(20); nextRosterRead = default; return;
        }
        if (!config.NotifyGardenCare || !config.SyncEnabled || now < nextGardenNotice || config.SharedRoster is not { } roster) return;
        nextGardenNotice = now.AddSeconds(30);
        var matches = roster.People.SelectMany(p => p.Characters).Where(c => string.Equals(c.Name, Player.CharacterName, StringComparison.OrdinalIgnoreCase) && string.Equals(c.World, WorldName(Player.HomeWorld.RowId), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (matches.Length != 1) return;
        foreach (var house in (roster.GardenCare ?? []).Where(g => g.CharacterIds.Contains(matches[0].Id)).GroupBy(g => g.HouseId))
        {
            var alerts = new List<(int Batch, string Kind, DateTimeOffset? DeathAt)>();
            foreach (var batch in house)
            {
                var due = new List<(int Bed, string Kind, DateTimeOffset? DeathAt)>();
                foreach (var bed in batch.Beds)
                {
                    // Local confirmed care already happened, even if the browser has not saved it yet.
                    var lastPlant = config.Planting.Where(t => t.Address.HouseId == batch.GameHouseId && t.Patch == batch.Batch && t.Bed == bed.Bed).Select(t => t.ConfirmedAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                    var lastTend = config.Tending.Where(t => t.Address.HouseId == batch.GameHouseId && t.Patch == batch.Batch && t.Bed == bed.Bed).Select(t => t.ConfirmedAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                    var localCare = lastPlant > lastTend ? lastPlant : lastTend;
                    var empty = config.Discoveries.Where(e => e.Kind == "garden.empty" && e.Address?.HouseId == batch.GameHouseId && e.Patch == batch.Batch && e.Bed == bed.Bed).Select(e => e.At).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                    if (empty > (bed.Watered ?? DateTimeOffset.MinValue) && empty >= localCare) continue;
                    var effective = localCare > (bed.Watered ?? DateTimeOffset.MinValue) ? bed with { Watered = localCare, NextTend = localCare.AddHours(12), DeathAt = bed.WiltHours is > 0 ? localCare.AddHours(bed.WiltHours.Value+24) : null } : bed;
                    if (lastPlant > (bed.Watered ?? DateTimeOffset.MinValue)) effective = effective with { Ready = false, KeepMature = false, HarvestAt = null, DeathAt = null };
                    var kind = GardenCareStatus.Due(effective, now); if (kind is null || !GardenMessageEnabled(kind)) continue;
                    var left = effective.DeathAt - now;
                    var urgency = left is null ? "unknown" : left <= TimeSpan.Zero ? "risk" : left <= TimeSpan.FromHours(1) ? "1h" : left <= TimeSpan.FromHours(4) ? "4h" : "normal";
                    var key = $"{batch.HouseId}:{batch.Batch}:{bed.Bed}:{kind}:{effective.Watered:O}:{bed.HarvestAt:O}:{urgency}";
                    if (gardenNotices.Add(key)) due.Add((bed.Bed, kind, effective.DeathAt));
                }
                foreach (var group in due.GroupBy(x => x.Kind)) alerts.Add((batch.Batch, group.Key, group.Any(x => x.DeathAt is null) ? null : group.Min(x => x.DeathAt)));
            }
            var h = house.First();
            foreach (var group in alerts.GroupBy(x => x.Kind))
                Chat.Print(GardenCareStatus.BatchMessage(h.HouseName, group.Key, group.Select(x => (x.Batch,x.DeathAt)), now));

        }
    }
}
