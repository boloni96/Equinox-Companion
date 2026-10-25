using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace EquinoxCompanion;

public sealed record MarketWatchOffer(string Retainer,long Price,bool Hq,uint Quantity,bool Mannequin);
public sealed record MarketWatchSnapshot(uint World,uint Item,DateTimeOffset Uploaded,DateTimeOffset Checked,MarketWatchOffer[] Offers,string Error="");
public sealed record MarketWatchBatch(MarketWatchSnapshot[] Items,TimeSpan RetryAfter,string Error="");

// Advisory cache only. SellerRun never accepts these records as a live comparison.
public static class MarketWatchPolicy
{
    public static SellerDecision Assess(MarketStock item,uint world,MarketWatchSnapshot? data,ISet<string> ownNames,SellerSettings settings,DateTimeOffset now)
    {
        SellerDecision Unknown(string reason)=>new(SellerStatus.Unknown,null,reason,"Universalis");
        if(settings.ExcludedItems.Contains(item.ItemId))return Unknown("Excluded from price updates by your item rule.");
        if(data==null)return Unknown("Waiting for a background price check.");
        if(data.World!=world||data.Item!=item.ItemId)return Unknown("Background price belongs to another item or world.");
        if(data.Error.Length>0)return Unknown(data.Error+" Live comparison is still available.");
        if(data.Uploaded==default||data.Uploaded>now.AddMinutes(5)||now-data.Uploaded>TimeSpan.FromMinutes(Math.Clamp(settings.BackgroundMaxAgeMinutes,5,1440)))
            return Unknown($"Universalis data is old or unverified (uploaded {data.Uploaded.ToLocalTime():g}). Check live prices at the bell.");
        var competing=data.Offers.Where(x=>x.Hq==item.Hq&&!x.Mannequin&&!ownNames.Contains(x.Retainer)).ToArray();
        if(competing.Length==0)return Unknown("No competing listings of this quality in cached data. Check live listings/history before deciding.");
        // The cache age is checked above; this is deliberately not a live receipt.
        var comparison=new SellerComparison(item.ItemId,world,now,true,competing.Select(x=>new SellerOffer(0,x.Price,x.Hq,x.Quantity)).ToArray(),false,[]);
        var d=SellerPricing.Decide(item.ItemId,world,item.Hq,item.Price,comparison,new HashSet<ulong>(),settings,now);
        return d with {Source="Universalis",Reason=$"Universalis estimate · uploaded {data.Uploaded.ToLocalTime():g} · checked {data.Checked.ToLocalTime():g}. {d.Reason} Live verification required before changing a price."};
    }

    public static MarketWatchSnapshot Parse(JsonElement root,uint world,uint item,DateTimeOffset checkedAt)
    {
        if(!root.TryGetProperty("worldID",out var w)||w.GetUInt32()!=world||!root.TryGetProperty("itemID",out var i)||i.GetUInt32()!=item)
            throw new JsonException("Universalis item/world identity is missing or different.");
        if(root.TryGetProperty("hasData",out var available)&&available.ValueKind==JsonValueKind.False)
            return new(world,item,default,checkedAt,[],"Universalis has no data yet.");
        if(!root.TryGetProperty("lastUploadTime",out var stamp)||!stamp.TryGetInt64(out var ms)||ms<=0)
            return new(world,item,default,checkedAt,[],"Universalis upload time is missing.");
        if(!root.TryGetProperty("listings",out var list)||list.ValueKind!=JsonValueKind.Array)throw new JsonException("Missing listings.");
        var offers=new List<MarketWatchOffer>();
        foreach(var row in list.EnumerateArray()){
            var price=row.GetProperty("pricePerUnit").GetInt64();var quantity=row.GetProperty("quantity").GetUInt32();
            if(price is <=0 or >SellerPricing.MaxPrice||quantity==0)throw new JsonException("Invalid market listing.");
            if(row.TryGetProperty("worldID",out var rw)&&rw.GetUInt32()!=world)throw new JsonException("Listing is from another world.");
            offers.Add(new(row.TryGetProperty("retainerName",out var name)?name.GetString()??"":"",price,row.GetProperty("hq").GetBoolean(),quantity,row.TryGetProperty("onMannequin",out var mannequin)&&mannequin.GetBoolean()));
            if(offers.Count>100)throw new JsonException("Unexpected number of listings.");
        }
        return new(world,item,DateTimeOffset.FromUnixTimeMilliseconds(ms),checkedAt,offers.ToArray());
    }
}

public sealed class MarketWatchClient:IDisposable
{
    private readonly HttpClient http;
    public MarketWatchClient(HttpMessageHandler? handler=null)
    {
        http=handler==null?new():new(handler);http.Timeout=TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("EquinoxCompanion-Market/0.5.1.246");
        http.MaxResponseContentBufferSize=8*1024*1024;
    }
    public async Task<MarketWatchBatch> Read(uint world,uint[] items,CancellationToken token)
    {
        if(world==0||items.Length is 0 or >50||items.Any(x=>x==0))throw new ArgumentException("Invalid world/items.");
        try{
            using var response=await http.GetAsync($"https://universalis.app/api/v2/{world}/{string.Join(",",items)}?listings=100&entries=0",token).ConfigureAwait(false);
            if(response.StatusCode==HttpStatusCode.TooManyRequests){
                var delay=response.Headers.RetryAfter?.Delta??(response.Headers.RetryAfter?.Date-DateTimeOffset.UtcNow)??TimeSpan.FromMinutes(1);
                return new([],TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds,30,900)),"Universalis is busy; background checks are waiting.");
            }
            response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            var root=json.RootElement;var checkedAt=DateTimeOffset.UtcNow;var result=new List<MarketWatchSnapshot>();
            foreach(var item in items){
                try{
                    var entry=items.Length==1?root:root.GetProperty("items").GetProperty(item.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    result.Add(MarketWatchPolicy.Parse(entry,world,item,checkedAt));
                }catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException or FormatException or OverflowException){result.Add(new(world,item,default,checkedAt,[],"No usable Universalis data for this item."));}
            }
            return new(result.ToArray(),TimeSpan.FromSeconds(10));
        }catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch(Exception e) when(e is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException){return new([],TimeSpan.FromMinutes(1),"Universalis could not be reached; prices were not changed.");}
    }
    public void Dispose()=>http.Dispose();
}
