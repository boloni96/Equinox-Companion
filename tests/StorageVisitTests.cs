using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageVisitTests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS cached organizer "+label);}
  ChestStack E(uint c,int i)=>new(c,i,0,0,false,0);
  ChestStack I(uint c,int i,uint id,int q,bool hq=false)=>new(c,i,id,q,hq,99);
  var recorded=Enumerable.Range(0,175).Select(i=>E((uint)(10000+i/25),i%25)).Concat(Enumerable.Range(0,18).Select(i=>new ChestStack(12001,i,(uint)(i+2),0,false,9999))).ToArray();
  Check("complete empty cache avoids inspection",StorageVisit.RecordedMatches(recorded,0));
  Check("game slot count change requires targeted inspection",!StorageVisit.RecordedMatches(recorded,1));
  Check("missing container cannot be treated as empty",!StorageVisit.RecordedMatches(recorded.Take(170).ToArray(),0));
  var duplicateSlots=recorded.ToArray();duplicateSlots[1]=duplicateSlots[0];Check("duplicate recorded slots rejected",!StorageVisit.RecordedMatches(duplicateSlots,0));
  var staleQty=recorded.ToArray();staleQty[0]=I(10000,0,100,100);Check("invalid stack quantity rejected",!StorageVisit.RecordedMatches(staleQty,1));
  var cached=new RoutedMove(2,new(I(10000,0,100,40),E(0,0),40),true);
  var bags=new[]{E(0,0),E(0,1),E(0,2)};
  var live=new[]{I(10002,5,100,15),I(10003,3,100,25)};
  var rebound=StorageVisit.Bind([cached],bags,live);
  Check("remaps changed source slots and fragmented stacks",rebound.Length==2&&rebound.Sum(x=>x.Move.Quantity)==40&&rebound[1].Move.Source.Container==10003);
  Check("does not mutate native snapshot",live[0].Quantity==15&&bags[0].Item==0);
  var deposit=new RoutedMove(1,new(I(0,0,100,40),E(10000,0),40),false);
  rebound=StorageVisit.Bind([deposit],[I(0,2,100,40)],[I(10000,4,100,90),E(10002,0)]);
  Check("fills actual partial stack before actual empty slot",rebound.Length==2&&rebound[0].Move.Quantity==9&&rebound[1].Move.Quantity==31);
  bool Reject(RoutedMove[] v,ChestStack[] b,ChestStack[] r){try{StorageVisit.Bind(v,b,r);return false;}catch(InvalidOperationException){return true;}}
  Check("never substitutes NQ for HQ",Reject([cached with{Move=cached.Move with{Source=cached.Move.Source with{Hq=true}}}],bags,live));
  Check("shortage rejected before submitting a visit",Reject([cached],bags,[I(10000,0,100,2)]));
  Check("full live destination rejected",Reject([deposit],[I(0,0,100,40)],[I(10000,0,200,99)]));
  var gear=new RoutedMove(1,new(new(0,1,400,1,false,1),E(10000,0),1),false);
  var kept=new HashSet<(uint,int)>{(0,0)};
  rebound=StorageVisit.Bind([gear],[new(0,0,400,1,false,1),new(0,2,400,1,false,1)],[E(10000,0)],true,kept);
  Check("does not entrust protected original bag gear",rebound.Single().Move.Source.Slot==2);
  var all=StorageAutoRouting.AllRetainers;
  StorageRoute rule=new(StorageRouting.AllSources,all,[],[],AllItems:true);
  var inv=new Dictionary<ulong,ChestStack[]>{{90,[I(10000,0,100,5),E(10000,1)]},{20,[I(10000,0,100,10),E(10000,1)]},{5,[I(10000,0,100,20),I(10000,1,200,3)]},{0,bags}};
  var plan=new StorageRoutingPlan("Default",1,[rule],0);
  var moves=StorageRouting.Build(plan,inv,_=>58,includeExistingBags:false);
  Check("last retainer is first duplicate source",moves.Moves.First(x=>x.Withdraw).Retainer==5);
  Check("game order rather than numeric ID defines first retainer",moves.Moves.Where(x=>!x.Withdraw).All(x=>x.Retainer==90));
  Check("unique items left alone",moves.Moves.All(x=>x.Move.Source.Item!=200));
  var fullFirst=new Dictionary<ulong,ChestStack[]>{{90,[I(10000,0,100,99)]},{20,[I(10000,0,100,80)]},{5,[I(10000,0,100,10)]},{0,bags}};
  var fullFlow=StorageRouting.Build(plan,fullFirst,_=>58,includeExistingBags:false);
  Check("full first retainer falls back to next existing holder",fullFlow.Remaining==0&&fullFlow.Moves.Any(x=>!x.Withdraw&&x.Retainer==20));
  var assignments=new[]{new RetainerAssignment(20,[58],[]),new RetainerAssignment(90,[58],[]),new RetainerAssignment(5,[],[200])};
  var rules=StorageAssignments.Apply([rule],assignments,inv,id=>id==100?58u:80u);
  Check("overlapping categories preserve chosen priority",rules.Single(x=>x.Items.Contains(100u)).Destinations!.SequenceEqual(new ulong[]{20,90}));
  Check("specific item destination",rules.Single(x=>x.Items.Contains(200u)).Destinations!.SequenceEqual(new ulong[]{5}));
  Check("unassigned category omitted",StorageAssignments.Apply([rule],assignments,inv,_=>42).All(x=>!x.Items.Contains(100u)));
  var explicitRule=rule with{To=90};Check("explicit route overrides assignments",StorageAssignments.Apply([explicitRule],assignments,inv,_=>42).Single()==explicitRule);
  var emptyTargets=new Dictionary<ulong,ChestStack[]>{{90,[E(10000,0)]},{20,[E(10000,0)]},{5,[I(10000,0,100,20)]},{0,bags}};
  var applied=StorageAssignments.Apply([rule],assignments,emptyTargets,_=>58);
  Check("assignment primary receives new category",StorageRouting.Build(plan with{Rules=applied},emptyTargets,_=>58,includeExistingBags:false).Moves.Any(x=>!x.Withdraw&&x.Retainer==20));
  var overflow=new Dictionary<ulong,ChestStack[]>{{90,[E(10000,0)]},{20,[I(10000,0,100,95)]},{5,[I(10000,0,100,20)]},{0,bags}};
  var flow=StorageRouting.Build(plan with{Rules=StorageAssignments.Apply([rule],assignments,overflow,id=>id==100?58u:0)},overflow,_=>58,includeExistingBags:false);
  Check("assignment overflow uses next eligible retainer",flow.Remaining==0&&flow.Moves.Any(x=>!x.Withdraw&&x.Retainer==90));
  var send=rule with{From=0,Amounts=new(){[100]=7}};overflow[0]=[I(0,0,100,30)];
  var sent=StorageRouting.Build(plan with{Rules=StorageAssignments.Apply([send],assignments,overflow,_=>58)},overflow,_=>58,true);
  Check("assigned entrust retains amount across overflow",sent.Moves.Sum(x=>x.Move.Quantity)==7&&sent.Moves.All(x=>x.Retainer is 20 or 90));
  var dupInv=new Dictionary<ulong,ChestStack[]>{{0,[I(0,0,100,10),I(0,1,200,10)]},{20,[I(10000,0,100,1),E(10000,1)]},{90,[I(10000,0,200,1),E(10000,1)]}};
  var dupAssign=new[]{new RetainerAssignment(20,[],[100]),new RetainerAssignment(90,[],[200])};
  var dupRules=StorageAssignments.Apply([new(0,all,[],[],Duplicates:true)],dupAssign,dupInv,_=>0);
  var dupMoves=StorageRouting.Build(plan with{Rules=dupRules},dupInv,_=>0,true);
  Check("duplicate entrust keeps each item's assigned destination",dupMoves.Moves.All(x=>x.Move.Source.Item==100?x.Retainer==20:x.Retainer==90));
  var json=System.Text.Json.JsonSerializer.Serialize(assignments);Check("assignments survive configuration roundtrip",System.Text.Json.JsonSerializer.Deserialize<RetainerAssignment[]>(json)![0].Categories.SequenceEqual(new uint[]{58}));
 }
}
