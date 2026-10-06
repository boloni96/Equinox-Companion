using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed record FollowPortalSignal(string Id,string Name,uint HomeWorld,uint CurrentWorld,string EntityId,uint Territory,uint MapId,uint BaseId,int HandlerType,float X,float Y,float Z,long SentAt,string Confirmation,long ExpiresAt=0);
public sealed record FollowPortalEnvelope(FollowPortalSignal? Signal);
public sealed class FollowPortalRelay : IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler {AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(4)};
    private readonly CancellationTokenSource cancel = new();
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private const string Endpoint="https://equinoxjournal.pages.dev/api/companion/portal";
    public async Task<(FollowPortalSignal? Signal,string Status)> Read(string key,string name,uint world)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint+"?name="+Uri.EscapeDataString(name)+"&world="+world);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            if(!response.IsSuccessStatusCode)return(null,Failure((int)response.StatusCode));
            // Only tiny relay responses are accepted; never buffer a website/error page as data.
            using var stream=await response.Content.ReadAsStreamAsync(cancel.Token);
            var bytes=new byte[4097];var count=0;
            while(count<bytes.Length){var n=await stream.ReadAsync(bytes.AsMemory(count),cancel.Token);if(n==0)break;count+=n;}
            if(count>4096)return(null,"Portal relay response too large; ignored.");
            return(JsonSerializer.Deserialize<FollowPortalEnvelope>(bytes.AsSpan(0,count),json)?.Signal,"Portal relay ready.");
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException){return(null,"Portal relay unavailable; no portal action taken.");}
    }
    public async Task<string> Send(string key,FollowPortalSignal signal)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint){Content=JsonContent.Create(signal,options:json)};
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            using var response=await client.SendAsync(request,cancel.Token);
            return response.IsSuccessStatusCode?"Portal transition shared for 15 seconds.":Failure((int)response.StatusCode);
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException){return "Portal relay unavailable; this transition was not queued for later replay.";}
    }
    private static string Failure(int code)=>code switch{404=>"Deploy Journal V7.11.74 to enable portal relay.",403=>"Portal relay refused; check pairing and deploy Journal V7.11.74.",401=>"Portal relay needs a valid pairing key and Journal V7.11.74.",_=>"Portal relay unavailable (HTTP "+code+"). No portal action taken."};
    public void Dispose(){cancel.Cancel();client.Dispose();cancel.Dispose();}
}
