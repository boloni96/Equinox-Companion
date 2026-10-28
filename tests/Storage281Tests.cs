using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Storage281Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS storage protection "+label);}
  ChestStack I(uint c,int slot,uint id,int q)=>new(c,slot,id,q,false,99);
  ChestStack E(uint c,int slot)=>new(c,slot,0,0,false,0);
  var original=I(0,0,100,25);
  var inventory=new Dictionary<ulong,ChestStack[]>{{0,[original,E(0,1),E(0,2)]},{1,[I(10000,0,100,10)]},{2,[I(10000,0,100,20)]}};
  var plan=new StorageRoutingPlan("Retainers only",1,[new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true)],0);
  var flow=StorageRouting.Build(plan,inventory,_=>0,includeExistingBags:false);
  Check("never deposits from an existing player stack",flow.Moves.Where(m=>!m.Withdraw&&!m.Internal).All(m=>m.Move.Source.Container!=0||m.Move.Source.Slot!=0));
  Check("never stages into an existing player stack",flow.Moves.Where(m=>m.Withdraw).All(m=>m.Move.Destination.Container!=0||m.Move.Destination.Slot!=0));
  Check("retainer quantities still consolidate",flow.Remaining==0&&flow.Moves.Any(m=>m.Withdraw));
  inventory[0]=[original];flow=StorageRouting.Build(plan,inventory,_=>0,includeExistingBags:false);
  Check("partial player stack is not treated as free staging space",flow.Moves.Length==0&&flow.Remaining>0);
  var protectedSlots=new HashSet<(uint,int)>{(0,0)};
  var pickup=new RoutedMove(2,new(I(10000,0,100,20),E(0,1),20),true);
  var rebound=StorageVisit.Bind([pickup],[original,E(0,2)],[I(10000,0,100,20)],true,protectedSlots);
  Check("live binding does not merge cargo into original bags",rebound.Single().Move.Destination.Slot==2);
  var drop=new RoutedMove(1,new(I(0,1,100,20),E(10000,0),20),false);
  rebound=StorageVisit.Bind([drop],[original,I(0,2,100,20)],[E(10000,0)],true,protectedSlots);
  Check("live binding withdraws only staged cargo",rebound.Single().Move.Source.Slot==2);
  var saved=new RetrievalPreset("Materials",[new(100,"Item",1)],PlanId:"plan");
  Check("plan-only assignment passes start guard",StorageAssignments.HasSelections([new(1,[],[],["plan"])],[saved]));
  Check("deleted and empty plans do not unlock an empty assignment",!StorageAssignments.HasSelections([new(1,[],[],["missing"])],[saved])&&!StorageAssignments.HasSelections([new(1,[],[],["plan"])],[saved with{Items=[]}]));
  var now=DateTimeOffset.Parse("2026-10-28T18:19:12Z");
  var bid=new LotteryBid("Lavender Beds",16,59,11,"fc","","lost","2026-10-23T15:00:00Z","result-dialog");
  Check("known losing ticket accepts separate refund offer",HousingLottery.RefundEligible(bid,null));
  Check("unresolved ticket requires winning number",!HousingLottery.RefundEligible(bid with{Status="entered"},null)&&HousingLottery.RefundEligible(bid with{Status="entered"},6));
  Check("winner and collected tickets cannot be refunded again",!HousingLottery.RefundEligible(bid,11)&&!HousingLottery.RefundEligible(bid with{Evidence="refund-receipt"},6)&&!HousingLottery.RefundEligible(bid with{Status="won"},6));
  Check("Vyrion exact refund chat receipt",HousingLottery.ReceivedGil("You obtain 3,750,000 gil.")==3750000);
  Check("Vyrion refund offer and receipt match",HousingLottery.RefundAmount("Accept a full refund of your deposit of 3,750,000 gil?")==3750000&&HousingLottery.RefundReceiptMatches(1,403,42,3750000,now,1,403,null,3750000,now.AddSeconds(2)));
  Check("dialog wrapping is harmless",HousingLottery.RefundAmount("Accept a full refund of your\ndeposit of 20,000,000 gil?")==20000000);
  Check("closing placard target does not lose receipt",HousingLottery.RefundReceiptMatches(1,403,42,20000000,now,1,403,null,20000000,now.AddSeconds(2)));
  Check("different selected placard still rejected",!HousingLottery.RefundReceiptMatches(1,403,42,20000000,now,1,403,99,20000000,now.AddSeconds(2)));
 }
}
