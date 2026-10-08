using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper96Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 96 "+name);}
  var x=new HelperExchange(100,"Apocalypsis Noctis Orchestrion Roll",1,200,"Unidentified Magitek",1);
  Test("shown event trade accepted",HelperExchangePolicy.Valid(x));
  Test("gil shop excluded",!HelperExchangePolicy.Valid(x with {CostName="Gil"}));
  Test("other token exchange excluded",!HelperExchangePolicy.Valid(x with {CostName="Tomestones"}));
  Test("different price rejected",!HelperExchangePolicy.Valid(x with {CostQuantity=2}));
  Test("arbitrary item rejected",!HelperExchangePolicy.Valid(x with {ItemName="Other item"}));
  Test("bounded quantity",!HelperExchangePolicy.Valid(x with {Quantity=100,CostQuantity=100}));
  Test("both inventory deltas required",HelperExchangePolicy.Confirmed(x,0,1,6,5));
  Test("button submission is not purchase",!HelperExchangePolicy.Confirmed(x,0,0,6,6));
  Test("currency-only change is not success",!HelperExchangePolicy.Confirmed(x,0,0,6,5));
  var prompt="Duty calls. Commence battle for “In the Dark of Night”?";
  var npc=new HelperNpc("duty",100,"Destination",10,20,1,new(0,0,0),new(0,0,0),0);
  var duty=new HelperAction("duty","Leader",1,"soloDuty",0,npc,prompt,HelperPolicy.Signature([prompt]),"SelectYesno","quest-step:2",QuestId:68695);
  Test("quest battle prompt parsed",HelperDutyPolicy.QuestTitle(prompt)=="In the Dark of Night");
  Test("cutscene prompt excluded",HelperDutyPolicy.QuestTitle("Skip this cutscene?")=="");
  Test("matching accepted quest can enter",HelperDutyPolicy.Matches(duty,prompt,"In the Dark of Night",true,2));
  Test("wrong quest step cannot enter",!HelperDutyPolicy.Matches(duty,prompt,"In the Dark of Night",true,3));
  Test("unaccepted quest cannot enter",!HelperDutyPolicy.Matches(duty,prompt,"In the Dark of Night",false,2));
  Test("missing step cannot enter",!HelperDutyPolicy.Matches(duty with {Scene=""},prompt,"In the Dark of Night",true,2));
  Test("different prompt cannot enter",!HelperDutyPolicy.Matches(duty,prompt+" Changed", "In the Dark of Night",true,2));
  Test("solo duty establishes quest scope",HelperQuestScope.Evidence([duty])==68695);
  var batch=duty with {Kind="conversation",Steps=[duty with {Id="interact",Kind="interact"},duty]};
  Test("solo duty can end a recorded conversation",HelperConversationPolicy.ValidSteps(batch));
  Test("vendor cannot be hidden inside dialogue replay",!HelperConversationPolicy.ValidSteps(batch with {Steps=[batch.Steps![0],duty with {Kind="vendorExchange",Exchange=x}]}));
 }
}
