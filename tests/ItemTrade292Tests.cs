using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class ItemTrade292Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception("Items: "+label);Console.WriteLine("PASS items292 "+label);}
  var t=DateTimeOffset.Parse("2026-10-29T03:00:00Z");
  var context=new GilTradeFrame(11,true,true,22,100,false,0,false,0,0,1,1,false,false,false);
  var bags=Enumerable.Range(0,6).Select(i=>new TradeBagSlot(0,i,(uint)(100+i),false,20,99,true)).Append(new(0,6,100,true,15,99,true)).Append(new(0,7,200,false,1,1,false)).ToArray();
  var initial=new ItemTradeFrame(context,bags,[],[],true);
  var wants=Enumerable.Range(100,6).Select(i=>new TradeItemAmount((uint)i,false,5)).ToArray();
  var batch=ItemTradePlan.Next(wants,bags);Check("batch max five source stacks",batch.Length==5&&batch.All(x=>x.Quantity==5));
  Check("HQ is separate",ItemTradePlan.Next([new(100,true,5)],bags).Single().Source.Hq);
  var rejected=false;try{ItemTradePlan.Next([new(200,false,1)],bags);}catch(InvalidOperationException){rejected=true;}Check("untradable copies excluded",rejected);
  rejected=false;try{ItemTradePlan.Next([new(100,true,16)],bags);}catch(InvalidOperationException){rejected=true;}Check("insufficient quality never substitutes",rejected);
  Check("receiver can fill partial stack",ItemTradePlan.CanReceive([new(100,false,79)],bags,_=>99));
  Check("receiver cannot overflow with full bags",!ItemTradePlan.CanReceive([new(100,false,80)],bags,_=>99));
  Check("HQ needs its own space",!ItemTradePlan.CanReceive([new(101,true,1)],bags,_=>99));
  Check("crystals use dedicated capacity",ItemTradePlan.CanReceive([new(2,false,9)],[new(2001,0,2,false,9990,9999,true)],_=>9999));
  Check("crystals cannot overflow into bags",!ItemTradePlan.CanReceive([new(2,false,10)],[new(2001,0,2,false,9990,9999,true),new(0,0,0,false,0,0,false)],_=>9999));
  var r=new ItemTradeRun();Check("empty sender rejected",!r.Start(true,[],initial,t));Check("wrong partner rejected at start",!r.Start(true,wants,initial with{Context=context with{PartnerNearby=false}},t));
  Check("sender starts",r.Start(true,wants,initial,t));
  var current=bags;
  for(var round=0;round<2;round++){
   var idle=initial with{Bags=current};Check("one request",r.Step(idle,t)==ItemTradeAction.Request);Check("no duplicate request",r.Step(idle,t.AddMilliseconds(10))==ItemTradeAction.None);
   var trade=idle with{Context=context with{TradeOpen=true,TradePartner=22,LocalState=3,RemoteState=3}};
   var expected=ItemTradePlan.Next(r.Remaining,current);var offered=new List<TradeItemAmount>();
   for(var i=0;i<expected.Length;i++){
    Check("offer next stack once",r.Step(trade with{Local=offered.ToArray()},t.AddMilliseconds(200+i*300))==ItemTradeAction.AddItem&&r.NextItem==expected[i]);
    Check("await native offer",r.Step(trade with{Local=offered.ToArray()},t.AddMilliseconds(250+i*300))==ItemTradeAction.None);
    offered.Add(new(expected[i].Source.Item,expected[i].Source.Hq,expected[i].Quantity));
   }
   trade=trade with{Local=offered.ToArray()};Check("lock exact batch",r.Step(trade,t.AddSeconds(4))==ItemTradeAction.LockOffer);
   trade=trade with{Context=trade.Context with{LocalState=4,RemoteState=4,Confirmation=true}};
   Check("confirm exact batch",r.Step(trade,t.AddSeconds(4.2))==ItemTradeAction.Confirm);
   current=current.Select(s=>s with{Quantity=s.Quantity-offered.Where(x=>x.Key==s.Key).Sum(x=>x.Quantity)}).ToArray();
   var closed=idle with{Bags=current,Context=context with{CompletionObserved=false}};
   r.Step(closed,t.AddSeconds(4.5));Check("inventory delta alone cannot complete",r.Trades==round);
   r.Step(closed with{Context=context with{CompletionObserved=true}},t.AddSeconds(4.7));Check("receipt and exact inventory delta complete",r.Trades==round+1);
   t=t.AddSeconds(7);
  }
  Check("all quantities sent across batches",r.Stage==ItemTradeStage.Complete&&r.Done==30&&r.Trades==2);
  ItemTradeRun Started(){var run=new ItemTradeRun();run.Start(true,[new(100,false,5)],initial,t);run.Step(initial,t);return run;}
  var open=initial with{Context=context with{TradeOpen=true,TradePartner=22,LocalState=3,RemoteState=3}};
  r=Started();r.Step(open with{Context=open.Context with{TradePartner=23}},t);Check("partner swap stops",!r.Running);
  r=Started();r.Step(open with{Remote=[new(100,false,1)]},t);Check("wrong-side item stops",!r.Running);
  r=Started();r.Step(open with{Context=open.Context with{LocalGil=1}},t);Check("mixed gil stops",!r.Running);
  r=Started();r.Step(open,t);r.Step(open with{Local=[new(100,false,6)]},t.AddSeconds(1));Check("wrong quantity stops",!r.Running);
  r=Started();r.Step(open,t);r.Step(initial,t.AddSeconds(1));Check("cancel does not count",!r.Running&&r.Done==0);
  r=new();Check("receiver starts",r.Start(false,[],initial,t));var receive=open with{Remote=[new(100,false,5)],Context=open.Context with{RemoteState=4}};
  r.Step(receive,t);Check("receiver locks stable offer",r.Step(receive,t.AddSeconds(1))==ItemTradeAction.LockOffer);
  r.Step(receive with{Remote=[new(100,false,6)]},t.AddSeconds(1.1));Check("changed locked receive offer stops",!r.Running);
  r=new();r.Start(false,[],initial,t);r.Step(receive with{CanReceive=false},t);Check("insufficient receiving space stops",!r.Running);
  r=new();r.Start(false,[],initial,t);r.Step(receive,t);r.Step(receive,t.AddSeconds(1));
  Check("receiver confirms",r.Step(receive with{Context=receive.Context with{LocalState=4,Confirmation=true}},t.AddSeconds(1.2))==ItemTradeAction.Confirm);
  r.Step(initial with{Bags=bags.Select(s=>s.Item==100&&!s.Hq?s with{Quantity=25}:s).ToArray(),Context=context with{CompletionObserved=true}},t.AddSeconds(2));
  Check("receiver records exact inventory gain",r.Done==5&&r.Trades==1);
 }
}
