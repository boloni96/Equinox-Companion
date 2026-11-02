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
