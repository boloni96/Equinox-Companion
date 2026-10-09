using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class EventQuest116Tests
{
 [ModuleInitializer] public static void Run(){
  var q=new EventQuestDefinition(72000,"New festival quest",12,"Moonfire Faire",30,[71999],[new(50000,"Festival reward")]);
  var c=new CollectionDetails("quest",[72000],[],[],1,[q],[72000]);
  void Check(bool ok){if(!ok)throw new Exception("Event quest definition validation failed");}
  Check(EventQuestPolicy.Valid(c));
  Check(!EventQuestPolicy.Valid(c with {Accepted=[72001]}));
  Check(!EventQuestPolicy.Valid(c with {QuestDefinitions=[q with {Name="Invalid\nname"}]}));
  Check(!EventQuestPolicy.Valid(c with {QuestDefinitions=[q with {Id=72001}]}));
  Check(!EventQuestPolicy.Valid(c with {QuestDefinitions=Enumerable.Repeat(q,9).ToArray()}));
  Check(EventQuestPolicy.Valid(new("quest",[68696],[68696],[])));
 }
}
