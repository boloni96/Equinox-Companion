using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class ArmouryWithdrawal310Tests
{
 [ModuleInitializer] internal static void Run(){
    void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS armoury withdrawal "+label);}
    var receipt=new WithdrawalArmouryReceipt();receipt.Begin(new Dictionary<uint,int>{{100,4}});
    Check("existing armoury gear does not satisfy bag targets",receipt.Held(100,0,4)==0);
    Check("new armoury gear confirms arrival",receipt.Held(100,0,5)==1);
    Check("bag fallback still counts",receipt.Held(100,1,4)==1);
    Check("moving owned bag gear into armoury does not double count",receipt.Held(100,0,5)==receipt.Held(100,1,4));
    Check("untracked materials ignore armoury",receipt.Held(200,3,99)==3);
    var now=DateTimeOffset.UtcNow;
    var p=new Port();var run=new RetrievalRun(p);
    run.Start([1],[new(100,"Boots",2),new(101,"Weapon",1)],true,now);
    for(var i=0;i<60&&run.Running;i++){p.Deliver();run.Tick(now.AddMilliseconds(i*200));}
    Check("multiple gear pieces continue after armoury receipt",!run.Running&&run.Status.StartsWith("Complete")&&p.Sent==3&&p.ConfirmedCount==3&&p.Armoury[100]==6&&p.Armoury[101]==1);
    p.Stock=[new(100,1,10000,0)];run.Start([1],[new(100,"Boots",1)],true,now);
    for(var i=0;i<30&&run.Running;i++){p.Deliver();run.Tick(now.AddMilliseconds(i*200));}
    Check("restart resets baseline rather than reusing prior receipts",p.Sent==4&&p.Armoury[100]==7&&run.Status.StartsWith("Complete"));
    p=new Port{ToBags=true};run=new(p);run.Start([1],[new(100,"Boots",2)],true,now);
    for(var i=0;i<40&&run.Running;i++){p.Deliver();run.Tick(now.AddMilliseconds(i*200));}
    Check("normal bag destination still completes",p.Bags.GetValueOrDefault(100u)==2&&p.Armoury[100]==4&&run.Status.StartsWith("Complete"));
    p=new Port{Acknowledge=false};run=new(p);run.Start([1],[new(100,"Boots",2)],true,now);
    for(var i=0;i<40&&run.Running;i++){p.Deliver();run.Tick(now.AddSeconds(i));}
    Check("missing receipt stops without repeating withdrawal",p.Sent==1&&!run.Running&&run.Status.Contains("Confirm was not confirmed"));
    p=new Port();run=new(p);run.Start([1],[new(100,"Boots",1)],true,now,allAvailable:true);
    for(var i=0;i<50&&run.Running;i++){p.Deliver();run.Tick(now.AddMilliseconds(i*200));}
    Check("all available continues across identical gear slots",p.Sent==2&&p.Armoury[100]==6&&run.Status.StartsWith("Complete"));
 }
 private sealed class Port:IRetrievalPort{
    public Dictionary<uint,int> Armoury=new(){{100,4}},Bags=[];
    public List<RetrievalSlot> Stock=[new(100,1,10000,0),new(100,1,10000,1),new(101,1,10000,2)];
    private readonly WithdrawalArmouryReceipt receipt=new();
    private RetrievalSlot? pending;private int delay;
    public bool ToBags,Acknowledge=true;public int Sent,ConfirmedCount;
    public bool ContextMatches=>true;
    public void Begin(IEnumerable<uint> items)=>receipt.Begin(items.Distinct().ToDictionary(i=>i,i=>Armoury.GetValueOrDefault(i)));
    public bool Prepare()=>true;public bool Select(ulong id)=>true;public bool OpenInventory(ulong id)=>true;
    public RetrievalSlot[] Read(ulong id)=>Stock.ToArray();
    public int Held(uint item)=>receipt.Held(item,Bags.GetValueOrDefault(item),Armoury.GetValueOrDefault(item));
    public bool Withdraw(ulong id,RetrievalSlot slot,int amount){Sent++;pending=slot;delay=2;return true;}
    public void Deliver(){if(pending is not {} slot||!Acknowledge||--delay>0)return;var target=ToBags?Bags:Armoury;target[slot.ItemId]=target.GetValueOrDefault(slot.ItemId)+1;Stock.Remove(slot);pending=null;}
    public void Confirmed(ulong id,RetrievalSlot slot,int amount)=>ConfirmedCount++;
    public bool Close(ulong id)=>true;public void Release(){}
 }
}
