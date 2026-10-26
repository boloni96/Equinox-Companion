namespace EquinoxCompanion;

// Display/check projection only. Never write remote rows into the local ledger
// or outbox: a download must not become a new observation or sale receipt.
public static class PairedMarketView
{
    public static MarketRetainer[] Build(MarketRetainer[] local, SharedRoster? roster, Func<string,uint> worldId, DateTimeOffset now)
    {
        var identities=(roster?.People??[]).SelectMany(p=>p.Characters.Select(c=>(Person:p,Character:c)))
            .Where(x=>ulong.TryParse(x.Character.ContentId,out var id)&&id>0).GroupBy(x=>x.Character.ContentId)
            .Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.Single());
        MarketRetainer Identity(MarketRetainer r)
        {
            if(!identities.TryGetValue(r.CharacterId.ToString(System.Globalization.CultureInfo.InvariantCulture),out var pair)||
                !string.Equals(pair.Character.World,r.World,StringComparison.OrdinalIgnoreCase))return r;
            return r with {Character=pair.Character.Name,Person=pair.Person.Name,Account=pair.Character.Account,
                PersonId=pair.Person.Id,AccountId=SharedCharacterGrouping.AccountKey(pair.Character),SharedCharacterId=pair.Character.Id};
        }
        var candidates=local.Select(Identity).ToList();
        if(roster is {MarketProjectionVersion:>=1})foreach(var pair in identities.Values)
        {
            var c=pair.Character;var world=worldId(c.World);
            if(world==0||c.RetainerMarket?.Retainers is not {Length:>0 and <=10} rows)continue;
            foreach(var r in rows)
            {
                if(!RetainerMarketSnapshot.Valid(new([r]),now)||!ulong.TryParse(r.Id,out var id))continue;
                candidates.Add(new(id,r.Name,ulong.Parse(c.ContentId),c.Name,world,c.World,pair.Person.Name,c.Account,r.Gil,r.ObservedAt,
                    r.Items.Select(i=>new MarketStock(i.Slot,i.ItemId,i.Name,i.Hq,i.Quantity,i.Price,i.ListedAt,i.Slot)).ToArray(),
                    r.LastVisit,r.Order,pair.Person.Id,SharedCharacterGrouping.AccountKey(c),c.Id,true,r.JobId,r.Level,r.Job,r.ProfileAt));
            }
        }
        return candidates.GroupBy(r=>r.Id).Where(g=>g.Select(r=>(r.CharacterId,r.WorldId)).Distinct().Count()==1)
            .Select(g=>{
                var stock=g.OrderByDescending(r=>r.At).ThenBy(r=>r.Paired).First();
                var profile=g.Where(r=>r.ProfileAt!=null).OrderByDescending(r=>r.ProfileAt).FirstOrDefault();
                return stock with {DisplayOrder=profile?.DisplayOrder??stock.DisplayOrder,LastVisit=g.Max(r=>r.LastVisit),JobId=profile?.JobId??stock.JobId,Level=profile?.Level??stock.Level,Job=profile?.Job??stock.Job,ProfileAt=profile?.ProfileAt??stock.ProfileAt};
            })
            .OrderBy(r=>r.Person).ThenBy(r=>r.PersonId).ThenBy(r=>r.Account).ThenBy(r=>r.AccountId)
            .ThenBy(r=>r.Character).ThenBy(r=>r.World).ThenBy(r=>r.CharacterId).ThenBy(r=>r.DisplayOrder).ThenBy(r=>r.Id).ToArray();
    }
    public static bool Same(MarketRetainer[] a,MarketRetainer[] b)=>a.Length==b.Length&&a.Zip(b).All(x=>
        x.First with {Stock=x.Second.Stock}==x.Second&&x.First.Stock.SequenceEqual(x.Second.Stock));
    public static string PersonKey(MarketRetainer r)=>r.PersonId.Length>0?r.PersonId:"local:"+r.Person;
    public static string AccountKey(MarketRetainer r)=>PersonKey(r)+"/"+(r.AccountId.Length>0?r.AccountId:"local:"+r.Account);
}
