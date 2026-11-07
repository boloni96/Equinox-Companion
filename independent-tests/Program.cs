using EquinoxCompanion;
using System.Numerics;
GenericRoad480Tests.Run();
void Check(bool condition,string name){if(!condition)throw new Exception(name);}
Check(IndependentStepPolicy.CanWalkWithoutShortcut("CompleteQuest",true,132,132,132),"locked Archers Guild shortcut falls back to walking within New Gridania");
Check(!IndependentStepPolicy.CanWalkWithoutShortcut("CompleteQuest",true,132,133,133),"locked cross-zone shortcut cannot walk coordinates in the wrong zone");
Check(!IndependentStepPolicy.CanWalkWithoutShortcut("None",true,132,132,132),"explicit travel-only step is not silently completed");
Check(!IndependentStepPolicy.CanWalkWithoutShortcut("CompleteQuest",false,132,132,132),"walking fallback needs a known position");
Check(AethernetTravelPolicy.CanUseMenuEntry(false,false,false),"available follower menu destination accepted");
Check(!AethernetTravelPolicy.CanUseMenuEntry(true,false,false),"locked follower destination rejected");
Check(!AethernetTravelPolicy.CanUseMenuEntry(false,true,false),"unusable follower destination rejected");
Check(!AethernetTravelPolicy.CanUseMenuEntry(false,false,true),"current destination does not dispatch travel");
Check(AethernetTravelPolicy.NameMatches("Leatherworkers' Guild & Shaded Bower","Leatherworkers' Guild"),"legacy shopping label resolves exact renamed destination");
Check(!AethernetTravelPolicy.NameMatches("Archers' Guild","Guild"),"ambiguous partial names are rejected");
var manualPause=new QuestMovementPause();var pauseAt=DateTimeOffset.UtcNow;
Check(!manualPause.Observe(pauseAt,false),"idle input does not pause quest");
Check(manualPause.Observe(pauseAt,true),"movement yields immediately");
Check(manualPause.Observe(pauseAt.AddMilliseconds(450),false),"release debounce prevents fighting input");
Check(manualPause.Observe(pauseAt.AddSeconds(60),true),"held input can pause without stopping quest");
Check(!manualPause.Observe(pauseAt.AddSeconds(60.5),false),"quiet input resumes automatically");
using var stream=File.OpenRead(args[0]);var book=IndependentQuestBook.Load(stream);
Check(book.Routes.Count==4388,"complete pinned import");
var evt=book.Find("5466").Single();
Check(evt.Definition.GetProperty("QuestSequence")[0].GetProperty("Steps")[0].GetProperty("AethernetShortcut").GetArrayLength()==2,"aethernet retained");
Check(IndependentQuestBook.MissingHandlers(evt,new HashSet<string>()).Contains("AcceptQuest"),"import does not enable unsupported steps");
var graph=new IndependentNavigationGraph(
    new[]{new IndependentNavigationGraph.Cell(0,new(0,0,0)),new(1,new(0,0,5)),new(2,new(5,0,5)),new(3,new(5,0,0)),new(4,new(0,10,0))},
    new[]{new IndependentNavigationGraph.Portal(0,1,new(0,0,2.5f)),new(1,2,new(2.5f,0,5)),new(2,3,new(5,0,2.5f))});
