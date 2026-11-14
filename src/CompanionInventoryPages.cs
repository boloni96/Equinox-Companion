using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
namespace EquinoxCompanion;
public sealed partial class CompanionSync
{
    public async Task<InventoryReadResult> ReadInventory(string key,SharedRoster roster)
    {
        try{
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(90));
            var updated=await ReadInventoryPages(key,roster,deadline.Token);return new(updated.ItemLocations,null);
        }
        catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or ProfileReadException or IOException){
            return new(null,ProfileFailure("Shared inventory",ex)+"; keeping saved inventory.");
        }
    }
    private sealed record InventoryPage(string Snapshot,int Total,SharedItemLocation[] Locations,int? Next);
    private async Task<SharedRoster> ReadInventoryPages(string key,SharedRoster roster,CancellationToken token)
    {
        for(var attempt=0;attempt<3;attempt++){
            var locations=new List<SharedItemLocation>();var seen=new HashSet<(string,string)>();
            string? snapshot=null;var offset=0;var total=-1;var restart=false;
            for(var pageNumber=0;pageNumber<1000;pageNumber++){
                var url="https://equinoxjournal.pages.dev/api/companion/roster?inventoryPage="+offset;
                if(snapshot!=null)url+="&snapshot="+Uri.EscapeDataString(snapshot);
                using var request=new HttpRequestMessage(HttpMethod.Get,url);
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);AddInstallationHeader(request);
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
                if(response.StatusCode==HttpStatusCode.Conflict){restart=true;break;}
                if(!response.IsSuccessStatusCode)throw new HttpRequestException("Shared inventory page unavailable",null,response.StatusCode);
                var page=JsonSerializer.Deserialize<InventoryPage>(await ReadProfileBody(response,1200000,token),Json);
                if(page==null||string.IsNullOrWhiteSpace(page.Snapshot)||page.Snapshot.Length>100||page.Total is <0 or >50000||page.Locations==null||page.Locations.Length>80||
                    snapshot!=null&&(snapshot!=page.Snapshot||total!=page.Total))throw new JsonException("Invalid shared inventory page");
                snapshot=page.Snapshot;total=page.Total;
                foreach(var row in page.Locations){
                    if(row==null||string.IsNullOrEmpty(row.OwnerId)||string.IsNullOrEmpty(row.Key)||row.Items==null||row.Items.Length>10000||row.Stacks?.Length>10000||!seen.Add((row.OwnerId,row.Key)))throw new JsonException("Invalid shared inventory location");
                    locations.Add(row);
                }
                if(page.Next==null){if(locations.Count!=total)throw new JsonException("Incomplete shared inventory");return roster with{ItemLocations=locations.ToArray(),ItemLocationsTruncated=false};}
                if(page.Next!=locations.Count||page.Next<=offset||page.Next>=total)throw new JsonException("Invalid inventory continuation");
                offset=page.Next.Value;
            }
            if(!restart)break;
        }
        throw new ProfileReadException("inventory snapshot changed during all three download attempts");
    }
}

public sealed record InventoryReadResult(SharedItemLocation[]? Locations,string? Error)
{
    public SharedRoster ApplyTo(SharedRoster current)=>Locations is {} items?current with{ItemLocations=items,ItemLocationsTruncated=false}:current;
}
