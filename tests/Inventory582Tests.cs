using System.Net;
using System.Text.Json;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Inventory582Tests
{
    [ModuleInitializer] internal static void Run()=>Network().GetAwaiter().GetResult();
    private static async Task Network()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS inventory582: "+name);}
        var now=DateTimeOffset.Parse("2026-11-17T18:00:00Z");
        SharedItemLocation Row(string key,int minutes)=>new("owner","Owner",key,key,now.AddMinutes(minutes),[]);
        var saved=new SharedRoster(12,now,[],ItemLocations:[Row("unread",0),Row("newer-local",5),Row("updated",0)],ItemStoragePaging:1);
        var attempts=0;
        using var sync=new CompanionSync(new Handler(request=>{
            if(request.RequestUri!.Query.Contains("inventoryPage=0")){
                attempts++;
                var row=attempts switch{1=>Row("updated",2),2=>Row("newer-local",1),_=>Row("discovered",3)};
                return Json(new{snapshot="s"+attempts,total=2,locations=new[]{row},next=1});
            }
            return new(HttpStatusCode.Conflict);
        }));
        var result=await sync.ReadInventory("test",saved);
        Check("conflicts retain independently read locations",!result.Complete&&result.Locations?.Length==3&&result.Error==null&&result.Notice?.Contains("full refresh will retry")==true&&attempts==3);
        var merged=result.ApplyTo(saved);
        Check("unread storage is never erased",merged.ItemLocations!.Any(l=>l.Key=="unread"));
        Check("fresh records replace stale quantities",merged.ItemLocations!.Single(l=>l.Key=="updated").At==now.AddMinutes(2));
        Check("older partial records cannot overwrite newer data",merged.ItemLocations!.Single(l=>l.Key=="newer-local").At==now.AddMinutes(5));
        Check("newly seen storage added once",merged.ItemLocations!.Length==4&&result.ApplyTo(merged).ItemLocations!.Length==4);
        Check("partial coverage stays explicit and profiles survive",merged.ItemLocationsTruncated&&merged.Revision==12);
        var complete=new InventoryReadResult([Row("only remaining",7)],null).ApplyTo(merged);
        Check("complete snapshot can remove deleted locations",!complete.ItemLocationsTruncated&&complete.ItemLocations!.Length==1);
        using var bad=new CompanionSync(new Handler(_=>Json(new{snapshot="s",total=2,locations=new[]{Row("invalid",0)},next=0})));
        var invalid=await bad.ReadInventory("test",saved);
        Check("invalid continuation never applies partial data",invalid.Error!=null&&ReferenceEquals(invalid.ApplyTo(saved),saved));
    }
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value))};
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
    }
}
