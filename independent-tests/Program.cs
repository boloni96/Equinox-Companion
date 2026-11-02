using EquinoxCompanion;
using System.Numerics;
void Check(bool condition,string name){if(!condition)throw new Exception(name);}
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
Check(WalkingSteering.AdvanceWaypoint(new(1.1f,0,0),[Vector3.Zero,new(1,0,0),new(2,0,0)],1,(_,_)=>false)==1,"passed waypoint does not cut obstacle");
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
