using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed partial class CompanionSync
{
    public string InstallationId {get;set;}="";
    private void AddInstallationHeader(HttpRequestMessage request){if(DeliveryPolicy.InstallationValid(InstallationId))request.Headers.TryAddWithoutValidation("X-Equinox-Install",InstallationId);}
    private static RosterResult WithRosterReceipt(RosterResult result,HttpResponseMessage response)
    {
        string Header(string name)=>response.Headers.TryGetValues(name,out var values)?values.FirstOrDefault()??"":"";
        if(Header("X-Equinox-Delivery-Status")=="unavailable")return result with {DeliveryError="Receipt tracking unavailable for this check; shared data still updated."};
        var receipt=Header("X-Equinox-Receipt");
        if(!DeliveryPolicy.ReceiptValid(receipt)||!long.TryParse(Header("X-Equinox-Roster-Revision"),out var revision)||revision<0||!long.TryParse(Header("X-Equinox-Applied-Cursor"),out var cursor)||cursor<0)return result;
        return result with {Receipt=receipt,ReceiptRevision=revision,AppliedCursor=cursor};
    }
    public async Task<DeliveryResult> ReadDelivery(string key)
    {
        try{
            using var request=new HttpRequestMessage(HttpMethod.Get,"https://equinoxjournal.pages.dev/api/companion/delivery");
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);AddInstallationHeader(request);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            if(!response.IsSuccessStatusCode)return new(null,$"Delivery details unavailable (HTTP {(int)response.StatusCode}).");
            await response.Content.LoadIntoBufferAsync(65536);
            var status=JsonSerializer.Deserialize<DeliveryStatus>(await response.Content.ReadAsStringAsync(cancel.Token),Json);
            return status is not null&&DeliveryPolicy.Valid(status)?new(status):new(null,"Delivery details were invalid.");
        }catch(Exception e)when(e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException){return new(null,"Delivery details unavailable; last confirmation retained.");}
    }
    public async Task<DeliveryAckResult> AcknowledgeRoster(string key,string receipt)
    {
        if(!DeliveryPolicy.ReceiptValid(receipt)||!DeliveryPolicy.InstallationValid(InstallationId))return new(false,"Invalid roster receipt.");
        try{
            using var request=new HttpRequestMessage(HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/delivery");
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);AddInstallationHeader(request);
            request.Content=JsonContent.Create(new {installationId=InstallationId,receipt},options:Json);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            return response.IsSuccessStatusCode?new(true):new(false,$"Receipt confirmation pending (HTTP {(int)response.StatusCode}).");
        }catch(Exception e)when(e is HttpRequestException or TaskCanceledException){return new(false,"Receipt confirmation pending; connection unavailable.");}
    }
    public async Task<BidHistoryResult> ReadBidHistory(string key,string characterId,string? cursor)
    {
        try{
            var url="https://equinoxjournal.pages.dev/api/companion/bid-history?characterId="+Uri.EscapeDataString(characterId)+"&limit=40"+(string.IsNullOrEmpty(cursor)?"":"&cursor="+Uri.EscapeDataString(cursor));
            using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);AddInstallationHeader(request);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
            if(response.StatusCode==HttpStatusCode.Gone)return new(null,"This history snapshot expired. Refresh history to continue.",true);
            if(!response.IsSuccessStatusCode)return new(null,$"History unavailable (HTTP {(int)response.StatusCode}); loaded records retained.");
            await response.Content.LoadIntoBufferAsync(262144);
            var page=JsonSerializer.Deserialize<BidHistoryPage>(await response.Content.ReadAsStringAsync(cancel.Token),Json);
            if(page is null||page.CharacterId!=characterId||page.Revision<0||page.Total<0||page.Bids is null||page.Total<page.Bids.Length||page.Bids.Length>40||page.NextCursor?.Length>4096||page.Bids.Any(b=>b is null||b.WorldId is <1 or >65535||b.At.Year<2020||!HousingLottery.Valid(new(b.District,b.Ward,b.Plot,b.Entry,b.Buyer,b.FcId,b.Status=="claimed"?"entered":b.Status,b.CycleStart,b.Evidence))))return new(null,"History response invalid; loaded records retained.");
            return new(page);
        }catch(Exception e)when(e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException){return new(null,"History unavailable; loaded records retained. Try again.");}
    }
}
