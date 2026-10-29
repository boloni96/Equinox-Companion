namespace EquinoxCompanion;
internal sealed record WithdrawalStock(ulong Retainer,uint Item,int Quantity,bool Hq);
internal sealed record WithdrawalOrder(ulong[] Retainers,int KnownHolders);
internal static class WithdrawalRetainerOrder
{
    internal static WithdrawalOrder Build(ulong[] owned,IEnumerable<WithdrawalStock> stock,
        RetrievalEntry[] plan,Func<uint,int> held,bool subtractHeld,RetrievalQuality quality)
    {
        var needed=plan.Where(e=>e.Quantity>(subtractHeld?held(e.ItemId):0)).Select(e=>e.ItemId).ToHashSet();
        if(needed.Count==0)return new([],0);
        var holders=stock.Where(s=>s.Quantity>0&&needed.Contains(s.Item)&&RetainerRetrieval.AllowsQuality(s.Hq,quality)).Select(s=>s.Retainer).ToHashSet();
        var known=owned.Distinct().Where(holders.Contains).ToArray();
        // A recorded absence may be stale. These are a fallback, never a preflight
        // scan: RetrievalRun stops immediately once actual bag counts are satisfied.
        return new(known.Concat(owned.Distinct().Where(id=>!holders.Contains(id))).ToArray(),known.Length);
    }
}
