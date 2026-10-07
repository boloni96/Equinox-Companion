using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper85Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string n,bool ok){if(!ok)throw new Exception(n);Console.WriteLine("PASS 85 "+n);}
  var npc=new HelperNpc("a",100,"Nananji",130,1,1,new(0,0,0),new(1,0,0),0);
  var first=new HelperAction("1","Leader",1,"interact",1000,npc);
  var offer=first with {Id="2",Kind="acceptQuest",SentAt=3000,QuestId=70315,Scene="offer"};
  var talk=first with {Id="3",Kind="talk",SentAt=6000,Text="Intro"};
  var confirmed=offer with {Id="4",SentAt=9000,Scene="confirmed"};
  var batch=first with {Id="batch",Kind="conversation",SentAt=10000,Steps=[first,offer,talk,confirmed]};
  Test("offer before intro before final confirmation is valid",HelperConversationPolicy.ValidSteps(batch));
  Test("batch without initial NPC interaction rejected",!HelperConversationPolicy.ValidSteps(batch with {Steps=[offer,talk]}));
  Test("other NPC cannot enter conversation",!HelperConversationPolicy.ValidSteps(batch with {Steps=[first,talk with {Npc=npc with {BaseId=101}}]}));
  Test("other leader cannot enter conversation",!HelperConversationPolicy.ValidSteps(batch with {Steps=[first,talk with {Name="Other"}]}));
  Test("duplicate actions rejected",!HelperConversationPolicy.ValidSteps(batch with {Steps=[first,first]}));
  Test("reordered actions rejected",!HelperConversationPolicy.ValidSteps(batch with {Steps=[first,confirmed,talk]}));
  Test("nested batches rejected",!HelperConversationPolicy.ValidSteps(batch with {Steps=[first,batch]}));
  Test("travel cannot be included in quest record",!HelperConversationPolicy.ValidSteps(batch with {Steps=[first,talk with {Kind="teleport"}]}));
  Test("original reading pause retained",HelperConversationPolicy.Delay(1000,12000)==11000);
  Test("fast clicks respect UI settling interval",HelperConversationPolicy.Delay(1000,1001)==450);
  Test("negative timing cannot burst clicks",HelperConversationPolicy.Delay(1000,1)==450);
  Test("decline is cancellation",HelperConversationPolicy.Cancellation(offer with {Scene="decline"}));
  Test("replay No is cancellation",HelperConversationPolicy.Cancellation(offer with {Kind="eventReplay",Scene="no"}));
  Test("checking replay is not consent",!HelperConversationPolicy.Cancellation(offer with {Kind="eventReplay",Scene="checked"}));
 }
}
