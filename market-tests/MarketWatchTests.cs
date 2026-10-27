using EquinoxCompanion;
using System.Net;
using System.Text.Json;

static class MarketWatchTests
{
    internal static async Task Run(Action<string,bool> check,DateTimeOffset now)
    {
        var stock=new MarketStock(0,10,"Test",false,99,300000,now,Materia:"");
        MarketWatchSnapshot data=new(20,10,now.AddMinutes(-10),now,[new("Other",100000,false,1,false),new("Another",250000,false,2,false)]);
        SellerDecision Assess(MarketWatchSnapshot? d,ISet<string>? names=null)=>MarketWatchPolicy.Assess(stock,20,d,names??new HashSet<string>(),new(),now);
        check("background uses same gap rule",Assess(data).Price==249999);
        check("background result is labelled cached",Assess(data).Source=="Universalis"&&Assess(data).Reason.Contains("Live verification required"));
        check("old background data not green or red",Assess(data with {Uploaded=now.AddHours(-2)}).Status==SellerStatus.Unknown);
        check("future background timestamp not trusted",Assess(data with {Uploaded=now.AddHours(1)}).Status==SellerStatus.Unknown);
        check("wrong background world rejected",Assess(data with {World=21}).Price==null);
        check("wrong background item rejected",Assess(data with {Item=11}).Price==null);
        check("background HQ never mixed",Assess(data with {Offers=[new("Other",10,true,1,false)]}).Price==null);
        check("own names on requested world matched",Assess(data,new HashSet<string>{"Other"}).Price==100000);
        check("own-only cache suggests same own price",Assess(data,new HashSet<string>{"Other","Another"}) is {Status:SellerStatus.Update,Price:100000});
        check("empty cache never uses guessed history",Assess(data with {Offers=[]}).Price==null);
        check("failed cache never green",Assess(data with {Error="Busy"}).Status==SellerStatus.Unknown);
        check("unknown cache never green",Assess(null).Status==SellerStatus.Unknown);
        string Json(uint world,uint item)=>JsonSerializer.Serialize(new {worldID=world,itemID=item,lastUploadTime=now.ToUnixTimeMilliseconds(),hasData=true,listings=new[]{new {pricePerUnit=10000,quantity=99,hq=false,retainerName="Other",onMannequin=false}}});
        using var parsed=JsonDocument.Parse(Json(20,10));
        check("parser uses upload time not fetch time",MarketWatchPolicy.Parse(parsed.RootElement,20,10,now.AddHours(1)).Uploaded==now);
        try{MarketWatchPolicy.Parse(parsed.RootElement,21,10,now);check("parser wrong world rejected",false);}catch(JsonException){check("parser wrong world rejected",true);}
        using var nullable=JsonDocument.Parse(Json(20,10).Replace("\"onMannequin\":false","\"onMannequin\":null"));
        check("optional null mannequin flag remains usable",MarketWatchPolicy.Parse(nullable.RootElement,20,10,now).Offers.Length==1);
        var handler=new WatchHandler();using var client=new MarketWatchClient(handler);
        handler.Body=Json(20,10);
        var one=await client.Read(20,[10],CancellationToken.None);
        check("background request scoped to exact world",handler.Url?.Contains("/api/v2/20/10?")==true&&one.Items.Length==1&&one.Items[0].Error=="");
        handler.Body="{\"items\":{\"10\":"+Json(20,10)+",\"11\":"+Json(20,11)+"}}";
        var batch=await client.Read(20,[10,11],CancellationToken.None);
        check("world requests batch item ids",handler.Url?.Contains("20/10,11?")==true&&batch.Items.Length==2&&batch.Items.All(x=>x.Error==""));
        handler.Body="{\"items\":{\"10\":"+Json(20,10)+"}}";
        var partial=await client.Read(20,[10,11],CancellationToken.None);
        check("missing batch item remains uncertain",partial.Items[0].Error==""&&partial.Items[1].Error.Length>0);
        handler.Status=HttpStatusCode.TooManyRequests;
        var rate=await client.Read(20,[10],CancellationToken.None);
        check("background rate limit backs off",rate.Items.Length==0&&rate.RetryAfter>=TimeSpan.FromSeconds(30)&&rate.Error.Length>0);
        handler.Status=HttpStatusCode.ServiceUnavailable;
        var outage=await client.Read(20,[10],CancellationToken.None);
        check("outage is not an empty market",outage.Items.Length==0&&outage.Error.Length>0);
        handler.Status=HttpStatusCode.OK;handler.Body="{}";
        var invalid=await client.Read(20,[10],CancellationToken.None);
        check("malformed response is not green",invalid.Items.Length==1&&invalid.Items[0].Error.Length>0);
    }
    private sealed class WatchHandler:HttpMessageHandler
    {
        internal string Body="",Url="";internal HttpStatusCode Status=HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Url=request.RequestUri!.ToString();return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(Body)});}
    }
}
