using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class HousingLotteryChecks
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool b,string why){if(!b)throw new Exception("Lottery: "+why);}
  var start=HousingLottery.Epoch;
  Check(HousingLottery.Period(start).Entry,"epoch entry");
  Check(!HousingLottery.Period(start.AddDays(5)).Entry,"results boundary");
  Check(HousingLottery.Period(start.AddDays(9)).Start==start.AddDays(9),"next round");
  Check(HousingLottery.Period(start.ToOffset(TimeSpan.FromHours(-1))).Start==start,"timezone invariant");
  Check(HousingLottery.WinningNumber("The winning number for this plot is 42.")==42,"public winning number");
  Check(HousingLottery.WinningNumber("The winning number is...3! Congratulations!")==3,"personal winning number");
  Check(HousingLottery.WinningNumber("Your lottery number is 3.")==null,"entry is not winning number");
  Check(HousingLottery.WinningNumber("The winning number for this plot is 0.")==null,"no winner not a loss");
  Check(HousingLottery.Parse("Plot 4, 7th Ward, Shirogane (Small)","Lottery Number: 3","fc",start,"timers") is {Entry:3,Ward:7,Plot:4,Buyer:"fc"},"entry identity");
  Check(HousingLottery.Parse("Plot 61, 7th Ward, Shirogane","Lottery Number: 3","fc",start,"timers")==null,"invalid plot");
  var login=new LotteryLogin();Check(!login.Due(1,start,true,false),"login grace");Check(login.Due(1,start.AddSeconds(20),true,false),"login notice");Check(!login.Due(1,start.AddMinutes(1),true,false),"notice once");
  Check(!HousingLottery.ValidResult(new("Mist",1,1,86,3,start.ToString("O")),start),"results disallowed during entry");
 }
}
