using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Adventure149Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool value,string why){if(!value)throw new Exception("Adventure: "+why);}
  var task=new AdventureTask("t","Tend","Batch 1",false,false);
  var a=new AdventureStop("A","Owner","h",1,[task],"Siren",["a"]);
  var b=new AdventureStop("B","Owner","h2",1,[task],"Siren",["b"]);
  var done=a with {Tasks=[task with {Done=true}]};
  var board=new AdventureBoard("garden:p","generation","2026-10-17T00:00:00Z","garden","Gardening",[a,b],0,1);
  Check(AdventurePolicy.Valid(board),"valid round");Check(!AdventurePolicy.Complete(a with {Tasks=[]}),"empty stop never auto completes");
  Check(!AdventurePolicy.Prompt(null,done),"opening finished stop never nags");Check(AdventurePolicy.Prompt(false,done),"new confirmation prompts");Check(!AdventurePolicy.Prompt(true,done),"same completion never nags");
  Check(AdventurePolicy.FollowIndex(board,"b","Siren")==1,"exact character beats same-world first stop");
  Check(AdventurePolicy.FollowIndex(board,"a","Siren") is null,"matching current stop stays");
  Check(AdventurePolicy.FollowIndex(board,"b","Sophia") is null,"no wrong-world jump");
  Check(AdventurePolicy.FollowIndex(board with {Stops=[done,b]},"","Siren")==1,"world fallback picks unfinished");
  Check(AdventurePolicy.FollowIndex(board with {Stops=[done,done]},"","Siren") is null,"no automatic completed stop");
  Check(!AdventurePolicy.Valid(board with {Index=3}),"bounds validated");
  Console.WriteLine("PASS 149 shared adventure validation, follow character/world, pending completion and no empty-stop completion");
 }
}
