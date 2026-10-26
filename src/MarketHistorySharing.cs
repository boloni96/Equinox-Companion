using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed record MarketHistoryRecord(string Id,string SourceId,string Kind,string CharacterId,string Character,uint WorldId,string World,
    string RetainerId,string Retainer,uint ItemId,string Item,bool Hq,uint Quantity,long? Gross,long? Net,int? Price,long? Tax,int? Before,int? After,string Result,string Evidence,DateTimeOffset At);
public sealed record SharedMarketHistoryState(string Scope,long Cursor,MarketHistoryRecord[] Records,Dictionary<string,string> Sent)
{
    public static SharedMarketHistoryState Empty(string scope)=>new(scope,0,[],[]);
}
public sealed record SharedMarketHistoryResult(SharedMarketHistoryState State,string Status,bool More,bool Changed);
public static class MarketHistorySharing
{
    public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true};
    public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static string Fingerprint(MarketHistoryRecord r)=>Hash(JsonSerializer.Serialize(r,Json));
    public static MarketHistoryRecord[] Local(MarketLedger ledger)
    {
        var records=new List<MarketHistoryRecord>();
        foreach(var x in ledger.Income)records.Add(new(Hash("sale:"+x.CharacterId+":"+x.Id),x.Id,"sale",x.CharacterId.ToString(),x.Character,x.WorldId,x.World,x.RetainerId.ToString(),x.Retainer,x.ItemId,x.Item,x.Hq,x.Quantity,x.Gross,x.Net,null,null,null,null,"",x.Evidence,x.At));
        foreach(var x in ledger.Expenses??[])records.Add(new(Hash("purchase:"+x.CharacterId+":"+x.Id),x.Id,"purchase",x.CharacterId.ToString(),x.Character,x.WorldId,x.World,"0","",x.ItemId,"",x.Hq,x.Quantity,null,null,(int)x.Price,x.Tax,null,null,"","Game market purchase",x.At));
        var retainers=ledger.Retainers.DistinctBy(r=>r.Id).ToDictionary(r=>r.Id);
        foreach(var x in ledger.Prices){
            if(!retainers.TryGetValue(x.RetainerId,out var r))continue;
            var source=Hash(JsonSerializer.Serialize(x,Json));
            records.Add(new(Hash("price:"+r.CharacterId+":"+source),source,"price",r.CharacterId.ToString(),r.Character,r.WorldId,r.World,r.Id.ToString(),r.Name,x.ItemId,x.Item,x.Hq,0,null,null,null,null,x.Before,x.After,x.Result,x.Reason,x.At));
        }
        return records.Where(Valid).DistinctBy(r=>r.Id).ToArray();
    }
    public static bool Valid(MarketHistoryRecord r)=>r!=null&&r.Id is {Length:64}&&r.Id.All(Uri.IsHexDigit)&&r.SourceId is {Length:>0 and <=500}&&
        r.Kind is "sale" or "purchase" or "price"&&ulong.TryParse(r.CharacterId,out var cid)&&cid>0&&ulong.TryParse(r.RetainerId,out _)&&
        r.Character is {Length:>0 and <=100}&&r.WorldId is >0 and <=65535&&r.World is {Length:>0 and <=80}&&r.Retainer is {Length:<=100}&&
        r.ItemId is >0 and <1000000&&r.Item is {Length:<=100}&&r.Quantity<=9999&&r.Result is {Length:<=80}&&r.Evidence is {Length:<=1500}&&
        r.At.Year>=2013&&r.At<=DateTimeOffset.UtcNow.AddMinutes(5)&&new[]{r.Gross,r.Net,r.Tax}.All(v=>v is null or >=0 and <=9999999999999)&&
        new[]{r.Price,r.Before,r.After}.All(v=>v is null or >=0 and <=999999999)&&
        (r.Kind!="sale"||r.Gross!=null||r.Net!=null)&&(r.Kind!="purchase"||r.Price>0&&r.Quantity>0)&&(r.Kind!="price"||r.Before>0);
    public static MarketLedger View(MarketLedger local,MarketHistoryRecord[] remote)
    {
        var records=remote.Concat(Local(local)).GroupBy(r=>r.Id).Select(g=>g.OrderByDescending(r=>r.Gross.HasValue).First()).Where(Valid).ToArray();
        var income=records.Where(r=>r.Kind=="sale").Select(r=>new MarketIncome(r.SourceId,ulong.Parse(r.CharacterId),r.Character,r.WorldId,r.World,ulong.Parse(r.RetainerId),r.Retainer,r.ItemId,r.Item,r.Hq,r.Quantity,r.Gross,r.Net,r.At,r.Evidence)).ToList();
        // A sale-history row can replace one matching chat receipt/estimate. The
        // original import evidence stays intact and identical legitimate sales stay distinct.
        var removed=new HashSet<(ulong Character,string Id)>();
        foreach(var h in income.Where(x=>x.Id.StartsWith("history:")).ToArray()){
            bool Matches(MarketIncome x){
                if(removed.Contains((x.CharacterId,x.Id))||x.Id.StartsWith("history:")||x.CharacterId!=h.CharacterId||(x.RetainerId!=0&&x.RetainerId!=h.RetainerId)||x.ItemId!=h.ItemId||x.Hq!=h.Hq)return false;
                if(x.Evidence.StartsWith("Game sale message")&&x.Net==h.Net&&(x.Quantity==0||x.Quantity==h.Quantity)&&Math.Abs((x.At-h.At).TotalSeconds)<=90)return true;
                var start=x.Evidence.IndexOf("since ",StringComparison.Ordinal);var end=x.Evidence.IndexOf(';',Math.Max(0,start));
                return x.Id.StartsWith("inferred:")&&x.Quantity==h.Quantity&&start>=0&&end>start&&DateTimeOffset.TryParse(x.Evidence[(start+6)..end],out var since)&&h.At>=since&&h.At<=x.At;
            }
            var old=income.FirstOrDefault(Matches);
            if(old!=null){removed.Add((old.CharacterId,old.Id));if(h.Gross==null&&old.Gross!=null)income[income.IndexOf(h)]=h with {Gross=old.Gross};}
        }
        var expenses=records.Where(r=>r.Kind=="purchase").Select(r=>new MarketExpense(r.SourceId,ulong.Parse(r.CharacterId),r.Character,r.WorldId,r.World,r.ItemId,r.Hq,r.Quantity,r.Price!.Value,r.Tax??0,r.At)).OrderBy(r=>r.At).ToArray();
        var prices=records.Where(r=>r.Kind=="price").Select(r=>new MarketPriceLog(r.At,ulong.Parse(r.RetainerId),r.Retainer,r.ItemId,r.Item,r.Hq,r.Before!.Value,r.After,r.Result,r.Evidence)).OrderBy(r=>r.At).ToArray();
        return local with {Income=income.Where(x=>!removed.Contains((x.CharacterId,x.Id))).OrderBy(x=>x.At).ToArray(),Expenses=expenses,Prices=prices};
    }
}

