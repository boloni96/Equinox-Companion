using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class ItemDelivery312Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(bool ok,string why){if(!ok)throw new Exception("Delivery: "+why);}
  var now=DateTimeOffset.Parse("2026-10-30T00:00:00Z");
  var context=new GilTradeFrame(11,true,true,22,100,false,0,false,0,0,1,1,false,false,false);
  var bags=new TradeBagSlot[]{new(0,0,100,false,20,99,true)};
  var initial=new ItemTradeFrame(context,bags,[],[],true);
  var offer=initial with{Context=context with{TradeOpen=true,TradePartner=22,LocalState=3,RemoteState=4},Remote=[new(100,false,5)]};
  foreach(var bad in new[]{new TradeItemAmount(101,false,1),new(100,true,1),new(100,false,11)}){
   var run=new ItemTradeRun();Check(run.Start(false,[new(100,false,10)],initial,now,exactReceive:true),"receiver starts");
   run.Step(offer with{Remote=[bad]},now);Check(!run.Running&&run.Done==0,"unexpected item, quality or excess quantity stops before acceptance");
  }
  var r=new ItemTradeRun();r.Start(false,[new(100,false,10)],initial,now,exactReceive:true);
  for(var n=0;n<2;n++){
   var t=now.AddSeconds(n*5);var frame=offer with{Bags=[bags[0] with{Quantity=20+n*5}]};
   r.Step(frame,t);Check(r.Step(frame,t.AddSeconds(1))==ItemTradeAction.LockOffer,"stable partial manifest locks");
   Check(r.Step(frame with{Context=frame.Context with{LocalState=4,Confirmation=true}},t.AddSeconds(1.2))==ItemTradeAction.Confirm,"confirm restricted receive");
   r.Step(initial with{Bags=[bags[0] with{Quantity=25+n*5}],Context=context with{CompletionObserved=true}},t.AddSeconds(2));
   Check(r.Done==(n+1)*5,"receipt reduces only confirmed quantity");
   Check(n==0?r.Running&&r.Remaining.Single().Quantity==5:r.Stage==ItemTradeStage.Complete,"receiver waits for full manifest then completes");
  }
  var space=Enumerable.Range(0,7).Select(i=>new TradeBagSlot(0,i,0,false,0,99,false)).ToArray();
  var many=Enumerable.Range(100,6).Select(i=>new TradeItemAmount((uint)i,false,1)).Append(new(200,false,99)).ToArray();
  Check(ItemTradePlan.CanReceiveManifest(many,space,_=>99),"whole delivery may exceed five trade slots");
  Check(!ItemTradePlan.CanReceive(many,space,_=>99),"single trade remains limited to five slots");
  Check(ItemTradePlan.CanReceiveManifest([new(100,false,198)],space.Take(2).ToArray(),_=>99),"whole manifest spans stacks");
  Check(!ItemTradePlan.CanReceiveManifest([new(100,false,199)],space.Take(2).ToArray(),_=>99),"whole manifest cannot exceed available slots");
  Console.WriteLine("PASS delivery312 restricted receipts and whole-manifest capacity");
 }
}
