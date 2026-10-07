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
            if(!response.IsSuccessStatusCode){
                var operation=query.Split('&')[0];
                var hint=(int)response.StatusCode switch {401 or 403=>"Check pairing and session permissions.",404=>"Deploy Journal V7.11.85.",410=>"This session ended; the follower must press Start again.",429=>"Relay rate limited; wait before retrying.",_=>"Request rejected; export diagnostics."};
                return(null,"Helper "+operation+" HTTP "+(int)response.StatusCode+". "+hint);
            }
            using var stream=await response.Content.ReadAsStreamAsync(cancel.Token);var bytes=new byte[1048577];var count=0;
            while(count<bytes.Length){var n=await stream.ReadAsync(bytes.AsMemory(count),cancel.Token);if(n==0)break;count+=n;}
            if(count==bytes.Length)return(null,"Helper reply too large; ignored.");
            var reply=JsonSerializer.Deserialize<HelperReply>(bytes.AsSpan(0,count),json);
            return reply?.Protocol==1?(reply,""):(null,"Helper needs Journal V7.11.84 on Cloudflare.");
        }catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException){return(null,"Helper relay unavailable; permissions remain local.");}
    }
    public void Dispose(){cancel.Cancel();client.Dispose();cancel.Dispose();}
}
