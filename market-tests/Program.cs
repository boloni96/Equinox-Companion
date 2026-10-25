using EquinoxCompanion;
using System.Text.Json;
var now=DateTimeOffset.Parse("2026-10-25T10:00:00Z");var tests=0;
void Check(string name,bool ok){if(!ok)throw new Exception("FAIL "+name);tests++;Console.WriteLine("PASS "+name);}
SellerComparison Compare(long[] prices,bool hq=false)=>new(10,20,now,true,prices.Select((p,i)=>new SellerOffer((ulong)(i+10),p,hq,99)).ToArray(),false,[]);
SellerDecision Price(SellerComparison? c,SellerSettings? s=null,int old=300000,bool hq=false)=>SellerPricing.Decide(10,20,hq,old,c,new HashSet<ulong>{1,2},s??new(),now);
Check("100k outlier uses 250k",Price(Compare([100000,250000,260000])).Price==249999);
Check("duplicate cheap listings count one price level",Price(Compare([100000,100000,250000,260000])).Price==249999);
Check("normal cheapest price",Price(Compare([200000,250000])).Price==199999);
Check("gap equal to threshold remains cheapest",Price(Compare([150000,250000])).Price==149999);
Check("outlier rule can be disabled",Price(Compare([100000,250000]),new(){LowOutlierPercent=0}).Price==99999);
Check("only one competing listing",Price(Compare([50000])).Price==49999);
Check("known own listings excluded",Price(Compare([100000,250000]) with {Offers=[new(1,100, false,1),new(9,10000,false,1)]}).Price==9999);
Check("own-only market keeps price",Price(Compare([]) with {Offers=[new(1,100,false,1)]}).Status==SellerStatus.Competitive);
Check("no HQ/NQ mixing",Price(Compare([100000],true)).Price==null);
Check("failed lookup never treated as empty",Price(Compare([]) with {Complete=false,HistoryComplete=true,History=[new(500,false,1,now)]}).Price==null);
Check("error preserves existing price",Price(Compare([10]) with {Error="Busy"}).Price==null);
Check("wrong item rejected",Price(Compare([100]) with {ItemId=11}).Price==null);
Check("wrong world rejected",Price(Compare([100]) with {WorldId=21}).Price==null);
Check("old result rejected",Price(Compare([100]) with {At=now.AddMinutes(-6)}).Price==null);
Check("future result rejected",Price(Compare([100]) with {At=now.AddMinutes(1)}).Price==null);
Check("missing comparison rejected",Price(null).Price==null);
Check("already lowest stays competitive",Price(Compare([100000]),old:99998).Status==SellerStatus.Competitive);
Check("equal target no write",Price(Compare([100000]),old:99999).CanApply==false);
Check("optional raises",Price(Compare([100000]),new(){RaiseCompetitivePrices=true},old:99998).Price==99999);
Check("minimum respected",Price(Compare([100000]),new(){MinimumPrices=new(){[10]=110000}}).Price==110000);
Check("below minimum never kept",Price(Compare([100000]),new(){MinimumPrices=new(){[10]=110000}},old:100).Price==110000);
Check("excluded item",Price(Compare([100000]),new(){ExcludedItems=[10]}).Price==null);
Check("percent undercut",Price(Compare([100000]),new(){PercentUndercut=true,UndercutPercent=2.5}).Price==97500);
Check("rounding",Price(Compare([100000]),new(){RoundTo=100}).Price==99900);
Check("floor is one",Price(Compare([1])).Price==1);
Check("overflow-safe max",SellerPricing.Adjust(long.MaxValue,new())==SellerPricing.MaxPrice);
SellerComparison History(params long[] prices)=>Compare([]) with {HistoryComplete=true,History=prices.Select(p=>new SellerSale(p,false,1,now.AddHours(-1))).ToArray()};
Check("empty market median",Price(History(100,110,120)).Price==110);
Check("highest supported sale",Price(History(100,110,120),new(){HistoryRule=SellerHistoryRule.HighestSupported}).Price==120);
Check("single huge sale excluded",Price(History(100,110,120,100000),new(){HistoryRule=SellerHistoryRule.HighestSupported}).Price==120);
Check("too few recent sales",Price(History(100,110)).Price==null);
Check("old sales rejected",Price(History(100,110,120) with {History=History(100,110,120).History.Select(x=>x with {At=now.AddDays(-10)}).ToArray()}).Price==null);
Check("history disabled",Price(History(100,110,120),new(){UseHistory=false}).Price==null);
Check("incomplete history rejected",Price(History(100,110,120) with {HistoryComplete=false}).Price==null);
Check("mannequins excluded",Price(History(100,110,120) with {History=History(100,110,120).History.Select(x=>x with {Mannequin=true}).ToArray()}).Price==null);
Check("latest history respects order",Price(History(100,110,120) with {History=[new(150,false,1,now),new(100,false,99,now.AddHours(-1)),new(110,false,2,now.AddHours(-2))]},new(){HistoryRule=SellerHistoryRule.Latest}).Price==150);
Check("stack 99 bounds availability",SellerPricing.Stack(99,22,99)==22);
Check("stack item max",SellerPricing.Stack(99,100,1)==1);
Check("stack single",SellerPricing.Stack(1,99,99)==1);
Check("no stock no listing",SellerPricing.Stack(99,0,99)==0);
var settings=JsonSerializer.Deserialize<SellerSettings>(JsonSerializer.Serialize(new SellerSettings{NewStackSize=1,MinimumPrices=new(){[10]=99}}))!;
Check("settings persist",settings.NewStackSize==1&&settings.MinimumPrices[10]==99);

