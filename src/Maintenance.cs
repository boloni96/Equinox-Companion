using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private DateTimeOffset nextMaintenance;
    private Task<ObservationPrunePlan>? retentionTask;
    private string retentionPairing="";
    private void MaintainRecords(DateTimeOffset now)
    {
        if(retentionTask?.IsCompleted==true)
        {
            if(retentionTask.IsCompletedSuccessfully&&retentionPairing==config.PairingKey)
            {
                var plan=retentionTask.Result;
                RefreshObservationIndex();
                // A receipt reset while cleanup ran must keep those actions for replay.
                plan.Houses.RemoveWhere(id=>!observations.Acknowledged(id));plan.Tending.RemoveWhere(id=>!observations.Acknowledged(id));
                plan.Planting.RemoveWhere(id=>!observations.Acknowledged(id));plan.Discoveries.RemoveWhere(id=>!observations.Acknowledged(id));
                if(plan.Count>0)
                {
                    config.Houses.RemoveAll(x=>plan.Houses.Contains(x.EventId));
                    config.Tending.RemoveAll(x=>plan.Tending.Contains(x.EventId));
                    config.Planting.RemoveAll(x=>plan.Planting.Contains(x.EventId));
                    config.Discoveries.RemoveAll(x=>plan.Discoveries.Contains(x.Id));
                    var removed=plan.Houses.Concat(plan.Tending).Concat(plan.Planting).Concat(plan.Discoveries).ToHashSet();
                    config.SentEvents.RemoveAll(removed.Contains);observations.Invalidate();SaveConfiguration();
                }
            }
            else if(retentionTask.IsFaulted)errorJournal.Record("retention","History cleanup deferred; all records retained.",exceptionType:retentionTask.Exception?.GetBaseException().GetType().Name);
            retentionTask=null;
        }
        if (now < nextMaintenance) return;
        nextMaintenance = now.AddMinutes(5);
        if(retentionTask is null)
        {
            var houses=config.Houses.ToArray();var tends=config.Tending.ToArray();var plants=config.Planting.ToArray();var discoveries=config.Discoveries.ToArray();var sent=config.SentEvents.ToArray();
            retentionPairing=config.PairingKey;
            retentionTask=Task.Run(()=>ObservationRetention.Plan(houses,tends,plants,discoveries,sent,now.AddDays(-60)));
        }
        if (!config.NotifyHousingWarnings || !Player.IsLoaded || config.SharedRoster is null) return;
        var chars=config.SharedRoster.People.SelectMany(x=>x.Characters).ToArray();
        var eligible=chars.SelectMany(c=>c.Houses.Where(h=>SharedHousingEligibility.CountsForCharacter(c,h,chars))).DistinctBy(h=>h.Id);
        foreach (var h in eligible)
        {
            if (h.Paused || h.LastEntry is not {} at || at > now || now-at < TimeSpan.FromDays(30)) continue;
            if (config.HousingWarnings.TryGetValue(h.Id,out var warned) && now-warned < TimeSpan.FromDays(1)) continue;
            PrintEquinox(new SeStringBuilder().AddUiForeground(17).AddText($"[Equinox] Housing reminder: {h.Name} · {h.World} {h.District} W{h.Ward} P{h.Plot} — {(int)(now-at).TotalDays} days since the last eligible recorded entry. Enter with the private owner or an FC member; confirm the timer in game.").AddUiForegroundOff().Build());
            config.HousingWarnings[h.Id]=now;
            SaveConfiguration();
        }
    }
}
