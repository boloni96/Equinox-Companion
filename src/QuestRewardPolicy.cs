namespace EquinoxCompanion;
internal sealed record QuestRewardCandidate(int Index,uint ItemId,uint Amount,bool Gil,bool UniqueOwned,int Upgrade,ulong VendorValue,string Name);
internal static class QuestRewardPolicy
{
    public static QuestRewardCandidate? Choose(IEnumerable<QuestRewardCandidate> offers)=>offers
        .Where(x=>x.ItemId!=0&&x.Amount>0&&!x.UniqueOwned)
        .OrderByDescending(x=>x.Gil).ThenByDescending(x=>x.Gil?x.VendorValue:0)
        .ThenByDescending(x=>x.Upgrade>0).ThenByDescending(x=>x.Upgrade)
        .ThenByDescending(x=>x.VendorValue).ThenBy(x=>x.Index).FirstOrDefault();
}
