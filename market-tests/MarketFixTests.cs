using EquinoxCompanion;
using System.Buffers.Binary;
using System.Text;

static class MarketFixTests
{
    public static void Run(Action<string,bool> check,DateTimeOffset now)
    {
        check("clicked gil comma",SellerPricing.TryReadGil("12,345\uE049",out var price)&&price==12345);
        check("clicked gil localized separators",SellerPricing.TryReadGil("12.345",out price)&&price==12345);
        check("invalid clicked price rejected",!SellerPricing.TryReadGil("-123",out _)&&!SellerPricing.TryReadGil("Loading…",out _)&&!SellerPricing.TryReadGil("1,000,000,000",out _));
        var compare=new SellerComparison(10,20,now,true,[new(99,100,false,1),new(98,250,false,1)],false,[]);
        var own=new HashSet<ulong>{1};var settings=new SellerSettings();
        var held=SellerPricing.Decide(10,20,false,200,compare,own,settings,now);
        check("ignored low price stays visible without automatic write",held.Undercut&&held.NeedsAttention&&!held.CanApply&&held.VisualStatus==SellerStatus.Update);
        var stacks=new[]{80,200,400}.Select(p=>SellerPricing.Decide(10,20,false,p,compare,own,settings,now)).ToArray();
        check("duplicate stacks have independent states",!stacks[0].NeedsAttention&&stacks[1].NeedsAttention&&stacks[2].CanApply);
        check("counter counts stacks not quantity",stacks.Count(x=>x.NeedsAttention)==2);
        check("own low offer excluded",!SellerPricing.Decide(10,20,false,200,compare with {Offers=[new(1,100,false,1),new(98,250,false,1)]},own,settings,now).Undercut);
        check("HQ does not make NQ red",!SellerPricing.Decide(10,20,false,200,compare with {Offers=[new(99,100,true,1),new(98,250,false,1)]},own,settings,now).Undercut);
        check("minimum preserved under cheaper offers",SellerPricing.Decide(10,20,false,400,compare,own,new(){MinimumPrices=new(){[10]=300}},now).Price==300);
        check("excluded stack not counted",!SellerPricing.Decide(10,20,false,400,compare,own,new(){ExcludedItems=[10]},now).NeedsAttention);
        var bytes=new byte[MarketRetainerHistory.PacketSize];
        void Row(int i,uint item=10,uint qty=2,uint net=190){var row=bytes.AsSpan(8+i*52,52);BinaryPrimitives.WriteUInt32LittleEndian(row,item);BinaryPrimitives.WriteUInt32LittleEndian(row[4..],net);BinaryPrimitives.WriteUInt32LittleEndian(row[8..],(uint)now.ToUnixTimeSeconds());BinaryPrimitives.WriteUInt32LittleEndian(row[12..],qty);Encoding.UTF8.GetBytes("Buyer Name").CopyTo(row[19..]);}
        Row(0);Row(1);
        var sales=MarketRetainerHistory.Read(bytes,now);
        check("history uses net and native timestamp",sales.Length==2&&sales[0].Net==190&&sales[0].Quantity==2&&sales[0].At==now);
        check("same-second identical sales distinct",sales[0].Occurrence==0&&sales[1].Occurrence==1);
        var retainer=new MarketRetainer(1,"One",100,"Character",20,"World","Person","Account",0,now,[]);
        var merged=MarketRetainerHistory.Merge([],retainer,sales,_=>"Item");
        check("history repeated is idempotent",MarketRetainerHistory.Merge(merged,retainer,sales,_=>"Item").Length==2);
        check("different retainers remain distinct",MarketRetainerHistory.Merge(merged,retainer with {Id=2},sales,_=>"Item").Length==4);
        var receipt=new MarketIncome("receipt",100,"Character",20,"World",0,"Unknown",10,"Item",false,0,null,190,now.AddSeconds(4),"Game sale message; net confirmed");
        check("history enriches live receipt without double counting",MarketRetainerHistory.Merge([receipt],retainer,sales[..1],_=>"Item") is [{RetainerId:1,Quantity:2,Net:190}]);
        var inferred=receipt with {Id="inferred:1",RetainerId=1,Quantity=2,Net=null,Gross=200,At=now.AddHours(1),Evidence=$"Inferred since {now.AddHours(-1):O}; sale time/net not confirmed"};
        check("history replaces inferred sale",MarketRetainerHistory.Merge([inferred],retainer,sales[..1],_=>"Item") is [{Gross:200,Net:190}]);
        check("truncated history rejected",MarketRetainerHistory.Read(bytes.AsSpan(0,100),now).Length==0);
        bytes[8+16]=8;check("bad history flags rejected",MarketRetainerHistory.Read(bytes,now).Length==0);bytes[8+16]=0;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8+8),uint.MaxValue);check("future sale rejected",MarketRetainerHistory.Read(bytes,now).Length==0);
        Array.Clear(bytes);
        for(var i=0;i<20;i++)Row(i,item:(uint)(10+i),qty:(uint)(1+i),net:(uint)(100+i));
        var full=MarketRetainerHistory.Read(bytes,now,out var historyError);
        check("sale history captures all twenty different items",historyError.Length==0&&full.Length==20&&full.Select(x=>x.Item).SequenceEqual(Enumerable.Range(10,20).Select(x=>(uint)x)));
        var fullLedger=MarketRetainerHistory.Merge([],retainer,full,_=>"Item");
        check("all history rows persisted once across reopens",fullLedger.Length==20&&MarketRetainerHistory.Merge(fullLedger,retainer,full,_=>"Item").Length==20&&fullLedger.Sum(x=>x.Net)==2190);
        bytes[8+5*52+16]=8;
        check("diagnostics identify malformed later history row",MarketRetainerHistory.Read(bytes,now,out historyError).Length==0&&historyError.Contains("row 6"));
        var port=new MultiStackPort(now);var run=new SellerRun(port);
        run.Start([new(1,"One"),new(2,"Two")],100,20,true,new(),[1,2],now);
        for(var i=1;i<150&&run.Running;i++){port.Now=now.AddSeconds(i);run.Tick(port.Now);}
        check("all stacks on all retainers are visited",!run.Running&&port.Reviews.Count==6&&port.Opened.Count==6);
        check("different-price duplicates update independently",port.Writes.SetEquals(new[]{(1UL,1),(1UL,2),(2UL,1),(2UL,2)}));
        check("competitive duplicate stack is left unchanged",port.Stock[(1,0)].Price==50&&port.Stock[(2,0)].Price==50);
        check("automatic update never changes stack size",port.Stock.Values.All(x=>x.Quantity==(uint)(x.Slot+1)));
        var trace=new SellerTrace();
        trace.Add(now,"Compare blocked","Waiting for listings",new {item=10});
        trace.Add(now.AddMilliseconds(500),"Compare blocked","Waiting for listings",new {item=10});
        check("repeated blocked frames do not flood diagnostics",trace.Snapshot().Length==1);
        var export=trace.Snapshot();
        trace.Clear();
        check("export trace survives clearing live capture",export.Length==1&&trace.Snapshot().Length==0);
        trace.Add(now,"Price click ignored","Manual item not captured",new {item=10});
        check("trace export preserves blocked reason and item",System.Text.Json.JsonSerializer.Serialize(trace.Snapshot()).Contains("Manual item not captured")&&System.Text.Json.JsonSerializer.Serialize(trace.Snapshot()).Contains("\"item\":10"));
        for(var i=0;i<1000;i++)trace.Add(now.AddSeconds(i),"State",i.ToString());
        check("trace retention bounded",trace.Snapshot().Length==SellerTrace.Capacity);
        var bell=new SellerBellSession();
        check("manual Seller away from bell is not closed",!bell.Observe(false,now));
        check("entering bell stays open",!bell.Observe(true,now));
        check("retainer transition does not close Seller",!bell.Observe(false,now.AddMilliseconds(100))&&!bell.Observe(true,now.AddMilliseconds(700)));
        check("bell departure waits for settled state",!bell.Observe(false,now.AddSeconds(1))&&bell.Observe(false,now.AddSeconds(3)));
        check("bell departure closes only once",!bell.Observe(false,now.AddSeconds(5)));
        check("selling count has labels",SellerLabels.Selling(13,6,true)=="13 selling · 6 updates");
        check("unknown data is explicit not question mark",SellerLabels.Selling(13,0,true)=="13 selling · uncertain");
    }

    private sealed class MultiStackPort : ISellerPort
    {
        public DateTimeOffset Now;
        public Dictionary<(ulong,int),MarketStock> Stock=[];
        public HashSet<(ulong,int)> Opened=[],Writes=[];
        public List<SellerDecision> Reviews=[];
        private (ulong,int)? editor;
        public MultiStackPort(DateTimeOffset now){Now=now;foreach(var id in new[]{1UL,2UL})for(var slot=0;slot<3;slot++)Stock[(id,slot)]=new(slot,10,"Same item",false,(uint)(slot+1),slot==0?50:300+slot,now,Materia:"");}
        public bool ContextMatches(ulong c,uint w)=>true;
        public bool SelectRetainer(SellerRetainerRef r)=>true;
        public bool OpenMarket(ulong r)=>true;
        public MarketStock[] ReadStock(ulong r)=>Stock.Where(x=>x.Key.Item1==r).Select(x=>x.Value).OrderBy(x=>x.Slot).ToArray();
        public bool OpenEditor(ulong r,MarketStock item){editor=(r,item.Slot);Opened.Add(editor.Value);return true;}
        public bool EditorMatches(ulong r,MarketStock item)=>editor==(r,item.Slot)&&Stock[(r,item.Slot)].SameItem(item)&&Stock[(r,item.Slot)].Price==item.Price;
        public bool BeginComparison(uint item)=>true;
        public SellerComparison Comparison=>new(10,20,Now,true,[new(99,100,false,1)],true,[]);
        public bool ApplyPrice(ulong r,MarketStock item,int price){Writes.Add((r,item.Slot));Stock[(r,item.Slot)]=item with {Price=price};editor=null;return true;}
        public bool PriceConfirmed(ulong r,MarketStock item,int price)=>editor==null&&Stock[(r,item.Slot)].Price==price;
        public bool CancelEditor(ulong r,MarketStock item){editor=null;return true;}
        public bool CloseRetainer(ulong r)=>true;
        public void Review(SellerRetainerRef r,MarketStock item,SellerDecision d,bool applied)=>Reviews.Add(d);
        public void Release(){}
    }
}
