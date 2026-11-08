using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class HousingStorage507Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool v,string why){if(!v)throw new Exception("Housing507: "+why);}
  var at=DateTimeOffset.UtcNow;
  var h=new SharedHouse("h","0000000000000001","Private house","Home","Rafflesia","Shirogane",15,15,"Medium","Owner","","",null,false);
  var other=h with{Id="other",GameHouseId="0000000000000002",Name="Other home"};
  var apartment=h with{Id="room",GameHouseId="0000000000000003",Type="Apartment",Plot=0};
  var c=new SharedCharacter("c","Owner","Rafflesia","Dynamis","NA","Main",[h,other,apartment]);
  var key="house:"+h.GameHouseId+":27001";
  var shared=new SharedItemLocation("house:"+h.GameHouseId,"Owner",key,"Inside storeroom",at,[10],[new(10,2,false,"Furniture")]);
  var roster=new SharedRoster(1,at,[new("p","Person",[c,c with{Id="tenant",Houses=[h]}])],ItemLocations:[shared]);
  var homes=HousingStorageCatalog.Build(roster,[]);
  Check(homes.Length==3&&homes.Single(x=>x.Id==h.GameHouseId).Locations.Length==1,"shared homes deduplicated across characters and shared storage visible");
  Check(homes.Single(x=>x.Id==other.GameHouseId).Locations.Length==0,"unrecorded houses stay visible");
  Check(!homes.Single(x=>x.Id==apartment.GameHouseId).Outside,"apartment does not invent outside storage");
  var newer=new SyncEvent("new","storage.observed",at.AddMinutes(1),new("9","Tenant",1,1),null,Storage:new(key,"Inside storeroom",[],[]));
  var stale=newer with{Id="old",At=at.AddMinutes(-1),Storage=new(key,"Inside storeroom",[10],[new(10,7,false,"Furniture")])};
  var merged=HousingStorageCatalog.Build(roster,[newer,stale]);
  Check(merged.Single(x=>x.Id==h.GameHouseId).Locations.Single().Items.Length==0,"newer empty local snapshot replaces stale shared/local stock");
  Check(HousingStorageCatalog.Inside(key)&&!HousingStorageCatalog.Inside("house:"+h.GameHouseId+":27000"),"inside and outside storerooms distinct");
  Check(HousingStorageCatalog.Inside("house:"+h.GameHouseId+":25003")&&!HousingStorageCatalog.Inside("house:"+h.GameHouseId+":25200"),"placed inventories classified separately by side");
  var index=new DiscoveryReadIndex();index.Refresh([newer,stale]);Check(index.AllStorage().Single().At==newer.At,"shared display uses latest indexed observations without full history scans");
  Console.WriteLine("507 shared housing deduplication, inside/outside, unrecorded homes and newest-empty snapshot tests passed.");
 }
}
