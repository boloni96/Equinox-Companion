using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Delivery215Tests
{
 [ModuleInitializer]internal static void Run()
 {
  void Check(bool condition,string label){if(!condition)throw new Exception("Delivery215: "+label);}
  var installed=new SharedRoster(8,DateTimeOffset.UtcNow,[]);
  var valid=new RosterResult(installed,"updated",Receipt:new string('a',64),ReceiptRevision:8,AppliedCursor:19);
  Check(DeliveryPolicy.CanAcknowledge(valid,installed),"installed roster may acknowledge offered receipt");
  Check(!DeliveryPolicy.CanAcknowledge(valid,installed with {Revision=9}),"another revision must not acknowledge token");
  Check(!DeliveryPolicy.CanAcknowledge(valid,null),"no local roster no acknowledgment");
  Check(!DeliveryPolicy.CanAcknowledge(valid with {Roster=null},installed),"failed read cannot acknowledge");
  Check(DeliveryPolicy.CanAcknowledge(valid with {Roster=null,NotModified=true},installed),"304 needs matching installed revision");
  Check(!DeliveryPolicy.CanAcknowledge(valid with {Receipt="unsafe"},installed),"invalid token rejected");
  var peer=new DeliveryPeer(new string('b',32),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,8,19,false);
  Check(DeliveryPolicy.Received(peer,19),"offline peer retains truthful prior receipt");
  Check(!DeliveryPolicy.Received(peer,20),"old receipt is not acknowledgment of new upload");
  Check(!DeliveryPolicy.Received(peer with {ReceivedAt=null},19),"no acknowledgment timestamp cannot prove receipt");
  Check(!DeliveryPolicy.Received(peer,0),"no target is not delivery success");
  var result=SyncReceipt.Parse("{\"accepted\":[\"a\"],\"inserted\":1,\"duplicates\":0,\"uploadedCursor\":19}",["a"]);
  Check(!result.Retry&&result.UploadedCursor==19,"upload response retains own cursor");
  Check(SyncReceipt.Parse("{\"accepted\":[\"a\"],\"uploadedCursor\":-1}",["a"]).UploadedCursor is null,"negative cursor cannot prove delivery");
  var bid=new SharedHousingBid("Mist",1,1,2,"private","","entered","2026-10-14T15:00:00Z","submission",57,"Siren",DateTimeOffset.UtcNow);
  var confirmed=bid with {Status="lost",Winner=3,At=bid.At.AddSeconds(1)};
  Check(BidHistoryPolicy.Prefer(bid,confirmed,true)==confirmed,"current roster replaces stale historical unknown outcome");
  Check(BidHistoryPolicy.Prefer(confirmed,bid with {At=bid.At.AddSeconds(2)},false)==confirmed,"local queued entry never undoes current confirmed loss");
  Check(BidHistoryPolicy.Prefer(bid,confirmed,false)==confirmed,"local newer result can resolve unknown entry");
  Check(BidHistoryPolicy.Key(bid)!=BidHistoryPolicy.Key(bid with {Entry=3}),"paired tickets at same plot stay separate");
  Console.WriteLine("PASS215 receipt revision binding, offline acknowledgment and own upload cursor");
 }
}
