using System.Net;
using System.Text;
using System.Text.Json;
namespace EquinoxCompanion;

internal sealed class ProfileReadException(string message):Exception(message);
public sealed partial class CompanionSync
{
    // Bound both memory and body-read time. ResponseHeadersRead's HttpClient
    // timeout alone does not cover reading the response body.
    private static async Task<string> ReadProfileBody(HttpResponseMessage response,int limit,CancellationToken token)
    {
        if(response.Content.Headers.ContentLength is {} length&&length>limit)
            throw new ProfileReadException($"response exceeds {limit:N0} bytes");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
        using var body=new MemoryStream();var buffer=new byte[16384];
        while(true){
            var count=await stream.ReadAsync(buffer.AsMemory(),deadline.Token);
            if(count==0)break;
            if(body.Length+count>limit)throw new ProfileReadException($"response exceeds {limit:N0} bytes");
            body.Write(buffer,0,count);
        }
        var bytes=body.ToArray();var offset=bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf?3:0;
        return Encoding.UTF8.GetString(bytes,offset,bytes.Length-offset);
    }
    internal static string ProfileFailure(string stage,Exception error)=>stage+": "+(error switch{
        ProfileReadException known=>known.Message,
        OperationCanceledException=>"request cancelled or timed out",
        JsonException=>"invalid JSON or inventory-page structure",
        HttpRequestException {StatusCode:{} status}=>$"HTTP {(int)status}",
        HttpRequestException http=>$"connection failure ({http.HttpRequestError}; {http.InnerException?.GetType().Name??http.GetType().Name})",
        IOException io=>$"response read failed ({io.GetType().Name})",
        _=>"invalid response"
    });
}
