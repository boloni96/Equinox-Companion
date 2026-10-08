using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper104Tests
{
 [ModuleInitializer] public static void Run(){
 void Check(bool ok,string label){if(!ok)throw new Exception("Helper104: "+label);}
 var p=new HelperPurchase("ShopExchangeItem",25083,"A Quick Pit Stop Orchestrion Roll",1,[new(25007,"Unidentified Magitek",1)]);
 Check(!HelperPurchasePolicy.Confirmed(p,0,0,[5],[5]),"selection or cancellation is not a completed purchase");
 Check(HelperPurchasePolicy.Confirmed(p,0,1,[5],[4]),"exact token purchase confirmed");
 Check(!HelperPurchasePolicy.Confirmed(p,0,1,[5],[5]),"received item without paying not mirrored");
 Check(!HelperPurchasePolicy.Confirmed(p,0,1,[5],[3]),"unexpected cost rejected");
 var npc=new HelperNpc("a",1026851,"Ironworks Hand",130,1,1,new(1,2,3),new(0,0,0),0);
 Check(HelperPurchasePolicy.SameVendor(npc,npc with {Conversation="b"}),"successive selections same vendor queue");
 Check(!HelperPurchasePolicy.SameVendor(npc,npc with {BaseId=9}),"other vendor rejected");
 Check(!HelperPurchasePolicy.SameVendor(npc,npc with {World=2}),"other world rejected");
 Check(!HelperPurchasePolicy.SameVendor(npc,npc with {Position=new(4,5,6)}),"other location rejected");
 }
}
