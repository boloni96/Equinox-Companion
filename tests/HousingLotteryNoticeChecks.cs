using System.Globalization;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class HousingLotteryNoticeChecks
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool b,string why){if(!b)throw new Exception("Lottery reminder: "+why);}
  var start=HousingLottery.Epoch;var results=start.AddDays(5);var actor=new Actor("123","Bidder",86,86,"Siren","Siren");
  var bid=new LotteryBid("Shirogane",7,4,3,"fc","1234","entered",start.ToString("O"),"submission");
  var e=new SyncEvent("a","housing.lottery",start,actor,null,Lottery:bid);
  Check(!HousingLotteryNotices.Messages([e],"123",start).Any(),"entry phase quiet");
  Check(!HousingLotteryNotices.Messages([e],"456",results).Any(),"only current character");
  Check(!HousingLotteryNotices.Messages([e],"123",start.AddDays(9)).Any(),"old round not current");
  var notice=HousingLotteryNotices.Messages([e],"123",results).Single();
  Check(notice.Text.Contains("Shirogane, Ward 7, Plot 4")&&notice.Text.Contains("Entry #3")&&notice.Text.Contains("FC"),"destination and entry");
  Check(!notice.Text.Contains("Siren"),"world omitted");
  var gate=new LotteryBidNoticeGate();Check(!gate.Due(123,"",start),"warmup");Check(gate.Due(123,notice.Key,results),"online at results transition");Check(!gate.Due(123,notice.Key,results.AddMinutes(1)),"no spam");gate.Reset();Check(!gate.Due(123,notice.Key,results),"relogin warmup");Check(gate.Due(123,notice.Key,results.AddSeconds(20)),"relogin reminder");
  var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new("pt-PT");var text=HousingLotteryNotices.LocalTime(results);Check(text.Contains("AM")||text.Contains("PM"),"Portuguese locale has explicit AM PM");}finally{CultureInfo.CurrentCulture=culture;}
 }
}
