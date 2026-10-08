using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper94Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 94 "+name);}
  var npc=new HelperNpc("turn-in",1026849,"Kipih Jakkya",130,1,1,new(0,0,0),new(0,0,0),0);
  var result=new HelperAction("complete","Leader",1,"completeQuest",0,npc,Addon:"JournalResult",Scene:"complete",QuestId:68694);
  HelperAction Talk(string id,string text)=>new(id,"Leader",1,"talk",1,npc,text,HelperPolicy.Signature(["",text]),"Talk","68694:15",QuestId:68694);
  var pages=new[]{Talk("1","The FATE “Like Clockwork” will appear at the Clutch in central Thanalan over the course of the seasonal event."),Talk("2","Completing this FATE will reward players with pieces of unidentified magitek."),Talk("3","These items may be exchanged for various goods by speaking with an Ironworks hand."),Talk("4","Ironworks hands are posted at Black Brush and in each of the three city-states.")};
  Test("turn-in can drain all four recorded information pages before final verification",HelperQuestResultPolicy.FollowupTalkCount(result,pages)==4);
  Test("missing post-result dialogue never assumes completion",HelperQuestResultPolicy.FollowupTalkCount(result,[])==0);
  Test("decline does not drain completion pages",HelperQuestResultPolicy.FollowupTalkCount(result with {Scene="decline"},pages)==0);
  Test("unrelated quest cannot be advanced",HelperQuestResultPolicy.FollowupTalkCount(result,[pages[0] with {Scene="68695:1"}])==0);
  Test("different NPC interaction cannot be advanced",HelperQuestResultPolicy.FollowupTalkCount(result,[pages[0] with {Npc=npc with {Conversation="new"}}])==0);
  Test("unverified line cannot be advanced",HelperQuestResultPolicy.FollowupTalkCount(result,[pages[0] with {Signature=""}])==0);
  Test("choice is a verification boundary",HelperQuestResultPolicy.FollowupTalkCount(result,[pages[0],pages[1] with {Kind="choice"},pages[2]])==1);
  Test("new quest is a verification boundary",HelperQuestResultPolicy.FollowupTalkCount(result,[..pages,result with {QuestId=68695}])==4);
  Test("other leader cannot supply follow-up",HelperQuestResultPolicy.FollowupTalkCount(result,[pages[0] with {Name="Other"}])==0);
 }
}
