namespace EquinoxCompanion;
public sealed record HelperPurchaseCost(uint ItemId,string Name,int Amount);
public sealed record HelperPurchase(string Shop,uint ItemId,string ItemName,int Quantity,HelperPurchaseCost[] Costs,string Prompt="");
public static class HelperPurchasePolicy
{
    public static bool RequestMatches(HelperPurchase p,IReadOnlyList<(uint ItemId,int Amount)> requested)=>
        Valid(p)&&p.Shop=="ShopExchangeItem"&&requested.Count==p.Costs.Length&&
        requested.Select(x=>x.ItemId).Distinct().Count()==requested.Count&&
        requested.All(x=>p.Costs.Any(c=>c.ItemId==x.ItemId&&c.Amount==x.Amount));
    public static bool Greeting(bool opened,int clicks,string speaker,string vendor,string text)=>opened&&clicks<20&&speaker==vendor&&vendor.Length>0&&text.Length>0;
    public static bool Valid(HelperPurchase? p)=>p!=null&&p.Shop is "Shop" or "ShopExchangeCurrency" or "ShopExchangeItem"&&p.ItemId is >0 and <1000000&&p.ItemName.Length is >0 and <=100&&p.Quantity is >=1 and <=99&&p.Prompt.Length<=4000&&p.Costs is {Length:>=1 and <=3}&&p.Costs.All(c=>c.ItemId is >0 and <1000000&&c.ItemId!=p.ItemId&&c.Name.Length is >0 and <=100&&c.Amount>0)&&p.Costs.Select(c=>c.ItemId).Distinct().Count()==p.Costs.Length;
    public static bool Same(HelperPurchase? a,HelperPurchase? b)=>Valid(a)&&Valid(b)&&a!.Shop==b!.Shop&&a.ItemId==b.ItemId&&a.ItemName==b.ItemName&&a.Quantity==b.Quantity&&a.Costs.OrderBy(c=>c.ItemId).SequenceEqual(b.Costs.OrderBy(c=>c.ItemId));
    public static bool Confirmed(HelperPurchase p,int before,int after,int[] costsBefore,int[] costsAfter)=>Valid(p)&&after-before==p.Quantity&&costsBefore.Length==p.Costs.Length&&costsAfter.Length==p.Costs.Length&&p.Costs.Select((c,i)=>costsBefore[i]-costsAfter[i]==c.Amount).All(x=>x);
}
