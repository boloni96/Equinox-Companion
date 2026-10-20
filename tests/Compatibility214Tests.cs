using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Compatibility214Tests {
 [ModuleInitializer] internal static void Run(){
  var now=DateTimeOffset.Parse("2026-10-20T03:55:30Z");
  var bid=new LotteryBid("Shirogane",7,8,9,"fc","","lost","2026-10-14T15:00:00Z","refund-receipt");
  var e=new SyncEvent("receipt","housing.lottery",now,new("123","Test",87,87,"Sophia","Sophia"),null,Lottery:bid);
  if(SyncValidation.SupportedByWebsite(e,22)||!SyncValidation.SupportedByWebsite(e,23))throw new Exception("Automatic refund requires receipt support");
  if(!SyncValidation.SupportedByWebsite(e with {Lottery=bid with {Evidence="manual-refund"}},22))throw new Exception("Manual refund support retained");
  if(!SyncValidation.SupportedByWebsite(e with {Lottery=bid with {Evidence="result-dialog"}},19))throw new Exception("Results continue on previous backend");
 }
}
