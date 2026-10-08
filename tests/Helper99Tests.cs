using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper99Tests
{
 [ModuleInitializer] public static void Run(){
  void Check(bool result,string name){if(!result)throw new Exception("Helper99: "+name);}
  var mgp=new HelperPurchase("ShopExchangeCurrency",100,"Reward",1,[new(29,"MGP",200000)]);
  Check(HelperPurchasePolicy.Valid(mgp),"MGP valid");
  Check(HelperPurchasePolicy.Valid(mgp with {Shop="Shop",Costs=[new(1,"Gil",100)]}),"gil valid");
  Check(!HelperPurchasePolicy.Valid(mgp with {Quantity=0}),"zero rejected");
  Check(!HelperPurchasePolicy.Valid(mgp with {Costs=[new(29,"MGP",0)]}),"zero price rejected");
  Check(!HelperPurchasePolicy.Valid(mgp with {Costs=[new(29,"MGP",1),new(29,"MGP",2)]}),"duplicate currency rejected");
  Check(!HelperPurchasePolicy.Same(mgp,mgp with {Costs=[new(29,"MGP",199999)]}),"price differs");
  Check(!HelperPurchasePolicy.Same(mgp,mgp with {ItemId=101}),"item differs");
  Check(HelperPurchasePolicy.Confirmed(mgp,0,1,[200000],[0]),"item and cost proof");
  Check(!HelperPurchasePolicy.Confirmed(mgp,0,1,[200000],[200000]),"item only insufficient");
  Check(!HelperPurchasePolicy.Confirmed(mgp,0,0,[200000],[0]),"cost only insufficient");
  var mixed=mgp with {Shop="ShopExchangeItem",Costs=[new(200,"Token",2),new(201,"Other token",3)]};
  Check(HelperPurchasePolicy.Valid(mixed),"mixed costs valid");
  Check(HelperPurchasePolicy.Same(mixed,mixed with {Costs=[mixed.Costs[1],mixed.Costs[0]]}),"cost order independent");
 }
}
