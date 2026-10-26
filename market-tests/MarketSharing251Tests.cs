using EquinoxCompanion;
using System.Net;
using System.Text.Json;
static class MarketSharing251Tests
{
 public static async Task Run(Action<string,bool> check,DateTimeOffset now){
  var r=new MarketRetainer(123,"Retainer",999,"Character",57,"Siren","Person","MAIN",0,now,[]);
  var inferred=new MarketIncome("inferred:1",999,"Character",57,"Siren",123,"Retainer",10,"Item",false,2,200,null,now.AddSeconds(20),$"Inferred since {now.AddMinutes(-5):O}; sale time/net not confirmed");
  var history=inferred with {Id="history:1",Gross=null,Net=190,At=now,Evidence="Game retainer sale history; net and sale time confirmed"};
  var ledger=new MarketLedger([r],[history],[],[]);
  var remote=MarketHistorySharing.Local(ledger with {Income=[inferred]});
  var view=MarketHistorySharing.View(ledger,remote);
  check("shared history replaces offline estimate once",view.Income is [{Net:190,Gross:200}]);
  var chat=history with {Id="receipt",RetainerId=0,Quantity=0,At=now.AddSeconds(3),Evidence="Game sale message; net confirmed"};
  check("shared history reconciles live receipt once",MarketHistorySharing.View(ledger,MarketHistorySharing.Local(ledger with {Income=[chat]})).Income.Length==1);
  check("same receipt ID from different characters is retained",MarketHistorySharing.View(ledger,MarketHistorySharing.Local(ledger with {Income=[chat,chat with {CharacterId=998}]})).Income.Length==2);
  check("identical legitimate sales remain distinct",MarketHistorySharing.View(ledger with {Income=[history,history with {Id="history:2"}]},remote).Income.Length==2);
  var record=MarketHistorySharing.Local(ledger).Single();
  check("wide IDs round trip as strings",JsonSerializer.Deserialize<MarketHistoryRecord>(JsonSerializer.Serialize(record with {CharacterId="18446744073709551615"},MarketHistorySharing.Json),MarketHistorySharing.Json)?.CharacterId=="18446744073709551615");
  check("invalid history values rejected",!MarketHistorySharing.Valid(record with {Net=-1})&&!MarketHistorySharing.Valid(record with {CharacterId="0"}));
  var handler=new Handler(record);using var client=new MarketHistoryClient(handler);var state=SharedMarketHistoryState.Empty("scope");
  var first=await client.Sync(new string('a',64),state,ledger,["999"],default);
  check("uploaded receipt survives failed history download",first.Changed&&first.State.Sent.Count==1&&first.State.Cursor==0);
  var second=await client.Sync(new string('a',64),first.State,ledger,["999"],default);
  check("retry does not upload acknowledged rows again",handler.Posts==1&&second.State.Cursor==7&&second.State.Records.Length==1);
  var third=await client.Sync(new string('a',64),second.State,ledger,["999"],default);
  check("repeated history page is idempotent",third.State.Records.Length==1&&handler.Posts==1);
  check("download never enters local ledger",ledger.Income.Length==1&&ledger.Retainers.Length==1);
 }
 sealed class Handler(MarketHistoryRecord row):HttpMessageHandler{
  public int Posts;int gets;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token){
   if(req.Headers.Authorization?.Parameter!=new string('a',64))throw new Exception("Missing pairing auth");
   object body;
   if(req.Method==HttpMethod.Post){Posts++;body=new {accepted=new[]{row.Id}};}
   else{if(++gets==1)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));body=new {records=new[]{row},cursor=7,more=false};}
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(body,MarketHistorySharing.Json))});
  }
 }
}