var stock=new MarketStock(0,10,"Test",false,99,300000,now);
var p=new FakePort(now,stock);var run=new SellerRun(p);
void Start(bool apply=true,SellerSettings? s=null){p=new FakePort(now,stock);run=new(p);run.Start([new(1,"One"),new(2,"Two")],100,20,apply,s??new(),[1,2],now);}
void Step(int n=1){for(var i=0;i<n;i++){p.Now=p.Now.AddSeconds(1);run.Tick(p.Now);}}
void Until(SellerRun.Phase phase){for(var i=0;i<100&&run.Running&&run.State!=phase;i++)Step();if(run.State!=phase)throw new Exception("Did not reach "+phase);}
Start();Step(100);Check("all retainers finish",!run.Running&&p.Closed==2);Check("one listing per stack, not per unit",p.Writes==2&&run.Updated==2);Check("stack quantities unchanged",p.Stocks.Values.All(x=>x.Quantity==99));Check("locks released at completion",p.Releases==1);
Start(false);Step(100);Check("check mode performs no writes",p.Writes==0&&p.Reviews.Count==2);
Start();Until(SellerRun.Phase.ReadComparison);run.Stop("stop");Step(100);Check("stop during comparison no late write",p.Writes==0&&p.Releases==1);
Start();Until(SellerRun.Phase.ReadComparison);run.Pause(p.Now);Step(600);Check("pause does not act or timeout",run.Running&&run.Paused&&p.Writes==0);run.Pause(p.Now);Step(100);Check("resume does fresh comparison",p.Compares>=3&&p.Writes==2);
Start();Until(SellerRun.Phase.Commit);var writes=p.Writes;run.Pause(p.Now);Step(60);run.Pause(p.Now);Step(100);Check("pause after submit never resubmits",p.Writes==2&&writes==1);
Start();Until(SellerRun.Phase.ReadComparison);p.Context=false;Step(100);Check("character/world change stops",p.Writes==0&&!run.Running&&p.Releases==1);
Start();Until(SellerRun.Phase.ReadComparison);p.EditorValid=false;Step(100);Check("changed editor cannot be confirmed",p.Writes==0&&!run.Running);
Start();p.FailComparison=true;Step(100);Check("failed comparisons keep prices and continue",p.Writes==0&&p.Closed==2&&run.Skipped==2);
Start();p.Empty=true;Step(100);Check("empty market history updates both",p.Writes==2&&p.Stocks.Values.All(x=>x.Price==110));
Start();p.NeverConfirm=true;Step(100);Check("unconfirmed commit stops without retry",p.Writes==1&&!run.Running&&p.Closed==0);
Start();p.Timeout=true;Step(100);Check("comparison timeout skips and continues",p.Writes==0&&run.Skipped==2&&p.Closed==2);
Start();Until(SellerRun.Phase.NextItem);p.Stocks[1]=stock with {ItemId=99};Step(100);Check("sold/replaced listing skipped",run.Skipped==1&&p.Writes==1);
Start();Until(SellerRun.Phase.ReadComparison);run.Pause(p.Now);p.Stocks[1]=stock with {Quantity=1};run.Pause(p.Now);Step(100);Check("quantity changed during pause stops without dismissing editor",!run.Running&&p.Editor&&p.Writes==0);
Start(s:new(){ExcludedItems=[10]});Step(100);Check("excluded listings never opened",p.Compares==0&&p.Writes==0);
Start();Until(SellerRun.Phase.Compare);p.EditorValid=false;Step();Check("editor mismatch before search stops",p.Compares==0&&!run.Running);

