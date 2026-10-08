using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper89Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 89 "+name);}
  var npc=new HelperNpc("a",1,"Vendor",130,1,1,new(0,0,0),new(0,0,0),0);
  var a=new HelperAction("a","Leader",1,"interact",1,npc);
  Test("bare vendor click is not quest evidence",HelperQuestScope.Evidence([a])==0);
  Test("vendor welcome dialogue is ignored",HelperQuestScope.Evidence([a,a with {Kind="talk",Text="Welcome!",Scene="262145:1"}])==0);
  Test("purchase menu is ignored",HelperQuestScope.Evidence([a,a with {Kind="choice",Text="Purchase items",Scene="262145:2"}])==0);
  Test("named quest lookalike in generic menu is insufficient",HelperQuestScope.Evidence([a with {Kind="choice",QuestId=70315,Text="Remembering the Past"}])==0);
  Test("ordinary NPC chatter is ignored",HelperQuestScope.Evidence([a with {Kind="talk",Scene=""}])==0);
  Test("native quest dialogue remains supported",HelperQuestScope.Evidence([a,a with {Kind="talk",Scene="70315:2"}])==70315);
  Test("native quest choice remains supported",HelperQuestScope.Evidence([a with {Kind="choice",Scene="70315:3"}])==70315);
  Test("quest cutscene skip remains supported",HelperQuestScope.Evidence([a with {Kind="skip",Scene="70315:3"}])==70315);
  foreach(var scene in new[]{"offer","decline","confirmed"})Test("verified quest "+scene,HelperQuestScope.Evidence([a with {Kind="acceptQuest",QuestId=70315,Addon="JournalAccept",Scene=scene}])==70315);
  Test("wrong addon cannot authorize quest",HelperQuestScope.Evidence([a with {Kind="acceptQuest",QuestId=70315,Addon="Shop",Scene="offer"}])==0);
  var replay=a with {Kind="eventReplay",Npc=npc with {Name="Kipih Jakkya"},QuestId=68694,Addon="SelectYesno",Scene="yes",Text="Do you wish to replay the event?"};
  Test("known seasonal replay remains supported",HelperQuestScope.Evidence([replay])==68694);
  Test("unrelated YesNo remains ignored",HelperQuestScope.Evidence([replay with {Text="Purchase this item?"}])==0);
  Test("replay prompt at wrong NPC ignored",HelperQuestScope.Evidence([replay with {Npc=npc}])==0);
  Test("bad native scene ignored",HelperQuestScope.Evidence([a with {Kind="talk",Scene="invalid:0"}])==0);
 }
}
