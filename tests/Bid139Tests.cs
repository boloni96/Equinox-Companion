using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Bid139Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string name){if(!ok)throw new Exception("139: "+name);}
  var json="""{"id":86,"districts":[{"name":"The Lavender Beds","open_plots":[{"world_id":86,"ward_number":0,"plot_number":59,"size":1,"price":17000000,"last_updated_time":1792000000,"first_seen_time":1791000000,"purchase_system":7,"lotto_entries":null,"lotto_phase":null,"lotto_phase_until":null}]}]}""";
  var p=BidSearch.Parse(json,86).Single();Check(p.Ward==1&&p.Plot==60,"zero based address conversion");Check(p.District=="Lavender Beds"&&p.Tenants=="Private / FC","district and tenant flags");Check(p.Entries is null&&p.PhaseLabel(DateTimeOffset.UtcNow)=="Unknown / outdated","unknown not zero or open");
  var rows=new[]{p with{Plot=1,Entries=5,Price=30},p with{Plot=2,Entries=1,Price=20},p with{Plot=3,Entries=null,Price=10}};
  Check(BidSearch.Sort(rows,5,false).Select(x=>x.Plot).SequenceEqual(new[]{2,1,3}),"entries ascending unknown last");Check(BidSearch.Sort(rows,5,true).Select(x=>x.Plot).SequenceEqual(new[]{1,2,3}),"entries descending unknown last");Check(BidSearch.Sort(rows,4,false)[0].Plot==3,"numeric price sorting");
  var now=DateTimeOffset.UtcNow;Check(!(p with{Phase=1,PhaseUntil=now.ToUnixTimeSeconds()-1}).Current(now),"expired phase not accepting");
  var actor=new Actor("123","Bidder",86,86,"Sephirot","Sephirot");var cycle=HousingLottery.Period(now).Start.ToString("O");var bid=new SharedHousingBid("Mist",1,2,3,"private","","won",cycle,"submission",86,"Sephirot",now);
  var c=new SharedCharacter("c","Bidder","Sephirot","Materia","Oceania","Main",[],ContentId:"123",HousingBids:[bid]);var r=new SharedRoster(1,now,[new("p","Person",[c])]);
  var local=new SyncEvent("a","housing.lottery",now,actor,null,Lottery:new("Mist",1,2,3,"private","","entered",cycle,"submission"));
  Check(HousingLotteryNotices.WithShared([local],r).Single().Lottery?.Status=="won","paired result overrides matching local entered");
  r=r with{People=[new("p","Person",[c with{HousingBids=[bid with{Status="claimed"}]}])]};Check(!HousingLotteryNotices.WithShared([local],r).Any(),"claimed bid suppresses reminder");
  Console.WriteLine("PASS 139 Paissa addresses, nullable fields, sorting, phase expiry and shared bid reminders");
 }
}
