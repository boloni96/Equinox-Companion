using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class GardenPerformance153Tests
{
 [ModuleInitializer] public static void Run()
 {
  void Check(bool value,string label){if(!value)throw new Exception("Garden display 153: "+label);}
  var now=DateTimeOffset.UtcNow;var actor=new Actor("1","Gardener",57,57);
  var address=new Address("game-house",57,341,1,1,0,false,false,"Siren","The Goblet");
  var bed=new SharedGardenBed(1,"","","actual","Krakka Root","",now,now,3,false);
  var plan=new SharedGardenPlan("house","House","Siren","The Goblet",1,1,1,"",now,[bed],address.HouseId);
  var actions=new List<SyncEvent>{new("tend","garden.tended",now,actor,address,1,1)};var sent=new List<string>();var reads=0;
  IEnumerable<SyncEvent> Read(){reads++;return actions;}
  var cache=new GardenPendingDisplay();var roster=new object();
  var pending=cache.Get(plan,roster,"first",0,Read,()=>sent);Check(pending.Beds[0].Queued,"pending care visible");
  for(var frame=1;frame<240;frame++)Check(ReferenceEquals(pending,cache.Get(plan,roster,"first",frame,Read,()=>sent)),"stable frames reuse display");
  Check(reads==1,"event history scanned once across 240 frames");Check(!bed.Queued&&pending.Beds[0].Watered==bed.Watered,"no observation or source mutation");
  sent.Add("tend");Check(!cache.Get(plan,roster,"ack",240,Read,()=>sent).Beds[0].Queued,"ack invalidates immediately");
  actions.Add(new("second","garden.tended",now,actor,address,1,1));Check(cache.Get(plan,roster,"action",241,Read,()=>sent).Beds[0].Queued,"new action invalidates immediately");
  var other=plan with {HouseId="other",GameHouseId="other-game",Plot=2};Check(!cache.Get(other,roster,"action",242,Read,()=>sent).Beds[0].Queued,"separate houses stay independent");
  var swapped=plan with {PhysicalPatch=2};Check(!cache.Get(swapped,roster,"action",243,Read,()=>sent).Beds[0].Queued,"physical patch mapping retained");
  var fresh=plan with {Beds=[bed with {Ready=true}]};Check(cache.Get(fresh,new object(),"action",244,Read,()=>sent).Beds[0].Ready,"roster updates preserved");
  var sources=new UiProjectionCache<int>(maxAgeMs:0);var builds=0;
  Check(sources.Get(roster,"a",0,()=>++builds)==1&&sources.Get(roster,"a",100000,()=>++builds)==1,"unchanged sources do not rebuild on timer");
  Check(sources.Get(roster,"b",100001,()=>++builds)==2&&sources.Get(new object(),"b",100002,()=>++builds)==3,"local edits and shared roster invalidate sources");
 }
}
