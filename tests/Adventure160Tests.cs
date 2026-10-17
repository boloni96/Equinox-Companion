using System.Runtime.CompilerServices;
namespace EquinoxCompanion.Tests;
public static class Adventure160Tests
{
 [ModuleInitializer] public static void Run()
 {
  var task=new AdventureTask("bed","Inspect bed","Garden",false,false);
  var board=new AdventureBoard("garden:p","generation","2026-10-17","garden","Garden",[new("House","Siren","h",1,[task])],0,1,Protocol:2);
  AdventureBoard[] current=[board];
  var same=board with {Stops=[board.Stops[0] with {Tasks=[task with {}]}]};
  if(!ReferenceEquals(current,AdventurePolicy.Reconcile(current,[same],false)))throw new Exception("Unchanged polls must preserve the view and its cached layout");
  var complete=board with {Revision=2,Stops=[board.Stops[0] with {Tasks=[task with {Done=true,ServerConfirmed=true}]}]};
  var updated=AdventurePolicy.Reconcile(current,[complete],false);
  if(!updated[0].Stops[0].Tasks[0].Done||ReferenceEquals(current,updated))throw new Exception("New game observations must update the view");
  var other=board with {Id="multi"};
  var partial=AdventurePolicy.Reconcile([board,other],[complete],true);
  if(partial.Length!=2||!ReferenceEquals(partial[1],other))throw new Exception("Saving one route must preserve the others");
  if(AdventurePolicy.Reconcile([board,other],[board],false).Length!=1)throw new Exception("Deleted routes must disappear on a full response");
  var rebuilt=board with {Generation="new",Revision=1};
  if(!ReferenceEquals(AdventurePolicy.Reconcile(current,[rebuilt],false)[0],rebuilt))throw new Exception("Rebuilt routes must not reuse old task state");
  Console.WriteLine("PASS 160 unchanged-poll stability, confirmed updates, partial saves and rebuilt/deleted adventures");
 }
}
