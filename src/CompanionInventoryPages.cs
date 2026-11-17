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
            return await ReadInventoryPages(key,deadline.Token);
        }
        catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or ProfileReadException or IOException){
            return new(null,ProfileFailure("Shared inventory",ex)+"; keeping saved inventory.");
        }
    }
    private sealed record InventoryPage(string Snapshot,int Total,SharedItemLocation[] Locations,int? Next);
    private async Task<InventoryReadResult> ReadInventoryPages(string key,CancellationToken token)
    {
        var conflicts=new List<string>();
        // Every location is an independently timestamped storage observation.
        // Retain validated observations across snapshot conflicts, but never treat
        // their union as a complete snapshot or delete locations we did not read.
        var observed=new Dictionary<(string Owner,string Key),SharedItemLocation>();
        for(var attempt=0;attempt<3;attempt++){
            var locations=new List<SharedItemLocation>();var seen=new HashSet<(string,string)>();
            string? snapshot=null;var offset=0;var total=-1;var restart=false;
            for(var pageNumber=0;pageNumber<1000;pageNumber++){
                var url="https://equinoxjournal.pages.dev/api/companion/roster?inventoryPage="+offset;
                if(snapshot!=null)url+="&snapshot="+Uri.EscapeDataString(snapshot);
                using var request=new HttpRequestMessage(HttpMethod.Get,url);
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);AddInstallationHeader(request);
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
                if(response.StatusCode==HttpStatusCode.Conflict){conflicts.Add($"attempt {attempt+1}: page {pageNumber+1}, {locations.Count}/{(total<0?"unknown":total.ToString())} locations received");restart=true;break;}
                if(!response.IsSuccessStatusCode)throw new HttpRequestException("Shared inventory page unavailable",null,response.StatusCode);
                var page=JsonSerializer.Deserialize<InventoryPage>(await ReadProfileBody(response,1200000,token),Json);
                if(page==null||string.IsNullOrWhiteSpace(page.Snapshot)||page.Snapshot.Length>100||page.Total is <0 or >50000||page.Locations==null||page.Locations.Length>80||
                    snapshot!=null&&(snapshot!=page.Snapshot||total!=page.Total))throw new JsonException("Invalid shared inventory page");
                snapshot=page.Snapshot;total=page.Total;
                foreach(var row in page.Locations){
                    if(row==null||string.IsNullOrEmpty(row.OwnerId)||string.IsNullOrEmpty(row.Key)||row.Items==null||row.Items.Length>10000||row.Stacks?.Length>10000||!seen.Add((row.OwnerId,row.Key)))throw new JsonException("Invalid shared inventory location");
                    locations.Add(row);
                    var identity=(row.OwnerId,row.Key);
                    if(!observed.TryGetValue(identity,out var previous)||row.At>previous.At)observed[identity]=row;
                }
                if(page.Next==null){if(locations.Count!=total)throw new JsonException("Incomplete shared inventory");return new(locations.ToArray(),null);}
                if(page.Next!=locations.Count||page.Next<=offset||page.Next>=total)throw new JsonException("Invalid inventory continuation");
                offset=page.Next.Value;
            }
            if(!restart)break;
        }
        if(observed.Count>0)return new(observed.Values.ToArray(),null,false,
            $"Shared inventory: refreshed {observed.Count} storage locations; inventory changed during download. Unread locations kept; full refresh will retry.");
        throw new ProfileReadException("inventory snapshot changed during all three download attempts ("+string.Join("; ",conflicts)+")");
    }
}

public sealed record InventoryReadResult(SharedItemLocation[]? Locations,string? Error,bool Complete=true,string? Notice=null)
{
    public SharedRoster ApplyTo(SharedRoster current)
    {
        if(Locations is not {} items)return current;
        if(Complete)return current with{ItemLocations=items,ItemLocationsTruncated=false};
        var merged=(current.ItemLocations??[]).GroupBy(l=>(l.OwnerId,l.Key)).ToDictionary(g=>g.Key,g=>g.MaxBy(l=>l.At)!);
        foreach(var item in items){
            var key=(item.OwnerId,item.Key);
            if(!merged.TryGetValue(key,out var previous)||item.At>previous.At)merged[key]=item;
        }
        return current with{ItemLocations=merged.Values.ToArray(),ItemLocationsTruncated=true};
    }
}
