using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Housing202Tests
{
 [ModuleInitializer]internal static void Run(){
  static void Check(bool value,string why){if(!value)throw new Exception("Housing202: "+why);}
  var bid=new LotteryBid("Lavender Beds",19,17,5,"fc","123","lost","2026-10-14T15:00:00Z","manual-refund","Our Company");
  Check(HousingLottery.Valid(bid),"Lost ticket permits explicit refund recovery");
  Check(!HousingLottery.Valid(bid with {Status="entered"}),"Unresolved ticket cannot use refund recovery");
  var shared=new SharedHousingBid("Lavender Beds",19,17,5,"fc","","entered",bid.CycleStart,"timers",35,"Famfrit",DateTimeOffset.UtcNow,FcClaimedAt:DateTimeOffset.UtcNow,FcClaimedOwner:"Hot Pink Haze");
  Check(HousingLottery.BidStatusLabel(shared)=="FC estate claimed · ticket result unknown","Claimed FC is the primary status without guessing ticket winner");
  Check(HousingLottery.BidEvidence(shared).Contains("Hot Pink Haze"),"Claim evidence available on hover");
  Check(AdventurePolicy.IsGardenTask(new("plan","Plan & plant","Garden",false,false,Rule:new(Kind:"planplant"))),"Unified job uses existing garden tools");
 }
}
