using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper91Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 91 "+name);}
  var npc=new HelperNpc("a",1,"Kipih Jakkya",130,1,1,new(0,0,0),new(0,0,0),0);
  var a=new HelperAction("a","Leader",1,"interact",1,npc,Scene:"68694:2",QuestId:68694);
  var result=a with {Id="b",Kind="completeQuest",Addon="JournalResult",Scene="complete"};
  Test("native quest result completion allowed",HelperQuestResultPolicy.Valid(result));
  Test("decline is distinct from complete",HelperQuestResultPolicy.Valid(result with {Scene="decline"}));
  Test("unconfirmed completion blocked",!HelperQuestResultPolicy.Valid(result with {Scene="pending"}));
  Test("wrong result window blocked",!HelperQuestResultPolicy.Valid(result with {Addon="Shop"}));
  Test("unknown quest blocked",!HelperQuestResultPolicy.Valid(result with {QuestId=0}));
  Test("vendor event cannot become completion",!HelperQuestResultPolicy.Valid(result with {QuestId=262145}));
  Test("completion follows interaction atomically",HelperConversationPolicy.ValidSteps(a with {Kind="conversation",Steps=[a,result]}));
  Test("wrong conversation rejected",!HelperConversationPolicy.ValidSteps(a with {Kind="conversation",Steps=[a,result with {Npc=npc with {Conversation="b"}}]}));
 }
}
