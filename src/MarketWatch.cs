using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace EquinoxCompanion;

public sealed record MarketWatchOffer(string Retainer,long Price,bool Hq,uint Quantity,bool Mannequin,string? Materia="");
public sealed record MarketWatchSnapshot(uint World,uint Item,DateTimeOffset Uploaded,DateTimeOffset Checked,MarketWatchOffer[] Offers,string Error="",SellerSale[]? History=null);
public sealed record MarketWatchBatch(MarketWatchSnapshot[] Items,TimeSpan RetryAfter,string Error="");

// Advisory cache only. SellerRun never accepts these records as a live comparison.
public static class MarketWatchPolicy
{
    public static DateTimeOffset NextCheck(MarketWatchBatch batch,uint item,DateTimeOffset now,int minutes)
    {
        var row=batch.Items.FirstOrDefault(x=>x.Item==item);
        return row is {Error:""}?now.AddMinutes(Math.Clamp(minutes,5,120)):now+TimeSpan.FromSeconds(Math.Max(60,batch.RetryAfter.TotalSeconds));
    }
    public static string Summary(MarketWatchBatch batch)=>batch.Error.Length>0?batch.Error:
        $"Universalis: {batch.Items.Count(x=>x.Error.Length==0)}/{batch.Items.Length} items received"+
        (batch.Items.Any(x=>x.Error.Length>0)?" · unavailable items will retry. Data age is shown on each listing.":" · data age is shown on each listing.");

    public static SellerDecision Assess(MarketStock item,uint world,MarketWatchSnapshot? data,ISet<string> ownNames,SellerSettings settings,DateTimeOffset now,bool levelOneOutfit=false)
    {
        SellerDecision Unknown(string reason)=>new(SellerStatus.Unknown,null,reason,"Universalis");
        if(settings.ExcludedItems.Contains(item.ItemId))return Unknown("Excluded from price updates by your item rule.");
        if(data==null)return Unknown("Waiting for a background price check.");
        if(data.World!=world||data.Item!=item.ItemId)return Unknown("Background price belongs to another item or world.");
        var checkedText=$"Equinox last checked: {data.Checked.ToLocalTime():g}. Market data uploaded: {(data.Uploaded==default?"not reported":data.Uploaded.ToLocalTime().ToString("g"))}. ";
        if(data.Error.Length>0)return Unknown(checkedText+data.Error+" Live comparison is still available.");
        if(data.Uploaded==default||data.Uploaded>now.AddMinutes(5)||now-data.Uploaded>TimeSpan.FromMinutes(Math.Clamp(settings.BackgroundMaxAgeMinutes,5,1440)))
            return Unknown(checkedText+"Old market data. Universalis has not supplied a recent upload; requesting it again does not perform an in-game search. Check live prices at the bell.");
        var offers=data.Offers.Where(x=>((settings.CombineLevelOneOutfitQuality&&levelOneOutfit)||x.Hq==item.Hq)&&!x.Mannequin).ToArray();
        if(offers.Length==0)return Unknown(checkedText+"No listings of this quality in cached data. Check live listings/history before deciding.");
        // The cache age is checked above; this is deliberately not a live receipt.
        // Synthetic IDs only distinguish known names on this world from other offers.
        var comparison=new SellerComparison(item.ItemId,world,now,true,offers.Select(x=>new SellerOffer(x.Retainer.Length>0&&ownNames.Contains(x.Retainer)?1UL:0UL,x.Price,x.Hq,x.Quantity,false,x.Materia)).ToArray(),data.History!=null,data.History??[]);
        var d=SellerPricing.Decide(item.ItemId,world,item.Hq,item.Price,comparison,new HashSet<ulong>{1},settings,now,item.Materia,levelOneOutfit);
        if (!SellerMateria.Known(item.Materia)) d=d with {Status=SellerStatus.Unknown};
        return d with {Source="Universalis",Reason=checkedText+$"{d.Reason} Live verification required before changing a price."};
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
            string? materia=null;
            if(row.TryGetProperty("materia",out var melds)&&melds.ValueKind==JsonValueKind.Array){
                // Dalamud uploads materia type in materiaID and zero-based
                // grade in slotID. Despite its name, slotID is not socket order.
                var parts=new List<(int,int)>();
                foreach(var m in melds.EnumerateArray()){
                    if(parts.Count>=5||!m.TryGetProperty("materiaID",out var type)||!type.TryGetInt32(out var t)||!m.TryGetProperty("slotID",out var grade)||!grade.TryGetInt32(out var g))throw new JsonException("Invalid materia details.");
                    parts.Add((t,g));
                }
                materia=SellerMateria.FromParts(parts);
                if(materia==null)throw new JsonException("Invalid materia types or grades.");
            }
            offers.Add(new(row.TryGetProperty("retainerName",out var name)?name.GetString()??"":"",price,row.GetProperty("hq").GetBoolean(),quantity,row.TryGetProperty("onMannequin",out var mannequin)&&mannequin.ValueKind!=JsonValueKind.Null&&mannequin.GetBoolean(),materia));
            if(offers.Count>100)throw new JsonException("Unexpected number of listings.");
        }
        SellerSale[]? history=null;
        if(root.TryGetProperty("recentHistory",out var sales)&&sales.ValueKind==JsonValueKind.Array){
            var entries=new List<SellerSale>();
            foreach(var sale in sales.EnumerateArray()){
                var price=sale.GetProperty("pricePerUnit").GetInt64();var quantity=sale.GetProperty("quantity").GetUInt32();
                if(price is <=0 or >SellerPricing.MaxPrice||quantity==0||entries.Count>=20)throw new JsonException("Invalid recent sale.");
                if(sale.TryGetProperty("worldID",out var sw)&&sw.GetUInt32()!=world)throw new JsonException("Sale is from another world.");
                entries.Add(new(price,sale.GetProperty("hq").GetBoolean(),quantity,DateTimeOffset.FromUnixTimeSeconds(sale.GetProperty("timestamp").GetInt64()),sale.TryGetProperty("onMannequin",out var mannequin)&&mannequin.ValueKind!=JsonValueKind.Null&&mannequin.GetBoolean()));
            }
            history=entries.ToArray();
        }
        return new(world,item,DateTimeOffset.FromUnixTimeMilliseconds(ms),checkedAt,offers.ToArray(),History:history);
    }
}

public sealed class MarketWatchClient:IDisposable
{
    private readonly HttpClient http;
    public MarketWatchClient(HttpMessageHandler? handler=null)
    {
        http=handler==null?new():new(handler);http.Timeout=TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("EquinoxCompanion-Market/0.5.1.258");
        http.MaxResponseContentBufferSize=8*1024*1024;
    }
    public async Task<MarketWatchBatch> Read(uint world,uint[] items,CancellationToken token)
    {
        if(world==0||items.Length is 0 or >50||items.Any(x=>x==0))throw new ArgumentException("Invalid world/items.");
        try{
            using var response=await http.GetAsync($"https://universalis.app/api/v2/{world}/{string.Join(",",items)}?listings=100&entries=20",token).ConfigureAwait(false);
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
