using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe (uint Page,int Quantity,bool Hq,uint Item)[] WorkshopRecordedChest(uint item)
    {
        var member=InfoProxyFreeCompanyMember.Instance();var fc=member==null?0:member->FreeCompanyId;
        if(fc==0)return [];
        var prefix="fc:"+fc+":";
        var latest=new Dictionary<string,(DateTimeOffset At,StorageStack[]? Stacks)>();
        foreach(var l in config.SharedRoster?.ItemLocations??[])
            if(l.Key.StartsWith(prefix,StringComparison.Ordinal)&&(!latest.TryGetValue(l.Key,out var old)||l.At>old.At))latest[l.Key]=(l.At,l.Stacks);
        foreach(var e in DiscoveryReads().AllStorage()){
            var l=e.Storage!;if(l.Key.StartsWith(prefix,StringComparison.Ordinal)&&(!latest.TryGetValue(l.Key,out var old)||e.At>old.At))latest[l.Key]=(e.At,l.Stacks);
        }
        return latest.Where(l=>uint.TryParse(l.Key[prefix.Length..],out var page)&&page is >=20000 and <=20004 or 22001)
            .SelectMany(l=>(l.Value.Stacks??[]).Where(s=>(item==0||s.ItemId==item)&&s.Quantity>0).Select(s=>(uint.Parse(l.Key[prefix.Length..]),s.Quantity,s.Hq,s.ItemId))).ToArray();
    }
    private string WorkshopMaterialLocations(uint item)=>string.Join("\n",
        retrievalStock.Where(s=>s.Item==item).Select(s=>$"Retainer {s.Holder}: {s.Quantity} {(s.Hq?"HQ":"NQ")}")
        .Concat(WorkshopRecordedChest(item).Select(s=>$"FC chest {(s.Page==22001?"crystals":"tab "+(s.Page-19999))}: {s.Quantity} {(s.Hq?"HQ":"NQ")}")));
    private bool TryWorkshopChestRestock(WorkshopSupply row,int nq,int hq,int rn,int rh,DateTimeOffset now)
    {
        var stock=WorkshopRecordedChest(row.ItemId);if(stock.Length==0)return false;
        var cn=stock.Where(s=>!s.Hq).Sum(s=>s.Quantity);var ch=stock.Where(s=>s.Hq).Sum(s=>s.Quantity);
        var batch=WorkshopPolicy.Restock(row,nq,hq,rn+cn,rh+ch,WorkshopActiveQualityFilter);if(batch==null)return false;
        var high=batch.Value.Hq;var held=high?hq:nq;var ret=high?rh:rn;var chest=high?ch:cn;
        var self=Objects.LocalPlayer;if(self==null)return false;
        float Distance(string name)=>Objects.Where(o=>o.IsTargetable&&string.Equals(o.Name.TextValue,name,StringComparison.OrdinalIgnoreCase)).Select(o=>Vector3.DistanceSquared(self.Position,o.Position)).DefaultIfEmpty(float.PositiveInfinity).Min();
        if(!WorkshopPolicy.PreferRecordedChest(workshopPreparationWithdrawal?workshopPreparationTarget-WorkshopPreparationHeld(row.ItemId):row.SetSize-held,ret,chest,Distance("Company Chest"),Distance("Summoning Bell")))return false;
        BeginWorkshopChest(now);if(workshopCombinedStage!=4)return true;
        workshopChestDeposit=false;workshopChestSupply=row;workshopChestHq=high;
        var take=Math.Min(batch.Value.Quantity,chest);
        if(workshopPreparationWithdrawal)take=Math.Min(take,Math.Max(0,workshopPreparationTarget-WorkshopPreparationHeld(row.ItemId)));
        workshopChestGoal=checked(held+take);
        workshopChestSources=stock.Where(s=>s.Hq==high).Select(s=>s.Page).Distinct().ToArray();
        workshopStatus=$"Withdraw {row.Name} · recorded in FC chest "+string.Join(", ",workshopChestSources.Select(p=>p==22001?"crystals":"tab "+(p-19999)));
        RecordTravelPrompt("Workshop material source",new{row.ItemId,source="FC chest",pages=workshopChestSources,quality=high?"HQ":"NQ",quantity=workshopChestGoal-held});return true;
    }
}
