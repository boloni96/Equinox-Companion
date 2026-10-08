namespace EquinoxCompanion;
public sealed record HelperExchange(uint ItemId,string ItemName,int Quantity,uint CostItemId,string CostName,int CostQuantity);
public static class HelperExchangePolicy
{
    public const string Currency="Unidentified Magitek";
    public static readonly string[] EventItems=["Hammerhead Orchestrion Roll","Valse di Fantastica Orchestrion Roll","Relax and Reflect Orchestrion Roll","Veiled in Black Orchestrion Roll","Apocalypsis Noctis Orchestrion Roll","A Quick Pit Stop Orchestrion Roll"];
    public static bool Valid(HelperExchange? x)=>x!=null&&x.ItemId>0&&x.ItemId<1000000&&x.CostItemId>0&&x.CostItemId<1000000&&x.ItemId!=x.CostItemId&&x.Quantity is >=1 and <=99&&x.CostQuantity==x.Quantity&&x.CostName==Currency&&EventItems.Contains(x.ItemName,StringComparer.Ordinal);
    public static bool Confirmed(HelperExchange x,int beforeItem,int afterItem,int beforeCost,int afterCost)=>Valid(x)&&afterItem-beforeItem==x.Quantity&&beforeCost-afterCost==x.CostQuantity;
}
