using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Housing148Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool b,string why){if(!b)throw new Exception("My Empire: "+why);}
  var time=DateTimeOffset.Parse("2026-10-16T00:00:00Z");
  var owner=new Actor("123456789","Owner",409,409,"Sophia","Sophia");
  var member=owner with {ContentId="987654321",Name="Member"};
  var guest=owner with {ContentId="111111111",Name="Guest"};
  var address=new Address("estate",409,641,7,8,0,false,false);
  var estate=new SyncEvent("estate","house.discovered",time,owner,address,House:new("Private house","Medium","owned-estate-id"));
  var fc=estate with {Id="fc",Address=address with {HouseId="fc"},House=new("Free Company house","Medium","owned-estate-id",new("999","FC","FC",409))};
  var linked=fc with {Id="member",Actor=member};
  var events=new[]{estate,fc,linked,estate with {Id="old",At=time.AddDays(-1)}};
  var visits=new[]{new HouseObservation("owner",time,"house.entered",owner,address),
   new HouseObservation("guest",time.AddDays(1),"house.entered",guest,address),
   new HouseObservation("member",time.AddDays(2),"house.entered",member,fc.Address!),
   new HouseObservation("workshop",time.AddDays(3),"house.entered",owner,fc.Address! with {Workshop=true}),
   new HouseObservation("other",time,"house.entered",owner,address with {HouseId="other"})};
  var index=HousingDisplayIndex.Build(events,visits);
  Check(index.Characters[owner.ContentId].Estates.Length==2,"latest estate per address");
  foreach(var row in index.Characters.Values)foreach(var e in row.Estates)
   Check(e.Entry==HousingStatus.LastEligibleEntry(e.Estate,events,visits),"indexed eligibility matches private/FC rules");
  Check(index.Characters[owner.ContentId].Estates[0].Entry==time,"guest excluded");
  Check(index.Characters[owner.ContentId].Estates[1].Entry==time.AddDays(2),"member included, workshop excluded");
  Check(index.Characters[owner.ContentId].OtherVisits.Single().Address.HouseId=="other","other visits separated");
  var cache=new UiProjectionCache<HousingDisplayIndex>(0);var builds=0;
  HousingDisplayIndex Build(){builds++;return HousingDisplayIndex.Build(events,visits);}
  for(var i=0;i<10000;i++)Check(ReferenceEquals(cache.Get(events,"1",i,Build),cache.Get(events,"1",i,Build)),"stable frame reuses index");
  Check(builds==1,"unchanged frames never rescan history");
  cache.Get(events,"2",10001,Build);Check(builds==2,"changed records invalidate cache");
  var updated=HousingDisplayIndex.Build(events,visits.Append(new("new",time.AddDays(4),"house.entered",owner,address)));
  Check(updated.Characters[owner.ContentId].Estates[0].Entry==time.AddDays(4),"new eligible entry reflected");
  Check(index.Characters[owner.ContentId].Estates[0].Entry==time,"derived snapshot preserves prior records");
  Console.WriteLine("PASS 148 My Empire indexed history, private/FC eligibility, changed entries and 10000 cached frames");
 }
}
