using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Retriever264Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception("Retriever: "+name);Console.WriteLine("PASS 264 "+name);}
        void Reject(string name,Action action){try{action();}catch(ArgumentException){Check(name,true);return;}throw new Exception(name+" accepted invalid input");}
        var ingot=new RetrievalEntry(1,"Cobalt Ingot",600);var ore=new RetrievalEntry(2,"Cobalt Ore",2);var recipes=new Dictionary<uint,RetrievalRecipe[]>{{1,[new(10,1,1,[ore],"Blacksmith")]}};
        Check("600 finished ingots remain ingots",RetainerRetrieval.Expand([ingot],1,RetrievalMode.FinalItems,recipes).Single()==ingot);
        Check("explicit material expansion produces 1200 ore",RetainerRetrieval.Expand([ingot],1,RetrievalMode.AllMaterials,recipes).Single().Quantity==1200);
        Check("preset multiplier is applied once",RetainerRetrieval.Expand([ingot],4,RetrievalMode.FinalItems,recipes).Single().Quantity==2400);
        Check("same item rows combine",RetainerRetrieval.Combine([ingot,ingot]).Single().Quantity==1200);
        var p=new RetrievalPreset("Workshop",[ingot]);var decoded=RetainerRetrieval.Import(RetainerRetrieval.Export(p),x=>x=="1"?ingot:null);Check("preset roundtrip",decoded.Name==p.Name&&decoded.Items.SequenceEqual(p.Items));
        Check("whole-list text resolves exact items",RetainerRetrieval.Import("600x Cobalt Ingot\n20x Cobalt Ingot",x=>x=="Cobalt Ingot"?ingot:null).Items.Single().Quantity==620);
        Reject("unknown item rejects whole import",()=>RetainerRetrieval.Import("600x Cobalt Ingot\n1x Unknown",x=>x=="Cobalt Ingot"?ingot:null));
        Reject("overflow quantity rejects",()=>RetainerRetrieval.Expand([ingot with{Quantity=1_000_000}],2,RetrievalMode.FinalItems,recipes));
        var cycle=new Dictionary<uint,RetrievalRecipe[]>{{1,[new(10,1,1,[ore],"A")]},{2,[new(11,2,1,[ingot],"B")]}};Reject("recipe cycle stops",()=>RetainerRetrieval.Expand([ingot],1,RetrievalMode.AllMaterials,cycle));
        var yield=new Dictionary<uint,RetrievalRecipe[]>{{1,[new(10,1,3,[ore],"A")]}};Check("recipe yields round up",RetainerRetrieval.Expand([ingot with{Quantity=4}],1,RetrievalMode.AllMaterials,yield).Single().Quantity==4);
        var alternate=new Dictionary<uint,RetrievalRecipe[]>{{1,[new(10,1,1,[ore],"A"),new(11,1,1,[ore with{ItemId=3}],"B")]}};Reject("ambiguous recipe is not guessed",()=>RetainerRetrieval.Expand([ingot],1,RetrievalMode.AllMaterials,alternate));
        Check("selected recipe controls expansion",RetainerRetrieval.Expand([ingot with{RecipeId=11}],1,RetrievalMode.AllMaterials,alternate).Single().ItemId==3);
        var port=new Port();var run=new RetrievalRun(port);var now=DateTimeOffset.UtcNow;run.Start([1,2],[new(1,"Ingot",600)],true,now);for(var i=0;i<30&&run.Running;i++)run.Tick(now.AddSeconds(i));Check("multi-retainer exact partial withdrawal",port.Confirmations==2&&port.Count==600&&port.Taken.SequenceEqual(new[]{100,300})&&!run.Running&&port.Released);
        port=new Port();run=new(port);run.Start([1],[new(1,"Ingot",50)],false,now);for(var i=0;i<20&&run.Running;i++)run.Tick(now.AddSeconds(i));Check("subtract disabled retrieves additional quantity",port.Count==250);
        port=new Port();run=new(port);run.Start([1,2],[new(1,"Ingot",1)],true,now,allAvailable:true);for(var i=0;i<40&&run.Running;i++)run.Tick(now.AddSeconds(i));Check("all available ignores quantity one and already held bags",port.Count==800&&port.Taken.SequenceEqual(new[]{100,500})&&!run.Running&&run.Status.StartsWith("Complete"));
        port=new Port{Confirm=false};run=new(port);run.Start([1,2],[new(1,"Ingot",1)],false,now,allAvailable:true);for(var i=0;i<40;i++)run.Tick(now.AddSeconds(i));Check("all available still requires transfer confirmation",port.Taken.Count==1&&!run.Running);
        port=new Port{Confirm=false};run=new(port);run.Start([1],[new(1,"Ingot",300)],true,now);for(var i=0;i<30;i++)run.Tick(now.AddSeconds(i));Check("no blind repeat when acknowledgement missing",port.Taken.Count==1&&!run.Running);
        port=new Port();run=new(port);run.Start([1],[ingot],true,now);port.ContextMatches=false;run.Tick(now);Check("character or world change stops before action",port.Taken.Count==0&&!run.Running&&port.Released);
        port=new Port();run=new(port);run.Start([1],[ingot],true,now);run.Stop("cancel");run.Tick(now);Check("cancel releases and performs no actions",port.Released&&port.Taken.Count==0);
    }
    private sealed class Port:IRetrievalPort
    {
        public bool ContextMatches{get;set;}=true;public bool Confirm=true,Released;public int Confirmations;public void Confirmed(ulong r,RetrievalSlot s,int q)=>Confirmations++;public int Count=200;public List<int>Taken=[];private Dictionary<ulong,int> stock=new(){{1,100},{2,500}};
        public bool Prepare()=>true;public bool Select(ulong r)=>true;public bool OpenInventory(ulong r)=>true;public RetrievalSlot[] Read(ulong r)=>stock[r]>0?[new(999,99,10000,1),new(1,stock[r],10000,0)]:[new(999,99,10000,1)];public int Held(uint i)=>Count;
        public bool Withdraw(ulong r,RetrievalSlot s,int q){if(s.ItemId!=1)throw new Exception("Moved an item outside the plan");Taken.Add(q);if(Confirm){Count+=q;stock[r]-=q;}return true;}public bool Close(ulong r)=>true;public void Release()=>Released=true;
    }
}

