using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper90Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 90 "+name);}
  var npc=new HelperNpc("a",2009662,"Destination",148,1,1,new(0,0,0),new(0,0,0),0);
  var a=new HelperAction("a","Leader",1,"interact",1,npc,Text:"EventObj",Scene:"68694:2",QuestId:68694);
  Test("quest destination event authorizes conversation",HelperQuestScope.Evidence([a])==68694);
  Test("object identity alone does not authorize",HelperQuestScope.Evidence([a with {Scene="",QuestId=0}])==0);
  Test("vendor event cannot authorize destination",HelperQuestScope.Evidence([a with {Scene="262145:2"}])==0);
  Test("native quest type",HelperQuestScenePolicy.Quest(68694));
  Test("empty quest entry rejected",!HelperQuestScenePolicy.Quest(65536));
  Test("vendor handler rejected",!HelperQuestScenePolicy.Quest(262145));
  Test("quest object kind retained",HelperQuestScenePolicy.ObjectKind("EventObj")=="EventObj");
  Test("legacy NPC kind retained",HelperQuestScenePolicy.ObjectKind("")=="EventNpc");
  Test("unknown kind cannot select arbitrary object",HelperQuestScenePolicy.ObjectKind("Treasure")=="EventNpc");
  var confirm=a with {Id="b",Text="confirmQuestScene"};
  Test("confirmation bound to quest scene",HelperQuestScenePolicy.Confirmation(confirm));
  Test("mismatched quest confirmation rejected",!HelperQuestScenePolicy.Confirmation(confirm with {QuestId=68695}));
  Test("marker and confirmation form valid atomic batch",HelperConversationPolicy.ValidSteps(a with {Kind="conversation",Steps=[a,confirm]}));
  Test("same current step allowed",HelperQuestScenePolicy.SameStep("quest-step:2",true,2));
  Test("different step blocked",!HelperQuestScenePolicy.SameStep("quest-step:2",true,3));
  Test("unaccepted step blocked",!HelperQuestScenePolicy.SameStep("quest-step:2",false,2));
  Test("new quest offer without prerequisite allowed",HelperQuestScenePolicy.SameStep("",false,0));
  Test("first-name dialogue canonicalized",HelperQuestScenePolicy.Canonical("A favor, Nala.","Nala Mooncloud")=="A favor, {player}.");
  Test("full-name dialogue canonicalized",HelperQuestScenePolicy.Canonical("Nala Mooncloud!","Nala Mooncloud")=="{player}!");
  Test("partial-name collision preserved",HelperQuestScenePolicy.Canonical("Nalan","Nala Mooncloud")=="Nalan");
  Test("dialogue whitespace normalized",HelperQuestScenePolicy.Canonical("hello\r\n world")=="hello world");
 }
}
