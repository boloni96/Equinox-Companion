namespace EquinoxCompanion;
public sealed record PreparedUpload(SyncEvent[] Pending,SyncEvent[] Held,int HistoricalHeld,int Unsupported,int Eligible,SyncEvent[] Events);
public static class UploadPreparation
{
    public static PreparedUpload Build(SyncEvent[] candidates,SyncEvent[] characters,DateTimeOffset now,int protocol,bool historyFirst,Func<SyncEvent[],int> bytes)
    {
        var updates=characters.GroupBy(e=>e.Actor.ContentId).ToDictionary(g=>g.Key,g=>g.ToArray());
        var pending=candidates.Where(e=>!SyncValidation.SupersededIncompleteCharacter(e,updates.GetValueOrDefault(e.Actor.ContentId)??[],now)).ToArray();
        var held=pending.Where(e=>!SyncValidation.CanSend(e,now)).ToArray();
        var valid=pending.Where(e=>SyncValidation.CanSend(e,now)).ToArray();
        var eligible=valid.Where(e=>SyncValidation.SupportedByWebsite(e,protocol)).ToArray();
        return new(pending,held,held.Count(e=>SyncValidation.HistoricalCompanyPlaceholder(e,now)),valid.Length-eligible.Length,eligible.Length,SyncQueue.Select(eligible,bytes,historyFirst));
    }
}
