using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed record FollowPortalSignal(string Id,string Name,uint HomeWorld,uint CurrentWorld,string EntityId,uint Territory,uint MapId,uint BaseId,int HandlerType,float X,float Y,float Z,long SentAt,string Confirmation,long ExpiresAt=0,string TravelKind="portal",uint AetheryteId=0,string Destination="",int Ward=0,uint DestinationTerritory=0,float SourceRadius=0,FollowMenuStep[]? Steps=null,string SourceKind="",uint DestinationWorld=0,string FriendContentId="",string EstateId="",FollowTravelPosition? Approach=null,uint DutyId=0,FollowTravelPosition? Arrival=null,uint ArrivalMap=0,uint ArrivalTerritory=0,uint ArrivalWorld=0,long Sequence=0,uint ArrivalInstance=0);
public sealed record FollowMenuStep(string Text,bool Confirmation=false,string Addon="",int[]? Arguments=null,string MenuSignature="");
public sealed record FollowPortalEnvelope(FollowPortalSignal? Signal,FollowPortalSignal[]? Signals=null);
public sealed class FollowPortalRelay : IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler {AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(4)};
    private readonly CancellationTokenSource cancel = new();
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private const string Endpoint="https://equinoxjournal.pages.dev/api/companion/portal";
    public async Task<(FollowPortalSignal[] Signals,string Status)> Read(string key,string name,uint world,string session,long after=0)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint+"?name="+Uri.EscapeDataString(name)+"&world="+world+"&queue=1&after="+after+"&session="+session);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            if(!response.IsSuccessStatusCode)return([],Failure((int)response.StatusCode));
            // Only tiny relay responses are accepted; never buffer a website/error page as data.
            using var stream=await response.Content.ReadAsStreamAsync(cancel.Token);
            var bytes=new byte[131073];var count=0;
            while(count<bytes.Length){var n=await stream.ReadAsync(bytes.AsMemory(count),cancel.Token);if(n==0)break;count+=n;}
            if(count>131072)return([],"Portal relay response too large; ignored.");
            var envelope=JsonSerializer.Deserialize<FollowPortalEnvelope>(bytes.AsSpan(0,count),json);
            return(envelope?.Signals??(envelope?.Signal is {} one?[one]:[]),"Portal relay ready.");
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException){return([],"Portal relay unavailable; no portal action taken.");}
    }
    public async Task<bool> Session(string key,string id,string name,uint world,bool active)
    {
        try
        {
            using var request=new HttpRequestMessage(active?HttpMethod.Post:HttpMethod.Delete,Endpoint+"?mode=session&session="+id);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            if(active)request.Content=JsonContent.Create(new {name,world});
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            return response.IsSuccessStatusCode;
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException){return false;}
    }
    public async Task<bool> HasFollowers(string key,string name,uint world)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint+"?mode=followers&name="+Uri.EscapeDataString(name)+"&world="+world);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            if(!response.IsSuccessStatusCode)return false;
            using var stream=await response.Content.ReadAsStreamAsync(cancel.Token);
            var bytes=new byte[513];var count=0;
            while(count<bytes.Length){var n=await stream.ReadAsync(bytes.AsMemory(count),cancel.Token);if(n==0)break;count+=n;}
            return count<=512&&JsonSerializer.Deserialize<SessionReply>(bytes.AsSpan(0,count),json)?.Active==true;
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException){return false;}
    }
    private sealed record SessionReply(bool Active);
    public async Task<string> Send(string key,FollowPortalSignal signal)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint){Content=JsonContent.Create(signal,options:json)};
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            using var response=await client.SendAsync(request,cancel.Token);
            return response.IsSuccessStatusCode?"Portal transition submitted for active followers (two-minute expiry).":Failure((int)response.StatusCode);
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException){return "Portal relay unavailable; this transition was not queued for later replay.";}
    }
    private static string Failure(int code)=>code switch{404=>"Deploy Journal V7.11.77 to enable portal relay.",403=>"Portal relay refused; check pairing and deploy Journal V7.11.77.",401=>"Portal relay needs a valid pairing key and Journal V7.11.77.",_=>"Portal relay unavailable (HTTP "+code+"). No portal action taken."};
    public void Dispose(){cancel.Cancel();client.Dispose();cancel.Dispose();}
}
