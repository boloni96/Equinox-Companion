using EquinoxCompanion;
using System.Runtime.CompilerServices;

internal static class Workshop589Tests
{
    private sealed class Port : IRetrievalPort
    {
        public readonly List<ulong> Visits=[];
        public readonly Dictionary<(uint Item,bool Hq),int> Bags=[];
        public readonly Dictionary<ulong,RetrievalSlot[]> Stock=new(){
            [7]=[new(100,12,0,0,true),new(100,5,0,1),new(101,117,0,2)],
            [8]=[new(102,8,0,0)]};
        public bool ContextMatches=>true;
        public bool Prepare()=>true;
        public bool Select(ulong id){Visits.Add(id);return true;}
        public bool OpenInventory(ulong id)=>true;
        public RetrievalSlot[] Read(ulong id)=>Stock[id].Where(s=>s.Quantity>0).ToArray();
        public int Held(uint item)=>Bags.Where(p=>p.Key.Item==item).Sum(p=>p.Value);
        public bool Withdraw(ulong id,RetrievalSlot slot,int quantity){
            if(quantity<=0||quantity>slot.Quantity)throw new Exception("Invalid move");
            Bags[(slot.ItemId,slot.Hq)]=Bags.GetValueOrDefault((slot.ItemId,slot.Hq))+quantity;
            Stock[id][slot.Slot]=slot with{Quantity=slot.Quantity-quantity};return true;
        }
        public bool Close(ulong id)=>true;
        public void Release(){}
    }

    [ModuleInitializer] internal static void Run()
    {
        var port=new Port();
        var stock=port.Stock.Values.SelectMany(s=>s).Select(s=>new WorkshopBulkStock(s.ItemId,s.Quantity,s.Hq,false))
            .Append(new(2,8647,false,true)).ToArray();
        RetrievalEntry[] nq=[new(100,"Ingot",1)],hq=[new(100,"Ingot",2)];
        RetrievalEntry[] ingredients=[new(100,"Ingot",4),new(101,"Hardened Sap",5),new(102,"Ore",1),new(2,"Fire Shard",8647)];
        var batches=WorkshopBulkPolicy.Preparation(nq,hq,true,ingredients,stock,false,id=>id==2?9998:0,out var deferred);
        var bell=batches.Single(b=>!b.Chest);
        if(deferred!=8646||batches.Single(b=>b.Chest).Items.Single().Quantity!=1)throw new Exception("Crystal headroom");
        var limits=bell.RetainerParts.SelectMany(p=>p.Items.Select(i=>(Key:(i.ItemId,p.Quality==RetrievalQuality.HqOnly),i.Quantity)))
            .ToDictionary(i=>i.Key,i=>i.Quantity);
        var run=new RetrievalRun(port);
        var now=DateTimeOffset.UtcNow;
        run.Start([7,8],bell.Items,false,now,slotLimit:s=>limits.GetValueOrDefault((s.ItemId,s.Hq))-port.Bags.GetValueOrDefault((s.ItemId,s.Hq)));
        for(int i=0;i<100&&run.Running;i++)run.Tick(now.AddMilliseconds(i));
        if(!run.Status.StartsWith("Complete")||!port.Visits.SequenceEqual(new ulong[]{7,8})||
            port.Bags.GetValueOrDefault((100u,false))!=5||port.Bags.GetValueOrDefault((100u,true))!=2||
            port.Held(101)!=5||port.Held(102)!=1)throw new Exception("Grouped retainer visit or quality allocation");
        var extras=WorkshopBulkPolicy.Preparation(nq,hq,false,ingredients,stock,true,id=>9999,out deferred);
        if(deferred!=8647||extras.Length!=1||extras[0].Items.Single(i=>i.ItemId==100).Quantity!=4)throw new Exception("Ingredient-only reservations");
        Console.WriteLine("PASS workshop589: combined withdrawals visit each retainer once and respect quality, reservations and crystal capacity");
    }
}
