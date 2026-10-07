using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;
public sealed class HelperRelay:IDisposable
{
    private readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(4)};
    private readonly CancellationTokenSource cancel=new();
    private static readonly JsonSerializerOptions json=new(JsonSerializerDefaults.Web);
    public async Task<(HelperReply? Reply,string Error)> Call(string key,string query,object? body=null)
    {
        try{
            using var request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/portal?mode=helper&"+query);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            if(body!=null)request.Content=JsonContent.Create(body,options:json);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            if(!response.IsSuccessStatusCode)return(null,"Helper relay HTTP "+(int)response.StatusCode+". Deploy Journal V7.11.83 and check pairing.");
            using var stream=await response.Content.ReadAsStreamAsync(cancel.Token);var bytes=new byte[262145];var count=0;
            while(count<bytes.Length){var n=await stream.ReadAsync(bytes.AsMemory(count),cancel.Token);if(n==0)break;count+=n;}
            if(count==bytes.Length)return(null,"Helper reply too large; ignored.");
            var reply=JsonSerializer.Deserialize<HelperReply>(bytes.AsSpan(0,count),json);
            return reply?.Protocol==1?(reply,""):(null,"Helper needs Journal V7.11.83 on Cloudflare.");
        }catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException){return(null,"Helper relay unavailable; permissions remain local.");}
    }
    public void Dispose(){cancel.Cancel();client.Dispose();cancel.Dispose();}
}
