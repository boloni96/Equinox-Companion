using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed class TestingSync:IDisposable
{
 private readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
 private readonly CancellationTokenSource cancel=new();
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 public Task<TestingResponse> Read(string key,long after)=>Request(key,after,null);
 public Task<TestingResponse> Send(string key,TestReport report)=>Request(key,0,report);
 private async Task<TestingResponse> Request(string key,long after,TestReport? report)
 {
  try{
   using var request=new HttpRequestMessage(report is null?HttpMethod.Get:HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/tests"+(report is null?"?after="+after:""));
   request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
   if(report is not null)request.Content=JsonContent.Create(report,options:Json);
   using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
   await response.Content.LoadIntoBufferAsync(4000000);var text=await response.Content.ReadAsStringAsync(cancel.Token);
   if(!response.IsSuccessStatusCode){string message="";try{using var doc=JsonDocument.Parse(text);message=doc.RootElement.GetProperty("error").GetString()??"";}catch{}
    var code=(int)response.StatusCode;return new(Error:code==404?"Deploy Journal V7.11.220 for the shared testing checklist.":code==401?"Connect with a valid Journal pairing key.":message.Length>0?message:$"Testing unavailable (HTTP {code}); your report is kept locally.",Conflict:code is 400 or 403 or 409);
   }
   if(report is not null){using var doc=JsonDocument.Parse(text);var saved=doc.RootElement.GetProperty("report").Deserialize<TestReport>(Json);return TestingPolicy.Valid(saved)&&saved!.Seq>0?new(Report:saved):new(Error:"Invalid testing receipt; keeping the report for retry.");}
   var page=JsonSerializer.Deserialize<TestingPage>(text,Json);
   return page is not null&&page.Schema==1&&TestingPolicy.Valid(page.Catalog)&&page.Reports is {Length:<=500}&&page.Reports.All(TestingPolicy.Valid)&&page.Cursor>=after?new(Page:page):new(Error:"Invalid testing response; saved results are kept.");
  }catch(OperationCanceledException){return new(Error:"Testing connection timed out; your report is kept locally.");}catch(Exception){return new(Error:"Testing connection unavailable; retrying saved reports.");}
 }
 public void Dispose(){cancel.Cancel();client.Dispose();cancel.Dispose();}
}
