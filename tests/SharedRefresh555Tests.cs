using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using EquinoxCompanion;
internal static class SharedRefresh555Tests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("555 "+message);}
    [ModuleInitializer] internal static void Run(){Housing();Network().GetAwaiter().GetResult();}
    private static void Housing()
    {
        var now=DateTimeOffset.Parse("2026-11-14T17:59:30Z");
        var owner=new Actor("123456789","Gideon Bulwark",74,74,"Coeurl","Coeurl");
        var address=new Address("004A03D300150016",74,979,22,23,0,false,false);
        var house=new SharedHouse("private",address.HouseId,"Private house","Home","Coeurl","Empyreum",22,23,"Small",owner.Name,"","",now.AddDays(-9),false);
        var estate=new SyncEvent("owned","house.discovered",now.AddDays(-10),owner,address,House:new("Private house","Small","owned-estate-id"));
        var visit=new HouseObservation("entry",now.AddSeconds(-20),"house.entered",owner,address);
        HousingEstateRow[] Rows(params HouseObservation[] visits)=>HousingDisplayIndex.Build([estate],visits).Characters.Values.SelectMany(c=>c.Estates).ToArray();
        var actual=SharedHouseLocalEntry.Apply(house,Rows(visit),now);
        Check(actual.LastEntry==visit.ObservedAt,"recorded Gideon entry did not override stale profile");
        Check(house.LastEntry==now.AddDays(-9),"server snapshot mutated");
        Check(SharedHouseLocalEntry.Apply(actual,Rows(visit with{ObservedAt=now.AddDays(-2)}),now).LastEntry==actual.LastEntry,"timestamp regressed");
        Check(SharedHouseLocalEntry.Apply(house,Rows(visit with{Actor=owner with{ContentId="987654321",Name="Tenant"}}),now)==house,"tenant counted as owner");
        foreach(var bad in new[]{visit with{Address=address with{Room=1}},visit with{Address=address with{Workshop=true}},visit with{Kind="house.observedInside"},visit with{ObservedAt=now.AddMinutes(1)}})
            Check(SharedHouseLocalEntry.Apply(house,Rows(bad),now)==house,"ineligible/future entry counted");
        Check(SharedHouseLocalEntry.Apply(house with{OwnerName="New Owner"},Rows(visit),now).LastEntry==house.LastEntry,"wrong owner counted");
        var fcEstate=estate with{House=new("Free Company house","Small","owned-estate-id",new("fc123","FC","FC",74))};
        var fc=house with{Type="Free Company house",FcId="fc123"};
        var fcRows=HousingDisplayIndex.Build([fcEstate],[visit]).Characters.Values.SelectMany(c=>c.Estates).ToArray();
        Check(SharedHouseLocalEntry.Apply(fc,fcRows,now).LastEntry==visit.ObservedAt,"proven FC member ignored");
        Check(SharedHouseLocalEntry.Apply(fc with{FcId="other"},fcRows,now).LastEntry==house.LastEntry,"wrong FC counted");
    }
    private static async Task Network()
    {
        var roster=new SharedRoster(12,DateTimeOffset.UtcNow,[],ItemStoragePaging:1);
        var pages=0;var mode="failure";var coreNotModified=false;
        using var sync=new CompanionSync(new Handler(request=>{
            if(request.RequestUri!.Query.Contains("inventoryPage")){
                pages++;
                return mode switch{
                    "failure"=>new(HttpStatusCode.ServiceUnavailable),
                    "conflict"=>new(HttpStatusCode.Conflict),
                    "large"=>new(HttpStatusCode.OK){Content=new StringContent(new string(' ',1200001))},
                    _=>Json(new{snapshot="stable",total=0,locations=Array.Empty<SharedItemLocation>(),next=(int?)null})
                };
            }
            if(coreNotModified)return new(HttpStatusCode.NotModified);
            return Json(roster);
        }));
        var core=await sync.ReadRoster("test",null);
        Check(core.Roster?.Revision==12&&pages==0,"fresh profiles waited for failing inventory pages");
        var failed=await sync.ReadInventory("test",roster);
        Check(failed.Error?.Contains("HTTP 503")==true,"inventory HTTP error collapsed to Unknown");
        var newer=roster with{Revision=13,ItemLocations=[new("c","Owner","bag","Bag",DateTimeOffset.UtcNow,[])]};
        Check(ReferenceEquals(failed.ApplyTo(newer),newer),"failed inventory replaced saved data");
        coreNotModified=true;Check((await sync.ReadRoster("test",12)).NotModified,"profile 304 lost");
        mode="success";var success=await sync.ReadInventory("test",roster);
        Check(success.Error==null&&success.Locations is {Length:0},"inventory retry after profile 304 failed");
        Check(success.ApplyTo(newer).Revision==13&&success.ApplyTo(newer).ItemLocations is {Length:0},"inventory result overwrote newer profiles or failed to clear empty inventory");
        mode="conflict";var before=pages;var changed=await sync.ReadInventory("test",roster);
        Check(pages-before==3&&changed.Error?.Contains("snapshot changed")==true,"changing snapshots not bounded/explained");
        mode="large";Check((await sync.ReadInventory("test",roster)).Error?.Contains("exceeds")==true,"oversized response not explained");
        Console.WriteLine("555 local entry eligibility, stale-date merging, independent profiles, HTTP errors, snapshot retries and response limits passed.");
    }
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value))};
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
    }
}
