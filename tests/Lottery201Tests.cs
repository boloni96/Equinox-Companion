using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Lottery201Tests
{
 [ModuleInitializer]internal static void Run(){
  static void Check(bool b,string why){if(!b)throw new Exception("Lottery201: "+why);}
  Check(HousingLottery.WinningNumber("The winning number is… 5!")==5,"Unicode ellipsis");
  Check(HousingLottery.WinningNumber("The winning number for this plot is\n5.")==5,"Wrapped placard text");
  Check(HousingLottery.WinningNumber("The winning number is 5.")==5,"Plain number");
  Check(HousingLottery.WinningNumber("Your lottery number is 5.")==null,"Never confuse entry number with result");
  Check(HousingLottery.Result("You were not the winner of this lottery.")==null,"Negative winner phrase cannot mark won");
  Check(HousingLottery.Result("Better luck\nin the future.")=="lost","Wrapped losing dialog");
  Check(HousingLottery.Result("You are the winner of this lottery!")=="won","Winning dialog retained");
  Check(HousingLottery.BuyerLabel("fc")=="FC house"&&HousingLottery.BuyerLabel("private")=="Private house","Explicit property types");
  var cycle="2026-10-14T15:00:00Z";
  var bid=new SharedHousingBid("Lavender Beds",19,17,5,"fc","","entered",cycle,"timers",35,"Famfrit",DateTimeOffset.Parse(cycle));
  var result=new LotteryResult("Lavender Beds",19,17,35,3,cycle);
  Check(HousingLottery.ObservedStatus(bid,[result])=="lost","Paired entry loses against observed winner");
  Check(HousingLottery.ObservedStatus(bid,[result with {Winner=5}])=="won","Matching entry wins");
  Check(HousingLottery.ObservedStatus(bid,[result with {WorldId=36}])=="entered","Other world ignored");
  Check(HousingLottery.ObservedStatus(bid,[result with {Plot=18}])=="entered","Other plot ignored");
  Check(HousingLottery.ObservedStatus(bid,[result with {CycleStart="2026-10-05T15:00:00Z"}])=="entered","Old round ignored");
  Check(HousingLottery.ObservedStatus(bid,[result,result with {Winner=5}])=="entered","Conflicting evidence stays unresolved");
  Check(HousingLottery.ObservedStatus(bid with {Status="claimed"},[result])=="claimed","Claim cannot regress");
 }
}
