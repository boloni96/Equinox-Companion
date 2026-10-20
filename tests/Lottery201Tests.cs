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
  Check(HousingLottery.RefundAmount("The winning number is...11. May you have better luck in the future!\n\nAccept a full refund of your deposit of 20,000,000 gil?")==20000000,"Exact refund offer");
  Check(HousingLottery.ReceivedGil("You obtain 20,000,000 gil.")==20000000,"Exact gil receipt");
  Check(HousingLottery.ReceivedGil("You spend 20,000,000 gil.")==null,"Spending is not a receipt");
  Check(HousingLottery.RefundAmount("You obtain 20,000,000 gil.")==null,"Receipt alone cannot identify refund");
  var now=DateTimeOffset.Parse("2026-10-20T03:17:00Z");
  Check(HousingLottery.RefundReceiptMatches(1,87,4,20000000,now,1,87,4,20000000,now.AddSeconds(4)),"Matching receipt");
  Check(!HousingLottery.RefundReceiptMatches(1,87,4,20000000,now,2,87,4,20000000,now.AddSeconds(4)),"Different character rejected");
  Check(!HousingLottery.RefundReceiptMatches(1,87,4,20000000,now,1,88,4,20000000,now.AddSeconds(4)),"Different world rejected");
  Check(!HousingLottery.RefundReceiptMatches(1,87,4,20000000,now,1,87,5,20000000,now.AddSeconds(4)),"Different target rejected");
  Check(!HousingLottery.RefundReceiptMatches(1,87,4,20000000,now,1,87,4,10,now.AddSeconds(4)),"Different amount rejected");
  Check(!HousingLottery.RefundReceiptMatches(1,87,4,20000000,now,1,87,4,20000000,now.AddSeconds(31)),"Expired prompt rejected");
  var address=HousingLottery.PlacardAddress("For Sale\n3,750,000 Gil\nPlot 50, 17th Ward, Mist\nSmall\nAvailable to free companies. Available to private buyers.\nSelling via lottery. (Current participants: 16)",now);
  Check(address is {District:"Mist",Ward:17,Plot:50},"Full visible sale placard address");
  Check(HousingLottery.PlacardAddress("Plot 50, 17th Ward, Mist\nPlot 51, 17th Ward, Mist",now)==null,"Conflicting addresses rejected");
  Check(HousingLottery.PlacardAddress("Plot 50, 17th Ward, Mist\nPlot 50, 17th Ward, Mist",now)?.Plot==50,"Repeated text nodes accepted");
  Check(HousingLottery.WinningNumber("The winning number for this plot is 13.")==13,"Nora screenshot winner");
  var nora=new SharedHousingBid("Mist",17,50,5,"fc","","entered","2026-10-14T15:00:00Z","timers",409,"Kraken",now);
  Check(HousingLottery.ObservedStatus(nora,[new("Mist",17,50,409,13,nora.CycleStart)])=="lost","Visitor winner 13 resolves paired entry 5");
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