var old=new MarketRetainer(1,"R",100,"C",20,"W","P","A",0,now,[stock]);
Check("withdrawal does not invent revenue",MarketAccounting.Removed([stock],[]).Length==1&&MarketLedger.Empty.Income.Length==0);
Check("price change keeps original listing date",MarketAccounting.PreserveListingDates(old,old with {Stock=[stock with {Price=2,ListedAt=now.AddDays(1)}]}).Stock[0].ListedAt==now);
Check("retainer visit reminder starts at six days",MarketAccounting.VisitDue(old,now.AddDays(6),6));
Check("recent retainer visit clears reminder",!MarketAccounting.VisitDue(old with {LastVisit=now.AddDays(5)},now.AddDays(6),6));
Check("empty retainer gets no visit reminder",!MarketAccounting.VisitDue(old with {Stock=[]},now.AddDays(8),6));
Check("not due before reminder threshold",!MarketAccounting.VisitDue(old,now.AddDays(6).AddSeconds(-1),6));
Check("CSV quoting",MarketAccounting.Export([new("x",100,"A, B",20,"W",1,"R",10,"O\"ne",false,1,100,95,now,"Confirmed")]).Contains("\"O\"\"ne\""));
var template="<ennoun(Item,1,lnum2,lnum3)> you put up for sale in the markets has sold for <num(lnum4)> gil (after fees).";
var receipt=MarketSaleReceipt.FromTemplate(745,template);
Check("receipt maps verified template",receipt==new MarketSaleReceipt(1,3));
Check("receipt reads actual item and net",receipt?.Read([0,1000010,99,9500]) is {Item:10,Hq:true,Net:9500});
Check("unsupported message rejected",MarketSaleReceipt.FromTemplate(750,template)==null);
Check("ambiguous item template rejected",MarketSaleReceipt.FromTemplate(745,template+"<sheet(Item,lnum8)>")==null);
Check("negative sale amount rejected",receipt?.Read([0,10,99,-1])==null);
Check("invalid live price never falls back to history",Price(History(100,110,120) with {Offers=[new(9,0,false,1)]}).Price==null);
Check("nonfinite undercut safely normalized",SellerPricing.Adjust(10000,new(){PercentUndercut=true,UndercutPercent=double.NaN})==9900);
Start();Until(SellerRun.Phase.ReadComparison);p.Timeout=true;p.EditorValid=false;Step(30);Check("changed timeout editor is not dismissed",p.Editor&&p.Writes==0&&!run.Running);
var later=old with {At=now.AddMinutes(10),Gil=28215000,Stock=[]};
var inferred=MarketAccounting.ReconcileOffline(old,later,[]);
Check("matching stock and gil produces labelled estimate",inferred.Length==1&&inferred[0].Net==null&&inferred[0].Evidence.StartsWith("Inferred"));
Check("missing stock without gil is not revenue",MarketAccounting.ReconcileOffline(old,later with {Gil=0},[]).Length==0);
Check("unmatched gil is not revenue",MarketAccounting.ReconcileOffline(old,later with {Gil=500},[]).Length==0);
Check("different retainer not reconciled",MarketAccounting.ReconcileOffline(old,later with {Id=9},[]).Length==0);
var confirmed=new MarketIncome("x",100,"C",20,"W",1,"R",10,"Test",false,99,29700000,28215000,now.AddMinutes(1),"Confirmed");
Check("live receipt prevents offline double count",MarketAccounting.ReconcileOffline(old,later,[confirmed]).Length==0);
Check("unassigned receipt prevents offline double count",MarketAccounting.ReconcileOffline(old,later,[confirmed with {RetainerId=0}]).Length==0);
const string header="Retainer ID,World ID,Item ID,Is HQ?,Quantity,Unit Price,Tax Rate,Sold At\n";
const string row="1,20,10,1,99,100,5,2026-10-25T10:00:00Z\n";
var imported=MarketSalesImport.Read(new StringReader(header+row+row));
Check("import preserves identical legitimate sales",imported.Length==2&&imported[0].Id!=imported[1].Id);
Check("reimport uses stable identities",MarketSalesImport.Read(new StringReader(header+row+row)).Select(x=>x.Id).SequenceEqual(imported.Select(x=>x.Id)));
Check("import separates gross from net",imported[0].Gross==9900&&imported[0].Net==null);
Check("import respects UTC timestamp",imported[0].At==now);
void ImportFails(string name,string csv){try{MarketSalesImport.Read(new StringReader(csv));Check(name,false);}catch(IOException){Check(name,true);}}
ImportFails("wrong CSV rejected","Other\n1\n");
ImportFails("duplicate header rejected",header.TrimEnd()+",Item ID\n");
ImportFails("invalid HQ flag rejected",header+row.Replace(",1,99,",",2,99,"));
ImportFails("oversized price rejected",header+row.Replace(",100,",",1000000000,"));
await MarketWatchTests.Run(Check,now);
MarketFixTests.Run(Check,now);
AutoRetainerPauseTests.Run(Check,now);
ComparisonCaptureTests.Run(Check,now);
Console.WriteLine($"{tests} market checks passed.");

