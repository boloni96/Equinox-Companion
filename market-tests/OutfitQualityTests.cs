using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class OutfitQualityTests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS outfit quality "+name);}
        var now=DateTimeOffset.UtcNow;var own=new HashSet<ulong>{1};
        var on=new SellerSettings{CombineLevelOneOutfitQuality=true};
        var compare=new SellerComparison(10,20,now,true,[new(9,10000,false,1)],true,[]);
        SellerDecision Price(bool eligible=true,bool hq=true,SellerSettings? settings=null,SellerComparison? result=null,string? materia="",int current=20000)=>SellerPricing.Decide(10,20,hq,current,result??compare,own,settings??on,now,materia,eligible);
        Check("eligible all-class level-one outfit",SellerPricing.IsLevelOneOutfit(1,1,1,true,false));
        Check("exclude higher equip level",!SellerPricing.IsLevelOneOutfit(2,1,1,true,false));
        Check("exclude higher item level",!SellerPricing.IsLevelOneOutfit(1,10,1,true,false));
        Check("exclude job gear",!SellerPricing.IsLevelOneOutfit(1,1,2,true,false));
        Check("exclude weapon or tool",!SellerPricing.IsLevelOneOutfit(1,1,1,true,true));
        Check("exclude non-equipment",!SellerPricing.IsLevelOneOutfit(1,1,1,false,false));
        Check("off by default",!new SellerSettings().CombineLevelOneOutfitQuality&&Price(settings:new()).Price==null);
        Check("HQ outfit compares NQ price",Price().Price==9999&&Price().Reason.Contains("exception"));
        Check("NQ outfit compares HQ price",Price(hq:false,result:compare with{Offers=[new(9,10000,true,1)]}).Price==9999);
        Check("other gear remains strict",Price(eligible:false).Price==null);
        Check("manual opt-in cannot enable automatic exception",Price(settings:new(){ManualAllowOtherQuality=true}).Price==null);
        Check("automatic exception does not enable manual override",SellerPricing.ManualClickPrice(10000,true,compare.Offers,own,on)==null);
        Check("materia remains separate",Price(materia:"1:0").Price==null);
        Check("unknown materia blocked",Price(materia:null).Price==null);
        Check("own retainer matched exactly",Price(result:compare with{Offers=[new(1,10000,false,1)]}).Price==10000);
        Check("minimum respected",Price(settings:new(){CombineLevelOneOutfitQuality=true,MinimumPrices=new(){[10]=15000}}).Price==15000);
        Check("increase cap still applies",Price(settings:new(){CombineLevelOneOutfitQuality=true,RaiseCompetitivePrices=true},result:compare with{Offers=[new(9,1000000,false,1)]}).Price==null);
        Check("manual exception never bypasses exclusions",Price(settings:new(){CombineLevelOneOutfitQuality=true,ExcludedItems=[10]}).Price==null);
        var history=compare with{Offers=[],History=[new(10000,false,1,now),new(10000,true,1,now.AddMinutes(-1)),new(10000,false,1,now.AddMinutes(-2))]};
        Check("empty-market history combines quality",Price(result:history).Price==10000);
        Check("empty-market history strict for other gear",Price(eligible:false,result:history).Price==null);
        Check("history not forced when other quality is usable",!SellerPricing.NeedsHistory(compare.Offers,true,"",own,on,true));
        var stock=new MarketStock(0,10,"Outfit",true,1,20000,now,Materia:"");
        var cached=new MarketWatchSnapshot(20,10,now,now,[new("Other",10000,false,1,false)]);
        Check("background uses same exception",MarketWatchPolicy.Assess(stock,20,cached,new HashSet<string>(),on,now,true).Price==9999);
        Check("background strict for other gear",!MarketWatchPolicy.Assess(stock,20,cached,new HashSet<string>(),on,now,false).CanApply);
        Check("setting persists",JsonSerializer.Deserialize<SellerSettings>(JsonSerializer.Serialize(on))!.CombineLevelOneOutfitQuality);
    }
}
