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
sealed class TestCollision:IWalkingCollision
{
    public bool Ground(Vector3 near,out Vector3 ground){ground=new(near.X,0,near.Z);return MathF.Abs(near.Y)<1&&MathF.Abs(near.X)<=8&&MathF.Abs(near.Z)<=8;}
    public bool Traverse(Vector3 from,Vector3 to){
        if(MathF.Abs(to.Y-from.Y)>.5f)return false;
        for(var i=0;i<=20;i++){var p=Vector3.Lerp(from,to,i/20f);if(p.X>1.4f&&p.X<2.6f&&p.Z<3)return false;}
        return true;
    }
}
