namespace EquinoxCompanion;

public sealed record MarketStock(int Slot, uint ItemId, string Name, bool Hq, uint Quantity, int Price, DateTimeOffset ListedAt,int DisplayOrder=int.MaxValue,string? Materia=null)
{
    public long Total => (long)Price * Quantity;
    public bool SamePricingItem(MarketStock other) => SameItem(other) && Materia==other.Materia;
    public bool SameItem(MarketStock other) => Slot==other.Slot && ItemId==other.ItemId && Hq==other.Hq && Quantity==other.Quantity;
}
public sealed record MarketRetainer(ulong Id, string Name, ulong CharacterId, string Character, uint WorldId, string World,
    string Person, string Account, uint Gil, DateTimeOffset At, MarketStock[] Stock,DateTimeOffset? LastVisit=null,int DisplayOrder=int.MaxValue,
    string PersonId="",string AccountId="",string SharedCharacterId="",bool Paired=false,uint? JobId=null,int? Level=null,string Job="",DateTimeOffset? ProfileAt=null,MarketPriceReview? PriceReview=null);
public sealed record MarketIncome(string Id, ulong CharacterId, string Character, uint WorldId, string World, ulong RetainerId,
    string Retainer, uint ItemId, string Item, bool Hq, uint Quantity, long? Gross, long? Net, DateTimeOffset At, string Evidence);
public sealed record MarketPriceLog(DateTimeOffset At, ulong RetainerId, string Retainer, uint ItemId, string Item, bool Hq,
    int Before, int? After, string Result, string Reason);
public sealed record MarketExpense(string Id, ulong CharacterId, string Character, uint WorldId, string World, uint ItemId, bool Hq, uint Quantity, long Price, long Tax, DateTimeOffset At);
public sealed record MarketLedger(MarketRetainer[] Retainers, MarketIncome[] Income, MarketPriceLog[] Prices, MarketExpense[]? Expenses=null)
{
    public static MarketLedger Empty => new([],[],[]);
}

public static class MarketAccounting
{
    public static bool VisitDue(MarketRetainer retainer,DateTimeOffset now,int days)=>retainer.Stock.Length>0&&(retainer.LastVisit??retainer.At)<=now.AddDays(-Math.Clamp(days,1,14));
    // Disappearance is not proof of a sale: withdrawals, exchanges, and missing
    // snapshots must never silently increase earnings.
    public static MarketStock[] Removed(MarketStock[] before, MarketStock[] after) =>
        before.Where(x=>!after.Any(y=>x.SameItem(y))).ToArray();
    public static MarketRetainer PreserveListingDates(MarketRetainer? before, MarketRetainer after) => after with {
        Stock=after.Stock.Select(x=>before?.Stock.FirstOrDefault(y=>y.SameItem(x)) is {} old?x with {ListedAt=old.ListedAt}:x).ToArray()
    };
    public static MarketIncome[] ReconcileOffline(MarketRetainer before,MarketRetainer after,IEnumerable<MarketIncome> receipts)
    {
        if(before.Id!=after.Id||before.CharacterId!=after.CharacterId||after.At<=before.At||after.Gil<=before.Gil)return [];
        var missing=Removed(before.Stock,after.Stock);
        if(missing.Length==0)return [];
        // A live receipt has already counted this interval. Ambiguous receipts
        // also prevent an offline estimate from counting the same sale twice.
        var known=receipts.Where(x=>x.CharacterId==before.CharacterId&&x.At>=before.At&&x.At<=after.At).ToArray();
        if(known.Any(x=>x.RetainerId==0||x.RetainerId==before.Id))return [];
        var gross=missing.Sum(x=>x.Total);var delta=(long)after.Gil-before.Gil;
        if(delta<gross-gross/20-missing.Length||delta>gross)return [];
        return missing.Select(x=>new MarketIncome($"inferred:{before.Id}:{x.Slot}:{before.At.ToUnixTimeMilliseconds()}",before.CharacterId,before.Character,before.WorldId,before.World,before.Id,before.Name,x.ItemId,x.Name,x.Hq,x.Quantity,x.Total,null,after.At,
            $"Inferred from missing listing and matching gil increase since {before.At:O}; sale time/net not confirmed")).ToArray();
    }
    public static string CsvCell(string s) => "\""+s.Replace("\"","\"\"")+"\"";
    public static string Export(IEnumerable<MarketIncome> income)
    {
        var b=new System.Text.StringBuilder("Date,Character,World,Retainer,Item,HQ,Quantity,Gross gil,Net gil,Evidence\r\n");
        foreach(var x in income) b.AppendLine(string.Join(",",new[]{x.At.ToString("O"),x.Character,x.World,x.Retainer,x.Item,x.Hq?"HQ":"NQ",x.Quantity.ToString(),x.Gross?.ToString()??"",x.Net?.ToString()??"",x.Evidence}.Select(CsvCell)));
        return b.ToString();
    }
}