public sealed class MarketHistoryClient:IDisposable
{
    private readonly HttpClient client;
    public MarketHistoryClient(HttpMessageHandler? handler=null){client=handler==null?new():new(handler);client.Timeout=TimeSpan.FromSeconds(20);client.MaxResponseContentBufferSize=1_000_000;}
    private async Task<JsonDocument> Request(string key,string path,object? body,CancellationToken cancel)
    {
        using var req=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/market-history"+path);
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        if(body!=null)req.Content=new StringContent(JsonSerializer.Serialize(body,MarketHistorySharing.Json),Encoding.UTF8,"application/json");
        using var response=await client.SendAsync(req,cancel).ConfigureAwait(false);response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false));
    }
    public async Task<SharedMarketHistoryResult> Sync(string key,SharedMarketHistoryState state,MarketLedger local,string[] characters,CancellationToken cancel)
    {
        var current=state;var changed=false;
        try{
            var allowed=characters.ToHashSet();var rows=MarketHistorySharing.Local(local).Where(r=>allowed.Contains(r.CharacterId)).ToArray();
            var pending=rows.Where(r=>state.Sent.GetValueOrDefault(r.Id)!=MarketHistorySharing.Fingerprint(r)).OrderByDescending(r=>r.At).Take(25).ToArray();
            if(pending.Length>0){
                using var posted=await Request(key,"",new {records=pending},cancel).ConfigureAwait(false);
                var accepted=posted.RootElement.GetProperty("accepted").Deserialize<string[]>()??[];
                if(accepted.Any(id=>!pending.Any(r=>r.Id==id)))throw new JsonException("Unexpected market receipt");
                var sent=new Dictionary<string,string>(state.Sent);
                foreach(var r in pending.Where(r=>accepted.Contains(r.Id)))sent[r.Id]=MarketHistorySharing.Fingerprint(r);
                current=current with {Sent=sent};changed=accepted.Length>0;
            }
            using var received=await Request(key,"?after="+state.Cursor+"&limit=100",null,cancel).ConfigureAwait(false);
            var page=received.RootElement;var cursor=page.GetProperty("cursor").GetInt64();
            var downloaded=page.GetProperty("records").Deserialize<MarketHistoryRecord[]>(MarketHistorySharing.Json)??[];
            if(cursor<state.Cursor||downloaded.Length>100||downloaded.Any(r=>!MarketHistorySharing.Valid(r)||!allowed.Contains(r.CharacterId)))throw new JsonException("Invalid market history page");
            if(downloaded.Length>0||cursor!=state.Cursor){
                var merged=current.Records.ToDictionary(r=>r.Id);foreach(var r in downloaded)merged[r.Id]=r;
                current=current with {Cursor=cursor,Records=merged.Values.ToArray()};changed=true;
            }
            var more=page.GetProperty("more").GetBoolean()||rows.Any(r=>current.Sent.GetValueOrDefault(r.Id)!=MarketHistorySharing.Fingerprint(r));
            return new(current,more?"Syncing paired Market history…":"Paired Market history synced.",more,changed);
        }catch(Exception e)when(e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException){return new(current,"Market history sync unavailable; saved records will retry.",false,changed);}
    }
    public void Dispose()=>client.Dispose();
}
