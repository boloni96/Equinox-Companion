namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private MarketRetainer[]? marketJournalSource;
    private readonly Dictionary<ulong,MarketRetainer[]> marketJournalGroups=[];
    private readonly LinkedList<ulong> marketJournalPending=new();
    private DateTimeOffset marketJournalNext;
    private void UpdateMarketJournal(DateTimeOffset now)
    {
        if(!config.Seller.SyncToJournal||!config.SyncEnabled||config.PairingKey.Length!=64||syncTask!=null||now<marketJournalNext)return;
        marketJournalNext=now.AddSeconds(5);
        var scope=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.PairingKey)));
        if(config.MarketJournal.Scope(scope)){WakeObservationSync();SaveConfiguration();}
        if(!ReferenceEquals(marketJournalSource,marketLedger.Retainers)){
            marketJournalSource=marketLedger.Retainers;
            foreach(var group in marketLedger.Retainers.GroupBy(r=>r.CharacterId)){
                var next=group.OrderBy(r=>r.DisplayOrder).ThenBy(r=>r.Id).Take(10).ToArray();
                if(marketJournalGroups.TryGetValue(group.Key,out var before)&&before.SequenceEqual(next))continue;
                marketJournalGroups[group.Key]=next;
                // Refresh the active character first without restarting the cached
                // roster: frequent live visits must not starve offline characters.
                if(Player.IsLoaded&&group.Key==Player.ContentId){marketJournalPending.Remove(group.Key);marketJournalPending.AddFirst(group.Key);}
                else if(!marketJournalPending.Contains(group.Key))marketJournalPending.AddLast(group.Key);
            }
        }
        if(marketJournalPending.First is not {} pending)return;
        var rows=marketJournalGroups[pending.Value];marketJournalPending.RemoveFirst();if(rows.Length==0)return;
        var first=rows.OrderByDescending(r=>r.At).First();if(rows.Any(r=>r.WorldId!=first.WorldId))return;
        var details=new RetainerMarketDetails(rows.Select((r,i)=>new RetainerMarketRow(r.Id.ToString(),r.Name,i,r.Gil,r.At,r.LastVisit,
            r.Stock.OrderBy(x=>x.Slot).Select(x=>new RetainerMarketItem(x.Slot,x.ItemId,x.Name,x.Hq,x.Quantity,x.Price,x.ListedAt)).ToArray(),r.JobId,r.Level,r.Job,r.ProfileAt)).ToArray());
        var actor=new Actor(first.CharacterId.ToString(),first.Character,first.WorldId,first.WorldId,first.World,first.World);
        if(config.MarketJournal.Put(actor,details,now)){WakeObservationSync();SaveConfiguration();}
    }
}
