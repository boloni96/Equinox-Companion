using EquinoxCompanion;

static class OwnPriceTests
{
    internal static void Run(Action<string,bool> check,DateTimeOffset now)
    {
        var own=new HashSet<ulong>{1,2};
        SellerDecision Price(SellerOffer[] offers,SellerSettings? settings=null,int current=500,bool hq=false)=>
            SellerPricing.Decide(10,20,hq,current,new(10,20,now,true,offers,true,[new(999,false,1,now)]),own,settings??new(),now);
        check("own match bypasses fixed undercut and rounding",Price([new(2,123,false,99)],new(){UndercutGil=10,RoundTo=100}) is {Price:123,CanApply:true,Undercut:false});
        check("own match bypasses percent undercut",Price([new(2,123,false,1)],new(){PercentUndercut=true,UndercutPercent=25,RoundTo=50}).Price==123);
        check("cheapest own retainer wins across duplicate stacks",Price([new(1,500,false,99),new(2,123,false,1),new(2,123,false,3)]).Price==123);
        check("matching own price does not keep lowering",Price([new(2,123,false,1)],current:123) is {Status:SellerStatus.Competitive,CanApply:false,Price:123});
        check("tie with outside retainer matches own price",Price([new(9,123,false,1),new(2,123,false,1)]).Price==123);
        check("outside cheaper retains ordinary undercut",Price([new(2,200,false,1),new(9,150,false,1)]).Price==149);
        check("outside cheaper still obeys gap rule",Price([new(2,200,false,1),new(9,100,false,1),new(8,250,false,1)]).Price==249);
        check("own match does not bypass item minimum",Price([new(2,123,false,1)],new(){MinimumPrices=new(){[10]=150}}) is {Price:150,Reason:var reason}&&reason.Contains("minimum"));
        check("own match repairs below-minimum listing",Price([new(2,123,false,1)],new(){MinimumPrices=new(){[10]=150}},100).Price==150);
        check("own match keeps lower price when raises disabled",Price([new(2,123,false,1)],current:100) is {Price:100,CanApply:false});
        check("own match permits configured raise",Price([new(2,123,false,1)],new(){RaiseCompetitivePrices=true},100).Price==123);
        check("own HQ listing is not an NQ anchor",Price([new(2,100,true,1),new(9,300,false,1)]).Price==299);
        check("own mannequin is not an anchor",Price([new(2,100,false,1,true),new(9,300,false,1)]).Price==299);
        check("excluded item cannot match own listing",Price([new(2,123,false,1)],new(){ExcludedItems=[10]}).Price==null);
        check("unidentified zero retainer is not own",SellerPricing.Decide(10,20,false,500,new(10,20,now,true,[new(0,123,false,1)],false,[]),new HashSet<ulong>{0},new(),now).Price==122);
        check("manual own reference matches exactly",SellerPricing.OfferPrice(123,true,new(){RoundTo=100,UndercutGil=50})==123);
        check("manual other reference still adjusted",SellerPricing.OfferPrice(123,false,new(){RoundTo=10,UndercutGil=1})==120);
        var stock=new MarketStock(0,10,"Test",false,99,500,now);
        var cache=new MarketWatchSnapshot(20,10,now,now,[new("Own",123,false,99,false),new("Other",200,false,1,false)]);
        check("background matches own exact price despite rounding",MarketWatchPolicy.Assess(stock,20,cache,new HashSet<string>{"Own"},new(){RoundTo=100},now) is {Price:123,Source:"Universalis"});
        check("background external lower remains competitor",MarketWatchPolicy.Assess(stock,20,cache with {Offers=[new("Own",123,false,1,false),new("Other",100,false,1,false)]},new HashSet<string>{"Own"},new(),now).Price==99);
        check("background blank name cannot be classified own",MarketWatchPolicy.Assess(stock,20,cache with {Offers=[new("",123,false,1,false)]},new HashSet<string>{""},new(),now).Price==122);
        check("background own stale data cannot suggest a price",MarketWatchPolicy.Assess(stock,20,cache with {Uploaded=now.AddDays(-1)},new HashSet<string>{"Own"},new(),now).Price==null);

        SellerComparison Empty(SellerSale[] sales)=>new(10,20,now,true,[],true,sales);
        SellerDecision History(SellerComparison comparison,SellerSettings? settings=null)=>SellerPricing.Decide(10,20,false,500,comparison,own,settings??new(),now);
        var mixed=new SellerSale[]{new(100,true,1,now),new(110,true,1,now),new(120,true,1,now),new(900,false,1,now.AddDays(-10))};
        var missing=History(Empty(mixed));
        check("history skip explains loaded quality age and minimum",missing is {Status:SellerStatus.Skipped,Price:null}&&missing.Reason.Contains("4 sales loaded")&&missing.Reason.Contains("1 valid NQ")&&missing.Reason.Contains("0/3 within 7 days"));
        var sales=new SellerSale[]{new(100,false,99,now),new(110,false,1,now),new(120,false,2,now)};
        check("history stays per unit and bypasses undercut",History(Empty(sales),new(){UndercutGil=50,PercentUndercut=true,UndercutPercent=50}) is {Status:SellerStatus.History,Price:110});
        check("history can support a configurable single sale",History(Empty(sales[..1]),new(){MinimumHistorySales=1}).Price==100);
        check("own offer does not fall through to history",History(Empty(sales) with {Offers=[new(2,123,false,1)]}) is {Price:123,Source:"Live listings"});
        check("failed comparison cannot borrow valid history",History(Empty(sales) with {Error="Busy"}).Price==null);
        var inflated=new SellerOffer[]{new(9,10000000,false,1),new(8,20000000,false,1)};
        check("50k stays unchanged against ten million asking prices by default",Price(inflated,current:50000) is {Status:SellerStatus.Competitive,Price:50000,CanApply:false});
        var inflatedDecision=Price(inflated,new(){RaiseCompetitivePrices=true,PercentUndercut=true,UndercutPercent=2},50000);
        check("large increase blocked even with raises enabled",inflatedDecision is {Status:SellerStatus.Skipped,Price:null}&&inflatedDecision.Reason.Contains("automatic increase limit"));
        check("own-retainer match cannot create extreme automatic increase",Price([new(2,10000000,false,1)],new(){RaiseCompetitivePrices=true},50000) is {Status:SellerStatus.Skipped,CanApply:false});
        check("empty history cannot create extreme automatic increase",History(Empty([new(10000000,false,1,now),new(10000000,false,1,now),new(10000000,false,1,now)])) is {Status:SellerStatus.Skipped,CanApply:false});
        check("ordinary increase within cap is allowed",Price([new(9,151,false,1)],new(){RaiseCompetitivePrices=true},100).Price==150);
        check("exact cap boundary allowed",Price([new(9,201,false,1)],new(){RaiseCompetitivePrices=true},100).Price==200);
        check("one above cap rejected",Price([new(9,202,false,1)],new(){RaiseCompetitivePrices=true},100).Price==null);
        check("configured smaller cap respected",Price([new(9,151,false,1)],new(){RaiseCompetitivePrices=true,MaximumIncreasePercent=25},100).Price==null);
        check("zero cap blocks raises",Price([new(9,102,false,1)],new(){RaiseCompetitivePrices=true,MaximumIncreasePercent=0},100).Price==null);
        check("explicit minimum remains allowed",Price([new(9,151,false,1)],new(){RaiseCompetitivePrices=true,MaximumIncreasePercent=0,MinimumPrices=new(){[10]=200}},100).Price==200);
        check("invalid cap falls back safely",Price(inflated,new(){RaiseCompetitivePrices=true,MaximumIncreasePercent=double.NaN},50000).Price==null);
        check("background inflated listings show skipped not price update",MarketWatchPolicy.Assess(stock with {Price=50000},20,cache with {Offers=[new("Other",10000000,false,1,false)]},new HashSet<string>(),new(){RaiseCompetitivePrices=true},now) is {Status:SellerStatus.Skipped,CanApply:false});
        check("manual explicit price still honors user selection",SellerPricing.OfferPrice(10000000,false,new(){PercentUndercut=true,UndercutPercent=2})==9800000);

        // Exercise the real run and comparison capture together: a successful
        // empty response has no offering packets and history arrives later.
        foreach(var mode in new[]{"update","check","too few","disabled","failed"}){
            var port=new HistoryPort(now,sales,mode);var run=new SellerRun(port);
            run.Start([new(1,"One")],100,20,mode!="check",new(){UseHistory=mode!="disabled",OpenHistory=false},[1,2],now);
            for(var i=0;i<100&&run.Running;i++){port.Now=now.AddSeconds(i);run.Tick(port.Now);}
            check("empty-results history run "+mode,!run.Running&&port.Reviews.Count==1&&port.Writes==(mode=="update"?1:0)&&
                (mode is "update" or "check"?port.Reviews[0].Price==110:port.Reviews[0].Status==SellerStatus.Skipped));
        }
    }
    private sealed class HistoryPort(DateTimeOffset now,SellerSale[] sales,string mode):ISellerPort
    {
        public DateTimeOffset Now=now;
        private readonly SellerComparisonCapture capture=new();
        private MarketStock stock=new(0,10,"Empty market item",false,99,500,now);
        public int Writes;public List<SellerDecision> Reviews=[];
        public bool ContextMatches(ulong c,uint w)=>true;
        public bool SelectRetainer(SellerRetainerRef r)=>true;
        public bool OpenMarket(ulong r)=>true;
        public MarketStock[] ReadStock(ulong r)=>[stock];
        public bool OpenEditor(ulong r,MarketStock item)=>true;
        public bool EditorMatches(ulong r,MarketStock item)=>stock.SameItem(item)&&stock.Price==item.Price;
        public bool BeginComparison(uint item){capture.Begin(item,20,false,Now);return true;}
        public SellerComparison? Comparison {
            get {
                if(Now-capture.RequestedAt<TimeSpan.FromSeconds(2))return null;
                capture.Response(10,0,mode=="failed"?1:0,Now);
                if(capture.Error!=0)return new(10,20,capture.RequestedAt,false,[],false,[],"Game comparison failed");
                if(!capture.Ready(10,20,0))return null;
                if(mode!="disabled"){
                    if(Now-capture.RequestedAt<TimeSpan.FromSeconds(4))return null;
                    capture.ReceiveHistory(10,mode=="too few"?sales[..1]:sales,Now);
                }
                return new(10,20,capture.RequestedAt,true,[],capture.HistoryReceived,capture.History);
            }
        }
        public bool ApplyPrice(ulong r,MarketStock item,int price){Writes++;stock=stock with {Price=price};return true;}
        public bool PriceConfirmed(ulong r,MarketStock item,int price)=>stock.Price==price;
        public bool CancelEditor(ulong r,MarketStock item)=>true;
        public bool CloseRetainer(ulong r)=>true;
        public void Review(SellerRetainerRef r,MarketStock item,SellerDecision d,bool applied)=>Reviews.Add(d);
        public void Release(){}
    }
}
