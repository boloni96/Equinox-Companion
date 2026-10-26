using EquinoxCompanion;
using System.Text.Json;

static class MateriaTests
{
    internal static void Run(Action<string,bool> check,DateTimeOffset now)
    {
        var five=SellerMateria.FromParts([(13,11),(13,11),(14,11),(14,10),(15,10)])!;
        var different=SellerMateria.FromParts([(13,11),(13,11),(14,11),(14,10),(15,9)])!;
        check("materia order does not alter finished setup",five==SellerMateria.FromParts([(15,10),(14,10),(14,11),(13,11),(13,11)]));
        check("type grade and duplicate count remain significant",five!=different&&five!=SellerMateria.FromParts([(13,11),(14,11),(14,10),(15,10)]));
        check("native inventory and network signatures match",five==SellerMateria.FromInventory([13,13,14,14,15],[11,11,11,10,10])&&five==SellerMateria.FromPacked([219,219,235,234,250],5));
        check("known plain differs from unknown",SellerMateria.FromInventory([0,0,0,0,0],[0,0,0,0,0])==""&&SellerMateria.FromPacked([],0)==""&&!SellerMateria.Matches(null,""));
        check("invalid materia never becomes plain",SellerMateria.FromPacked([0],1)==null&&SellerMateria.FromPacked([0xF123],1)==null&&SellerMateria.FromParts([(256,1)])==null&&SellerMateria.FromParts([(13,16)])==null&&SellerMateria.FromParts([(13,1)],2)==null);
        check("persisted signatures validated",SellerMateria.Known(five)&&!SellerMateria.Known("0219")&&!SellerMateria.Known("250,219")&&!SellerMateria.Known("1")&&!SellerMateria.Known("219,219,219,219,219,219"));
        SellerComparison comparison=new(10,20,now,true,[new(9,298880,true,1,Materia:""),new(8,799999,false,1,Materia:""),new(7,1197021,true,1,Materia:five),new(6,1197021,true,1,Materia:five)],true,[new(298789,true,1,now.AddDays(-4)),new(319983,true,1,now.AddDays(-32)),new(1197021,true,1,now.AddDays(-34))]);
        SellerDecision Price(string? materia,int current=291408,SellerComparison? c=null,SellerSettings? s=null)=>SellerPricing.Decide(10,20,true,current,c??comparison,new HashSet<ulong>{1},s??new(){RaiseCompetitivePrices=true,PercentUndercut=true,UndercutPercent=2.5},now,materia);
        var plain=Price("");
        check("screenshot plain 298880 is not rejected against five materia premiums",plain.Price==291408&&plain.Status==SellerStatus.Competitive&&!plain.Reason.Contains("Ignored unusually"));
        check("screenshot recent lower sale is shown without claiming materia evidence",plain.Reason.Contains("298,789")&&plain.Reason.Contains("does not report materia")&&plain.Reason.Contains("1/3"));
        check("melded item finds higher matching listings across all results",Price(five,1300000).Price==1167095);
        check("same count different grades not used automatically",Price(different).Price==null&&Price(different).Reason.Contains("same materia types and grades"));
        check("plain listings cannot price a melded item",Price(five,c:comparison with {Offers=[comparison.Offers[0]],History=[new(300000,true,1,now),new(300000,true,1,now),new(300000,true,1,now)]}).Price==null);
        check("own melded offer cannot override plain competitor",Price("",400000,comparison with {Offers=[comparison.Offers[0],new(1,1197021,true,1,Materia:five)]}).Price==291408);
        check("own same meld matches without undercut",Price(five,1300000,comparison with {Offers=[new(1,1197021,true,1,Materia:five),new(9,1500000,true,1,Materia:five)]}).Price==1197021);
        check("unknown stock never repriced",Price(null).Price==null);
        check("unknown listing never treated as plain",Price("",c:comparison with {Offers=[new(9,298880,true,1,Materia:null)]}).Price==null);
        var low=comparison with {Offers=[new(9,100000,true,1),new(8,250000,true,1)],History=[new(99000,true,1,now),new(100100,true,1,now.AddHours(-1)),new(101000,true,1,now.AddHours(-2))]};
        check("several recent same-quality sales support a plausible plain low",Price("",300000,low,new()).Price==99999);
        check("minimum sales is respected when supporting low price",Price("",300000,low with {History=low.History.Take(1).ToArray()},new()).Price==249999);
        check("old sales cannot support a low price",Price("",300000,low with {History=low.History.Select(x=>x with {At=now.AddDays(-8)}).ToArray()},new()).Price==249999);
        check("other quality cannot support a low price",Price("",300000,low with {History=low.History.Select(x=>x with {Hq=false}).ToArray()},new()).Price==249999);
        check("disabled history cannot override low rule",Price("",300000,low,new(){UseHistory=false}).Price==249999);
        check("history support never lowers a meld premium",Price(five,300000,low with {Offers=low.Offers.Select(x=>x with {Materia=five}).ToArray()},new()).Price==249999);
        check("large increase limit still applies within same meld group",Price(five,100000).Price==null&&Price(five,100000).Reason.Contains("increase limit"));
        check("history requested for plain ambiguous price group",SellerPricing.NeedsHistory(low.Offers,true,"",new HashSet<ulong>(),new()));
        check("history not used to resolve unrelated meld premiums",!SellerPricing.NeedsHistory(comparison.Offers,true,"",new HashSet<ulong>(),new())&&!SellerPricing.NeedsHistory(low.Offers,true,five,new HashSet<ulong>(),new()));
        var many=comparison with {Offers=Enumerable.Range(1,99).Select(i=>new SellerOffer((ulong)(100+i),200000+i,true,1)).Append(new(400,1197021,true,1,Materia:five)).ToArray()};
        check("matching materia can be the last of 100 returned listings",Price(five,1300000,many).Price==1167095);
        var capture=new SellerComparisonCapture();capture.Begin(10,20,true,now,five);capture.Response(10,1,0,now);capture.ReceiveOffers(1,[new(1,10,comparison.Offers[2])],now);
        check("capture validates materia as part of fresh listing identity",capture.Ready(10,20,1)&&capture.Contains(1,comparison.Offers[2])&&!capture.Contains(1,comparison.Offers[2] with {Materia=different}));
        capture.ReceiveOffers(1,[new(1,10,comparison.Offers[2] with {Materia=different})],now);
        check("contradictory materia packet cannot complete a search",!capture.Ready(10,20,1));
        var json=JsonSerializer.Serialize(new {worldID=20,itemID=10,lastUploadTime=now.ToUnixTimeMilliseconds(),listings=new object[]{new {retainerName="Plain",pricePerUnit=298880,quantity=1,hq=true,materia=Array.Empty<object>()},new {retainerName="Melds",pricePerUnit=1197021,quantity=1,hq=true,materia=new[]{new{materiaID=13,slotID=11},new{materiaID=13,slotID=11},new{materiaID=14,slotID=11},new{materiaID=14,slotID=10},new{materiaID=15,slotID=10}}}},recentHistory=new[]{new {pricePerUnit=298789,quantity=1,hq=true,timestamp=now.AddDays(-4).ToUnixTimeSeconds()}}});
        using var doc=JsonDocument.Parse(json);var data=MarketWatchPolicy.Parse(doc.RootElement,20,10,now);
        check("Universalis grade is not misread as socket index",data.Offers[1].Materia==five&&data.Offers[0].Materia=="");
        check("Universalis recent sales retained with original age",data.History?.Single().At==now.AddDays(-4));
        var item=new MarketStock(0,10,"Knife",true,1,400000,now,Materia:"");
        var advisory=MarketWatchPolicy.Assess(item,20,data,new HashSet<string>(),new(),now);
        check("background highlights use the same plain cohort",advisory.Price==298879&&advisory.LowestCompetitor==298880);
        check("background unknown legacy stock remains uncertain",MarketWatchPolicy.Assess(item with {Materia=null},20,data,new HashSet<string>(),new(),now).Status==SellerStatus.Unknown);
        var missingNode=System.Text.Json.Nodes.JsonNode.Parse(json)!;missingNode["listings"]![0]!.AsObject().Remove("materia");
        using var missing=JsonDocument.Parse(missingNode.ToJsonString());
        check("missing API metadata is not assumed plain",MarketWatchPolicy.Parse(missing.RootElement,20,10,now).Offers[0].Materia==null);
        using var malformed=JsonDocument.Parse(json.Replace("\"slotID\":11","\"slotID\":20"));
        try{MarketWatchPolicy.Parse(malformed.RootElement,20,10,now);check("bad API materia rejected",false);}catch(JsonException){check("bad API materia rejected",true);}
        check("cached pricing invalidated when materia changes",!item.SamePricingItem(item with {Materia=five})&&item.SamePricingItem(item with {Price=5}));
        var old=item with {Materia=null};var retainer=new MarketRetainer(1,"R",100,"C",20,"W","P","A",0,now,[old]);
        check("learning materia does not invent a sale or reset listing age",MarketAccounting.Removed([old],[item]).Length==0&&MarketAccounting.PreserveListingDates(retainer,retainer with {Stock=[item with {ListedAt=now.AddHours(1)}]}).Stock[0].ListedAt==now);
        check("legacy saved stock deserializes as unknown",JsonSerializer.Deserialize<MarketStock>("{\"Slot\":0,\"ItemId\":10}")?.Materia==null);
    }
}