Check(graph.Find(0,3)!.Any(p=>p.Z==5),"follow connected corridor around blocked direct route");
Check(graph.Find(0,4)==null,"overlapping disconnected floor unreachable");
Check(graph.Find(3,0)==null,"one-way portals respected");
using var cancel=new CancellationTokenSource();cancel.Cancel();
try{graph.Find(0,3,cancel.Token);throw new Exception("cancellation ignored");}catch(OperationCanceledException){}
try{graph.Find(0,3,expansionLimit:1);throw new Exception("budget ignored");}catch(InvalidOperationException){}
Console.WriteLine($"Independent data/navigation core passed: {book.Routes.Count} routes. Game geometry and execution not enabled.");
var collision=new TestCollision();
var search=new CollisionRouteSearch(collision,new(0,0,0),new(5,0,0),.3f);
for(var i=0;i<1000&&!search.Finished;i++)search.Advance(100,1000);
Check(search.Path!=null&&search.Path.Any(p=>p.Z>=3),"collision search takes corridor around wall");
Check(search.Path!.Zip(search.Path.Skip(1)).All(p=>collision.Traverse(p.First,p.Second)),"every returned edge checked");
var impossible=new CollisionRouteSearch(collision,new(0,0,0),new(0,10,0),.3f);
for(var i=0;i<1000&&!impossible.Finished;i++)impossible.Advance(100,1000);
Check(impossible.Path==null&&impossible.Error.Length>0,"no jumping between floors");
using(var definition=System.Text.Json.JsonDocument.Parse("{\"InteractionType\":\"Interact\",\"UnknownGate\":true}"))Check(IndependentStepPolicy.Review(definition.RootElement).Contains("UnknownGate"),"unknown conditions block dispatch");
using(var definition=System.Text.Json.JsonDocument.Parse("{\"InteractionType\":\"SinglePlayerDuty\"}"))Check(IndependentStepPolicy.Review(definition.RootElement).Contains("manual"),"duty remains manual");
using(var flags=System.Text.Json.JsonDocument.Parse("[16,null,null,null,null,null]"))Check(IndependentStepPolicy.CompletedFlags(flags.RootElement,new byte[]{48,0,0,0,0,0}),"quest flag bits");
using(var flags=System.Text.Json.JsonDocument.Parse("[{\"High\":1,\"Mode\":\"Exact\"},null,null,null,null,null]"))Check(!IndependentStepPolicy.CompletedFlags(flags.RootElement,new byte[]{48,0,0,0,0,0}),"quest exact nibble");
Check(book.TravelId("[Gold Saucer] Entrance & Card Squares")==63,"shard mapping");
Check(book.EmoteId("bow")>0,"emote mapping");
Console.WriteLine("Collision planning, quest conditions and action mappings passed.");
var lease=new WalkingInputLease();var frame=DateTimeOffset.UtcNow;
Check(lease.Check(frame)==WalkingInputDecision.Inactive,"no movement before arming");
var afterSlowPlanning=frame.AddMilliseconds(850);lease.Arm(afterSlowPlanning);
Check(lease.Check(afterSlowPlanning.AddMilliseconds(10))==WalkingInputDecision.Allowed,"slow planning does not consume the input lease");
Check(lease.Check(afterSlowPlanning.AddMilliseconds(180))==WalkingInputDecision.Expired,"stale input expires at boundary");
lease.Arm(afterSlowPlanning.AddSeconds(1));lease.Revoke();
Check(lease.Check(afterSlowPlanning.AddSeconds(1))==WalkingInputDecision.Inactive,"pause/stop revokes input immediately");
Check(WalkingInputLease.CanSupplyGround(0,0,0),"supply idle ground movement");
Check(!WalkingInputLease.CanSupplyGround(1,0,0),"never replace manual strafe");
Check(!WalkingInputLease.CanSupplyGround(0,-1,0),"never replace manual backward input");
Check(!WalkingInputLease.CanSupplyGround(0,0,.1f),"never replace manual turn input");
Check(!WalkingInputLease.CanSupplyGround(float.NaN,0,0),"invalid movement output fails closed");
Console.WriteLine("Movement lease timing, cancellation and manual priority passed.");
var forwardAxes=WalkingSteering.Axes(Vector3.Zero,new(0,0,5),0);
Check(Vector2.Distance(forwardAxes,new(0,1))<.001f,"forward steering");
Check(Vector2.Distance(WalkingSteering.Axes(Vector3.Zero,new(5,0,0),MathF.PI/2),new(0,1))<.001f,"relative heading steering");
var corridor=new[]{Vector3.Zero,new Vector3(0,0,1),new Vector3(.3f,0,2),new Vector3(0,0,3)};
Check(WalkingSteering.LookAhead(Vector3.Zero,corridor,1,(_,_)=>true)==2,"bounded lookahead removes zigzag");
Check(WalkingSteering.LookAhead(Vector3.Zero,corridor,1,(_,_)=>false)==1,"lookahead cannot cut blocked corner");
Console.WriteLine("Relative movement and collision-approved lookahead passed.");
var phases=new NavigationPhaseTimer();phases.Begin(frame);
Check(!phases.Expired(frame.AddSeconds(89.6)),"planning has own time budget");
phases.SetPlanning(false,frame.AddSeconds(89.6));
Check(!phases.Expired(frame.AddSeconds(94)),"long planning does not consume walking budget");
Check(phases.Expired(frame.AddSeconds(180)),"walking is still bounded");
var avoided=new AvoidStalledTargets(collision,new[]{new Vector3(0,0,1)});
Check(!avoided.Traverse(Vector3.Zero,new(0,0,1)),"retry rejects stalled target");
Check(avoided.Traverse(Vector3.Zero,new(-1,0,0)),"retry may leave along another clear edge");
Check(!avoided.Traverse(Vector3.Zero,new(0,0,2)),"retry rejects an edge crossing a blocked target even when its endpoint is clear");
Check(AvoidStalledTargets.ClearOfBlocked(new(0,0,.5f),new(0,0,-.5f),[new(0,0,1)]),"can escape a nearby blocked footprint by moving away");
Check(!AvoidStalledTargets.ClearOfBlocked(new(0,0,.5f),new(0,0,2),[new(0,0,1)]),"cannot escape by crossing through the obstruction");
Check(AvoidStalledTargets.ClearOfBlocked(new(0,4,0),new(0,4,2),[new(0,0,1)]),"blocked footprint does not obstruct another floor");
var detour=new[]{Vector3.Zero,new Vector3(1,0,0),new Vector3(1,0,2),new Vector3(0,0,3)};
var around=WalkingSteering.PursuitTarget(Vector3.Zero,detour,1,(a,b)=>AvoidStalledTargets.ClearOfBlocked(a,b,[new(.4f,0,.8f)]));
Check(around==new Vector3(1,0,0),"pursuit follows the corner rather than cutting across a remembered obstacle");
var rememberedObstacle=new AvoidStalledTargets(new OpenCollision(),[new(0,0,1)]);
var rememberedRoute=new CollisionRouteSearch(rememberedObstacle,Vector3.Zero,new(0,0,3),.35f,.5f);
for(var i=0;i<100&&!rememberedRoute.Finished;i++)rememberedRoute.Advance(100,10000);
Check(rememberedRoute.Path!=null&&rememberedRoute.Path.Zip(rememberedRoute.Path.Skip(1)).All(e=>rememberedObstacle.Traverse(e.First,e.Second)),"fine recovery grid finds a connected detour around a remembered furniture footprint");
Console.WriteLine("Separate phase deadlines and blocked-target retry passed.");
// Thin post offset between the old center and side tracks.
Check(!WalkingClearance.BodyRoom(Vector3.Zero,.45f,(a,b)=>{
    var d=b-a;var post=new Vector3(.22f,a.Y,0);
    var t=Math.Clamp(Vector3.Dot(post-a,d)/d.LengthSquared(),0,1);
    return Vector3.Distance(post,a+t*d)<.04f;
}),"full footprint catches a narrow off-center post");
Check(WalkingClearance.BodyRoom(Vector3.Zero,.45f,(_,_)=>false),"open footprint remains walkable");
Console.WriteLine("Thin obstacle footprint checks passed.");
Check(MathF.Abs(WalkingSteering.SmoothHeading(0,1,.02f)-.048f)<.001f,"turn rate is bounded");
Check(WalkingSteering.SmoothHeading(3.13f,-3.13f,.02f)>3.13f,"heading wraps along shortest turn");
Check(WalkingSteering.AdvanceWaypoint(new(1.1f,0,0),[Vector3.Zero,new(1,0,0),new(2,0,0)],1,(_,_)=>true)==2,"passed waypoint never turns back");
Check(WalkingSteering.AdvanceWaypoint(new(1.8f,0,0),[Vector3.Zero,new(2,0,0)],1,(_,_)=>true)==1,"final endpoint retained outside arrival range");
var blockedPosition=new Vector3(1.1f,0,0);
Vector3[] blockedRoute=[Vector3.Zero,new(1,0,0),new(2,0,0)];
var consumed=WalkingSteering.AdvanceWaypoint(blockedPosition,blockedRoute,1,(_,_)=>false);
Check(WalkingSteering.PursuitTarget(blockedPosition,blockedRoute,consumed,(_,_)=>false,(_,_)=>false)==blockedPosition,"consumed waypoint does not permit movement through an obstacle");
Check(WalkingSteering.PursuitTarget(new(.5f,0,0),[Vector3.Zero,new(1,0,0),new(2,0,0),new(3,0,0)],1,(_,_)=>true).X>2.6f,"pursuit advances continuously along route");
var exact=new CollisionRouteSearch(collision,Vector3.Zero,new(-.5f,0,-.5f),.4f);
for(var i=0;i<100&&!exact.Finished;i++)exact.Advance(100,1000);
Check(exact.Path!=null&&CollisionRouteSearch.Horizontal(exact.Path[^1],new(-.5f,0,-.5f))<=.4f,"sub-grid destination gets verified connector");
var longLine=Enumerable.Range(0,10).Select(i=>new Vector3(i,0,0)).ToArray();
Check(WalkingSteering.AdvanceWaypoint(new(3.2f,0,.2f),longLine,1,(_,_)=>true)>=4,"slow frame recovers onward waypoint instead of turning back");
Check(Vector3.Distance(Vector3.Zero,WalkingSteering.PursuitTarget(Vector3.Zero,[new(4,0,0),new(5,0,0)],1,(_,_)=>false))<=2.21f,"fallback target stays inside collision query range");
var opening=VerifiedWalkingOpening.Build(collision,Vector3.Zero,new(-12,0,0),.4f);
Check(opening.Length==7&&opening.Zip(opening.Skip(1)).All(p=>collision.Traverse(p.First,p.Second)),"opening is checked before streaming movement");
Check(VerifiedWalkingOpening.Build(collision,Vector3.Zero,new(12,0,0),.4f).Length==0,"blocked opening waits for full planning");
Check(VerifiedWalkingOpening.Build(collision,Vector3.Zero,new(-12,4,0),.4f).Length==0,"different floor waits for stair planning");
RememberedWalkingRoute[] remembered=[new(144,new(8,0,0),1,longLine)];
Check(WalkingRouteMemory.Join(remembered,144,new(3.1f,0,0),new(8,0,0),1,(_,_)=>true)!=null,"saved route can join near current position");
Check(WalkingRouteMemory.Join(remembered,1360,new(3.1f,0,0),new(8,0,0),1,(_,_)=>true)==null,"route memory isolates territories");
Check(WalkingRouteMemory.Join(remembered,144,new(3.1f,0,0),new(8,0,0),1,(_,_)=>false)==null,"blocked saved-route connector cannot start walking");
var json=System.Text.Json.JsonSerializer.Serialize(remembered,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
var restoredRoutes=System.Text.Json.JsonSerializer.Deserialize<RememberedWalkingRoute[]>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true})!;
Check(restoredRoutes[0].Points[^1]==longLine[^1],"persisted route coordinates survive reload");
var wallRoute=new CollisionRouteSearch(new WallPreferenceCollision(false),Vector3.Zero,new(6,0,0),.4f);
for(var i=0;i<100&&!wallRoute.Finished;i++)wallRoute.Advance(100,1000);
Check(wallRoute.Path!=null&&wallRoute.Path.Any(p=>p.Z>=1),"prefer space away from wall over shortest hugging route");
var narrowRoute=new CollisionRouteSearch(new WallPreferenceCollision(true),Vector3.Zero,new(6,0,0),.4f);
for(var i=0;i<100&&!narrowRoute.Finished;i++)narrowRoute.Advance(100,1000);
Check(narrowRoute.Path!=null,"narrow passage remains usable despite wall preference");
Check(!WalkingClearance.PreservesRoom(Vector3.Zero,new(2,0,0),p=>p.X>.4f&&p.X<1.6f?1:0),"rounded shortcut cannot approach wall between open endpoints");
Check(WalkingClearance.PreservesRoom(Vector3.Zero,new(2,0,0),_=>1),"smoothing permits consistently narrow passage");
var skipNow=DateTimeOffset.UtcNow;
Check(EventCutsceneRetry.Ready(0,skipNow,default),"first skip pulse allowed");
Check(!EventCutsceneRetry.Ready(1,skipNow,skipNow.AddSeconds(-1)),"skip pulses separated by release interval");
Check(EventCutsceneRetry.Ready(1,skipNow,skipNow.AddSeconds(-2)),"missed early pulse may retry");
Check(!EventCutsceneRetry.Ready(3,skipNow,skipNow.AddSeconds(-20)),"skip retries bounded");
Console.WriteLine("Wall clearance and event skip retry checks passed.");
Check(IndependentDialoguePolicy.OwnedScene(100,65636,42,42,false,true),"matching interacted quest scene becomes owned");
Check(IndependentDialoguePolicy.OwnedScene(100,65636,0,42,true,false),"owned cutscene continuation survives long scene");
Check(!IndependentDialoguePolicy.OwnedScene(100,65637,0,42,true,true),"foreign quest revokes scene ownership");
Check(!IndependentDialoguePolicy.OwnedScene(100,65636,43,42,true,true),"different NPC cannot borrow scene ownership");
Check(!IndependentDialoguePolicy.OwnedScene(100,65636,0,0,false,true),"anonymous scene cannot establish ownership");
using(var combatStep=System.Text.Json.JsonDocument.Parse("{\"InteractionType\":\"Combat\"}"))Check(IndependentStepPolicy.Review(combatStep.RootElement).Contains("manual"),"combat remains manual");
using(var leveStep=System.Text.Json.JsonDocument.Parse("{\"InteractionType\":\"AcceptLeve\"}"))Check(IndependentStepPolicy.Review(leveStep.RootElement).Contains("manual"),"levequests remain manual");
QuestRewardCandidate[] rewardOffers=[new(0,10,1,false,false,50,500,"Upgrade"),new(1,11,2,true,false,0,200,"Coins"),new(2,12,1,false,false,0,1000,"Sell")];
Check(QuestRewardPolicy.Choose(rewardOffers)?.ItemId==11,"gil first over gear and sell value");
Check(QuestRewardPolicy.Choose(rewardOffers.Where(x=>!x.Gil))?.ItemId==10,"usable gear upgrade before vendor fallback");
Check(QuestRewardPolicy.Choose(rewardOffers.Select(x=>x with {Gil=false,Upgrade=0}))?.ItemId==12,"highest total vendor value fallback");
Check(QuestRewardPolicy.Choose(rewardOffers.Select(x=>x with {UniqueOwned=true}))==null,"owned unique rewards excluded");
foreach(var (quest,sequence) in new[]{(162,2),(5490,1),(1556,1),(2560,1)}){
 var imported=book.Find(quest.ToString()).Single().Definition.GetProperty("QuestSequence").EnumerateArray().Single(q=>q.GetProperty("Sequence").GetInt32()==sequence).GetProperty("Steps")[0];
 Check(IndependentStepPolicy.Review(imported)=="",$"reported quest {quest} sequence {sequence} is supported");
}
using(var skipDoc=System.Text.Json.JsonDocument.Parse("""
{"TerritoryId":133,"InteractionType":"Interact","SkipConditions":{"AetheryteShortcutIf":{"InTerritory":[132,133]},"StepIf":{"NearPosition":{"TerritoryId":133,"Position":{"X":2,"Y":0,"Z":0},"MaximumDistance":3}}}}
""")){
 var step=skipDoc.RootElement;
 Check(IndependentStepPolicy.Review(step)=="","known skip groups are supported");
 Check(IndependentSkipPolicy.Matches(step,"AetheryteShortcutIf",132,Vector3.Zero,new byte[6],_=>false,_=>false),"city teleport skip applies in either city zone");
 Check(!IndependentSkipPolicy.Matches(step,"StepIf",132,Vector3.Zero,new byte[6],_=>false,_=>false),"nearby coordinate in wrong territory does not skip quest step");
 Check(IndependentSkipPolicy.Matches(step,"StepIf",133,Vector3.Zero,new byte[6],_=>false,_=>false),"nearby matching territory skips step");
}
using(var unknown=System.Text.Json.JsonDocument.Parse("""{"InteractionType":"Interact","SkipConditions":{"StepIf":{"UnknownCondition":true}}}"""))
 Check(IndependentStepPolicy.Review(unknown.RootElement).Length>0,"unknown skip condition remains blocked");
