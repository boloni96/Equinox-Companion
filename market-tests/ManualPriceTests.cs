using EquinoxCompanion;
using System.Text.Json;

static class ManualPriceTests
{
    internal static void Run(Action<string,bool> check,DateTimeOffset now)
    {
        var own=new HashSet<ulong>{1};
        var allow=new SellerSettings {ManualAllowOtherQuality=true};
        SellerOffer nq=new(9,10000,false,1),hq=new(9,10000,true,1);
        (int Price,bool MatchOwn)? Click(bool editorHq,SellerOffer[] rows,SellerSettings? settings=null,long reference=10000,int minimum=1)
            =>SellerPricing.ManualClickPrice(reference,editorHq,rows,own,settings??new(),minimum);
        check("manual HQ/NQ crossover defaults off",!new SellerSettings().ManualAllowOtherQuality&&Click(true,[nq])==null&&Click(false,[hq])==null);
        check("manual HQ item can use an NQ price when allowed",Click(true,[nq],allow)==(9999,false));
        check("manual NQ item can use an HQ price when allowed",Click(false,[hq],allow)==(9999,false));
        check("same-quality manual clicks keep working with either setting",Click(true,[hq])==(9999,false)&&Click(false,[nq],allow)==(9999,false));
        check("turning crossover off takes effect on the next click",Click(true,[nq],allow)!=null&&Click(true,[nq],withSetting(false))==null);
        check("manual cross-quality own price is matched exactly",Click(true,[nq with {RetainerId=1}],allow)==(10000,true));
        check("other-quality own offer cannot affect strict manual selection",Click(true,[nq with {RetainerId=1},hq])==(9999,false));
        check("sorted equal-price rows still preserve known own price",Click(true,[hq,nq with {RetainerId=1}],allow)==(10000,true));
        check("different displayed price cannot be substituted",Click(true,[nq with {Price=9000}],allow)==null);
        check("an unverified or empty selection cannot supply a manual price",Click(true,[],allow)==null);
        check("mannequin and zero-quantity rows cannot supply a manual price",Click(true,[nq with {Mannequin=true},nq with {Quantity=0}],allow)==null);
        check("invalid selected prices are rejected",Click(true,[hq],allow,0)==null&&Click(true,[hq],allow,-1)==null&&Click(true,[hq],allow,(long)SellerPricing.MaxPrice+1)==null);
        check("manual cross-quality click still respects item minimum",Click(true,[nq],allow,minimum:12000)==(12000,false));
        check("manual click keeps configured percentage and rounding",Click(true,[nq],new(){ManualAllowOtherQuality=true,PercentUndercut=true,UndercutPercent=2.5,RoundTo=100})==(9700,false));
        check("manual own price bypasses undercut and rounding",Click(true,[nq with {RetainerId=1,Price=10009}],new(){ManualAllowOtherQuality=true,PercentUndercut=true,UndercutPercent=2.5,RoundTo=100},10009)==(10009,true));
        var comparison=new SellerComparison(10,20,now,true,[nq],true,[]);
        check("manual override never enables HQ/NQ mixing in automatic pricing",SellerPricing.Decide(10,20,true,20000,comparison,own,allow,now,"").Price==null);
        check("manual override never enables HQ/NQ mixing in reverse automatic pricing",SellerPricing.Decide(10,20,false,20000,comparison with {Offers=[hq]},own,allow,now,"").Price==null);
        check("automatic materia matching stays independent of manual override",SellerPricing.Decide(10,20,true,20000,comparison with {Offers=[hq]},own,allow,now,"219").Price==null);
        check("manual selection may intentionally use another materia setup",Click(true,[hq with {Materia="219,219,234,235,250"}])==(9999,false));
        var high=hq with {Price=1000000};
        check("automatic increase cap stays active while explicit clicks work",SellerPricing.Decide(10,20,true,10000,comparison with {Offers=[high]},own,new(){ManualAllowOtherQuality=true,RaiseCompetitivePrices=true},now,"").Price==null&&Click(true,[high],allow,1000000)==(999999,false));
        var saved=JsonSerializer.Deserialize<SellerSettings>(JsonSerializer.Serialize(allow))!;
        check("manual allowance survives settings save and old settings stay strict",saved.ManualAllowOtherQuality&&!JsonSerializer.Deserialize<SellerSettings>("{}")!.ManualAllowOtherQuality);
    }
    private static SellerSettings withSetting(bool enabled)=>new(){ManualAllowOtherQuality=enabled};
}