sealed class FakePort(DateTimeOffset now,MarketStock stock):ISellerPort
{
 public DateTimeOffset Now=now;public bool Context=true,EditorValid=true,FailComparison,Empty,NeverConfirm,Timeout;
 public int Writes,Closed,Releases,Compares;public ulong Current;public bool Editor;
 public Dictionary<ulong,MarketStock> Stocks=new(){[1]=stock,[2]=stock};public List<SellerDecision> Reviews=[];
 public bool ContextMatches(ulong c,uint w)=>Context;
 public bool SelectRetainer(SellerRetainerRef r){Current=r.Id;return true;}
 public bool OpenMarket(ulong r)=>Current==r;
 public MarketStock[]? ReadStock(ulong r)=>[Stocks[r]];
 public bool OpenEditor(ulong r,MarketStock item){if(!Stocks[r].SameItem(item))return false;Editor=true;return true;}
 public bool EditorMatches(ulong r,MarketStock item)=>Editor&&EditorValid&&Stocks[r].SameItem(item)&&Stocks[r].Price==item.Price;
 public bool BeginComparison(uint item){Compares++;return true;}
 public SellerComparison? Comparison=>Timeout?null:new(10,20,Now,!FailComparison,Empty?[]:[new(99,250000,false,1)],true,[new(100,false,1,Now),new(110,false,1,Now),new(120,false,1,Now)],FailComparison?"Busy":"");
 public bool ApplyPrice(ulong r,MarketStock item,int price){Writes++;Editor=false;Stocks[r]=item with {Price=price};return true;}
 public bool PriceConfirmed(ulong r,MarketStock item,int price)=>!NeverConfirm&&Stocks[r].Price==price;
 public bool CancelEditor(ulong r,MarketStock item){if(Editor&&!EditorMatches(r,item))return false;Editor=false;return true;}
 public bool CloseRetainer(ulong r){Closed++;return true;}
 public void Review(SellerRetainerRef r,MarketStock item,SellerDecision d,bool applied)=>Reviews.Add(d);
 public void Release()=>Releases++;
}