using(var flight=System.Text.Json.JsonDocument.Parse("""{"InteractionType":"Interact","Fly":true}"""))
 Check(IndependentStepPolicy.Review(flight.RootElement)=="","flight preference permits verified ground route");
var longRoute=new CollisionRouteSearch(new OpenCollision(),Vector3.Zero,new(240,0,0),2.5f);
for(var i=0;i<100&&!longRoute.Finished;i++)longRoute.Advance(100,10000);
Check(longRoute.Path!=null&&longRoute.Path.Length>160,"ground route spans former short search radius");
var stairs=new CollisionRouteSearch(new StairCollision(),Vector3.Zero,new(5,4.5f,0),.3f);
for(var i=0;i<100&&!stairs.Finished;i++)stairs.Advance(100,10000);
Check(stairs.Path!=null&&MathF.Abs(stairs.Path[^1].Y-4.5f)<.1f,"stair endpoint above first ground probe remains reachable through verified edges");
// Navigation expansion, route stitching, progress recovery and flight regression cases.
var extended=new CollisionRouteSearch(new OpenCollision(),Vector3.Zero,new(600,0,0),1);
for(var i=0;i<100&&!extended.Finished;i++)extended.Advance(100,10000);
Check(extended.Path!=null,"ground navigation exceeds the former 512-yalm cap");
var networkRoutes=new[]{new RememberedWalkingRoute(1,new(3,0,0),1,[new(0,0,0),new(1,0,0),new(2,0,0),new(3,0,0)]),new RememberedWalkingRoute(1,new(3,0,3),1,[new(3,0,0),new(3,0,1),new(3,0,2),new(3,0,3)])};
var stitched=WalkingRouteNetwork.Find(networkRoutes,1,Vector3.Zero,new(3,0,3),.5f,(a,b)=>Vector3.Distance(a,b)<.01f);
Check(stitched!=null&&stitched.Any(p=>p==new Vector3(3,0,0)),"recorded routes stitch at shared samples without inventing a diagonal");
Check(WalkingRouteNetwork.Find(networkRoutes,2,Vector3.Zero,new(3,0,3),.5f,(a,b)=>true)==null,"route network does not cross territories");
Check(WalkingRouteNetwork.Find(networkRoutes,1,new(0,8,0),new(3,0,3),.5f,(a,b)=>true)==null,"overlapping upper floor is not a route join");
var recovery=new WalkingRecoveryBudget();
Check(recovery.Try(Vector3.Zero)&&recovery.Try(Vector3.Zero)&&recovery.Try(Vector3.Zero)&&recovery.Try(Vector3.Zero)&&!recovery.Try(Vector3.Zero),"recovery stops after four failures at the same obstruction");
Check(recovery.Try(new(10,0,0)),"meaningful movement restores recovery budget for a different obstacle");
Check(FlightNavigationPolicy.Prefer(true,true,false,100),"long unlocked routes prefer flight");
Check(!FlightNavigationPolicy.Prefer(false,true,true,100),"flight request cannot override unlock restrictions");
Check(!FlightNavigationPolicy.Prefer(true,false,true,100),"explicit ground preference remains ground");
Check(!FlightNavigationPolicy.CanSupply(0,0,.1f,0)&&!FlightNavigationPolicy.CanSupply(float.NaN,0,0,0),"manual vertical input and invalid input are never overwritten");
Check(!FlightNavigationPolicy.Arrived(new(0,5,0),Vector3.Zero,2),"above destination is not a confirmed landing");
var air=new FlightRouteSearch(new AirObstacle(),new(0,4,0),new(16,4,0));
for(var i=0;i<100&&air.Path==null&&air.Error.Length==0;i++)air.Advance(100,10000);
Check(air.Path!=null&&air.Path.Zip(air.Path.Skip(1)).All(p=>new AirObstacle().Clear(p.First,p.Second)),"flight detours around a solid obstacle with every edge checked");
var sealedAir=new FlightRouteSearch(new NoAir(),Vector3.Zero,new(12,0,0));sealedAir.Advance(100,10000);
Check(sealedAir.Path==null&&sealedAir.Error.Length>0,"blocked flight never falls back to unchecked straight movement");
Console.WriteLine("Extended ground maps, recovery and flight policy checks passed.");
using(var levelingRoute=System.Text.Json.JsonDocument.Parse("""
{"QuestSequence":[{"Sequence":0,"Steps":[{"InteractionType":"Interact"}]},{"Sequence":1,"Steps":[{"InteractionType":"Combat"}]},{"Sequence":255,"Steps":[{"InteractionType":"CompleteQuest"}]}]}
""")){
    var start=QuestLevelingPolicy.Assess(levelingRoute.RootElement,0);
    Check(start.SupportedNow&&!start.FullySupported,"leveling can progress MSQ until manual combat");
    Check(!QuestLevelingPolicy.Assess(levelingRoute.RootElement,1).SupportedNow,"leveling skips a current combat step");
    Check(QuestLevelingPolicy.Assess(levelingRoute.RootElement,255).FullySupported,"completed manual combat allows turn-in");
    Check(!QuestLevelingPolicy.Assess(levelingRoute.RootElement,42).SupportedNow,"unknown sequence cannot be selected for leveling");
}
var starter=new QuestLevelingCandidate(1,100,5,[]);
var followup=new QuestLevelingCandidate(2,10000,5,[1]);
Check(QuestLevelingPolicy.Score(starter,[starter,followup],_=>false)>1000,"leveling values a starter unlocking a strong automatic follow-up");
var lockedFollowup=followup with { Previous=[1,3] };
Check(QuestLevelingPolicy.Score(starter,[starter,lockedFollowup],_=>false)==20,"unmet extra prerequisites do not inflate starter XP score");
Console.WriteLine("Quest leveling route suitability and chain scoring passed.");
long Cost(int level)=>level switch{1=>100,2=>200,3=>300,_=>0};
Check(QuestLevelingProgress.Remaining(1,40,3,Cost)==260,"target includes current partial XP and intervening levels");
Check(QuestLevelingProgress.Remaining(3,20,3,Cost)==0,"reached target needs no XP");
Check(QuestLevelingProgress.Remaining(1,0,6,Cost)==null,"unknown level costs cannot produce false ETA");
Check(QuestLevelingProgress.Remaining(1,120,3,Cost)==null,"inconsistent level-up XP waits for fresh observation");
var pace=new QuestLevelingProgress();var at=DateTimeOffset.UtcNow;pace.Begin(40,at);
pace.Observe(140,at.AddMinutes(1));pace.Complete();
Check(pace.SecondsRemaining(200,at.AddMinutes(1))==null,"one quest is not enough for ETA");
pace.Observe(240,at.AddMinutes(2));pace.Complete();
Check(pace.SecondsRemaining(200,at.AddMinutes(2))==120,"observed pace estimates remaining time across level boundaries");
Check(pace.SecondsRemaining(200,at.AddMinutes(13))==null,"stalled progress expires ETA");
pace.Observe(10,at.AddMinutes(14));Check(pace.SecondsRemaining(200,at.AddMinutes(15))==null,"XP reset discards pace");
using(var attune=System.Text.Json.JsonDocument.Parse("""
{"InteractionType":"AttuneAethernetShard","AethernetShard":"[Gridania] Archers' Guild","SkipConditions":{"StepIf":{"AethernetShardUnlocked":"[Gridania] Archers' Guild"}}}
""")){
 Check(IndependentStepPolicy.Review(attune.RootElement)=="","shard attunement is supported explicitly");
 Check(!IndependentSkipPolicy.Matches(attune.RootElement,"StepIf",132,Vector3.Zero,new byte[6],_=>false,_=>false,_=>false),"locked shard is not skipped");
 Check(IndependentSkipPolicy.Matches(attune.RootElement,"StepIf",132,Vector3.Zero,new byte[6],_=>false,_=>false,_=>true),"confirmed unlocked shard is skipped");
}
Console.WriteLine("Level targets, observed ETA and attunement checks passed.");
sealed class NoAir:IFlightCollision {public bool Clear(Vector3 a,Vector3 b)=>false;}
sealed class AirObstacle:IFlightCollision {
 public bool Clear(Vector3 a,Vector3 b){for(var i=0;i<=40;i++){var p=Vector3.Lerp(a,b,i/40f);if(p.X>5&&p.X<11&&MathF.Abs(p.Z)<3&&p.Y<10)return false;}return true;}
}
sealed class StairCollision:IWalkingCollision {
 public bool Ground(Vector3 p,out Vector3 ground){ground=new(p.X,.9f*p.X,p.Z);return p.X>=0&&p.X<=5&&MathF.Abs(p.Z)<.1f&&MathF.Abs(p.Y-ground.Y)<=.7f;}
 public bool Traverse(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<1.5f;
}
sealed class OpenCollision:IWalkingCollision {
 public bool Ground(Vector3 p,out Vector3 ground){ground=p;return true;}
 public bool Traverse(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<2;
}
sealed class WallPreferenceCollision(bool narrow):IWalkingCollision
{
    public bool Ground(Vector3 near,out Vector3 ground){ground=near;return near.X>=0&&near.X<=7&&near.Z>=0&&near.Z<=(narrow?0:3);}
    public bool Traverse(Vector3 from,Vector3 to)=>true;
    public float WallPenalty(Vector3 point)=>point.Z<.75f?1:0;
}
sealed class TestCollision:IWalkingCollision
{
    public bool Ground(Vector3 near,out Vector3 ground){ground=new(near.X,0,near.Z);return MathF.Abs(near.Y)<1&&MathF.Abs(near.X)<=8&&MathF.Abs(near.Z)<=8;}
    public bool Traverse(Vector3 from,Vector3 to){
        if(MathF.Abs(to.Y-from.Y)>.5f)return false;
        for(var i=0;i<=20;i++){var p=Vector3.Lerp(from,to,i/20f);if(p.X>1.4f&&p.X<2.6f&&p.Z<3)return false;}
        return true;
    }
}


