using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private DateTimeOffset nextMaintenance;
    private void MaintainRecords(DateTimeOffset now)
    {
        if (now < nextMaintenance) return;
        nextMaintenance = now.AddMinutes(5);
        var cutoff = now.AddDays(-60);
        var sent = config.SentEvents.ToHashSet();
        // Preserve current state for each entity and every unsent action, regardless of age.
        var latestHouse = config.Houses.GroupBy(x => (x.Actor.ContentId,x.Address.HouseId,x.Kind)).Select(g => g.MaxBy(x=>x.ObservedAt)!.EventId).ToHashSet();
        var latestTend = config.Tending.GroupBy(x => (x.Actor.ContentId,x.Address.HouseId,x.Patch,x.Bed)).Select(g => g.MaxBy(x=>x.ConfirmedAt)!.EventId).ToHashSet();
        var latestPlant = config.Planting.GroupBy(x => (x.Actor.ContentId,x.Address.HouseId,x.Patch,x.Bed)).Select(g => g.MaxBy(x=>x.ConfirmedAt)!.EventId).ToHashSet();
        var latestDetail = config.Discoveries.GroupBy(x => (x.Actor.ContentId,x.Kind,x.Address?.HouseId,x.Patch,x.Bed,x.Collection?.Category,x.GardenTarget?.Argument)).Select(g => g.MaxBy(x=>x.At)!.Id).ToHashSet();
        var removed = config.Houses.RemoveAll(x => x.ObservedAt < cutoff && sent.Contains(x.EventId) && !latestHouse.Contains(x.EventId));
        removed += config.Tending.RemoveAll(x => x.ConfirmedAt < cutoff && sent.Contains(x.EventId) && !latestTend.Contains(x.EventId));
        removed += config.Planting.RemoveAll(x => x.ConfirmedAt < cutoff && sent.Contains(x.EventId) && !latestPlant.Contains(x.EventId));
        removed += config.Discoveries.RemoveAll(x => x.At < cutoff && sent.Contains(x.Id) && !latestDetail.Contains(x.Id));
        if (removed > 0)
        {
            var retained = config.Houses.Select(x => x.EventId).Concat(config.Tending.Select(x => x.EventId))
                .Concat(config.Planting.Select(x => x.EventId)).Concat(config.Discoveries.Select(x => x.Id)).ToHashSet();
            config.SentEvents.RemoveAll(id => !retained.Contains(id));
            Pi.SavePluginConfig(config);
        }
        if (!config.NotifyHousingWarnings || !Player.IsLoaded || config.SharedRoster is null) return;
        var chars=config.SharedRoster.People.SelectMany(x=>x.Characters).ToArray();
        var eligible=chars.SelectMany(c=>c.Houses.Where(h=>SharedHousingEligibility.CountsForCharacter(c,h,chars))).DistinctBy(h=>h.Id);
        foreach (var h in eligible)
        {
            if (h.Paused || h.LastEntry is not {} at || at > now || now-at < TimeSpan.FromDays(30)) continue;
            if (config.HousingWarnings.TryGetValue(h.Id,out var warned) && now-warned < TimeSpan.FromDays(1)) continue;
            Chat.Print(new SeStringBuilder().AddUiForeground(17).AddText($"[Equinox] Housing reminder: {h.Name} · {h.World} {h.District} W{h.Ward} P{h.Plot} — {(int)(now-at).TotalDays} days since the last eligible recorded entry. Enter with the private owner or an FC member; confirm the timer in game.").AddUiForegroundOff().Build());
            config.HousingWarnings[h.Id]=now;
            Pi.SavePluginConfig(config);
        }
    }
}
