using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper103Tests
{
 [ModuleInitializer] public static void Run(){
 void Check(bool ok,string label){if(!ok)throw new Exception("Helper103: "+label);}
 var p=new HelperPurchase("ShopExchangeItem",100,"Roll",1,[new(200,"Token",1)]);
 Check(HelperPurchasePolicy.RequestMatches(p,[(200u,1)]),"exact authorized token");
 Check(!HelperPurchasePolicy.RequestMatches(p,[(201u,1)]),"wrong token rejected");
 Check(!HelperPurchasePolicy.RequestMatches(p,[(200u,2)]),"extra quantity rejected");
 Check(!HelperPurchasePolicy.RequestMatches(p,[]),"empty rejected");
 Check(!HelperPurchasePolicy.RequestMatches(p,[(200u,1),(200u,1)]),"duplicate rejected");
 Check(!HelperPurchasePolicy.RequestMatches(p with {Shop="Shop"},[(200u,1)]),"non-item shop rejected");
 var two=p with {Costs=[new(200,"Token",1),new(201,"Other token",2)]};
 Check(HelperPurchasePolicy.RequestMatches(two,[(201u,2),(200u,1)]),"multi-cost order independent");
 Check(!HelperPurchasePolicy.RequestMatches(two,[(200u,1)]),"missing cost rejected");
 }
}
