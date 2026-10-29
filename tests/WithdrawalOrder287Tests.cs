using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class WithdrawalOrder287Tests
{
 [ModuleInitializer] internal static void Run()
 {
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS withdrawal287 "+label);}
  var items=new[]{new RetrievalEntry(100,"Cloth",5)};
  var stock=new[]{new WithdrawalStock(3,100,20,false),new WithdrawalStock(99,100,100,false)};
  var order=WithdrawalRetainerOrder.Build([1,2,3,4],stock,items,_=>0,true,RetrievalQuality.Any);
  Check("recorded holder first; other characters excluded",order.Retainers.SequenceEqual(new ulong[]{3,1,2,4})&&order.KnownHolders==1);
  var p=new Port{Stock=new(){{3,20}}};var run=new RetrievalRun(p);var now=DateTimeOffset.UtcNow;
  run.Start(order.Retainers,items,true,now,order.KnownHolders);for(var i=0;i<60&&run.Running;i++)run.Tick(now.AddSeconds(i));
  Check("successful known-stock withdrawal never opens other retainers",p.Opened.SequenceEqual(new ulong[]{3})&&p.Count==5&&!run.Running);
  p=new Port{Stock=new(){{2,5}}};run=new(p);run.Start(order.Retainers,items,true,now,order.KnownHolders);var fallback=false;
  for(var i=0;i<60&&run.Running;i++){run.Tick(now.AddSeconds(i));fallback|=run.Status.Contains("checking missing stock");}
  Check("stale record falls back only after known holder fails",p.Opened.SequenceEqual(new ulong[]{3,1,2})&&p.Count==5&&fallback);
  Check("already held request has no retainer route",WithdrawalRetainerOrder.Build([1,2,3],stock,items,_=>5,true,RetrievalQuality.Any).Retainers.Length==0);
  Check("additional-quantity mode still visits known holder",WithdrawalRetainerOrder.Build([1,2,3],stock,items,_=>5,false,RetrievalQuality.Any).Retainers[0]==3);
  var quality=WithdrawalRetainerOrder.Build([1,2,3],[new(1,100,5,false),new(3,100,5,true)],items,_=>0,true,RetrievalQuality.HqOnly);
  Check("quality filter chooses the matching holder",quality.KnownHolders==1&&quality.Retainers[0]==3);
  p=new Port{Count=5};run=new(p);run.Start([1,2],items,true,now);run.Tick(now);
  Check("inventory satisfied before prepare avoids all retainer selection",!run.Running&&p.Opened.Count==0&&p.Released);
 }
 private sealed class Port:IRetrievalPort
 {
  public bool ContextMatches=>true;public int Count;public bool Released;public Dictionary<ulong,int> Stock=[];public List<ulong> Opened=[];
  public bool Prepare()=>true;
  public bool Select(ulong id){Opened.Add(id);return true;}
  public bool OpenInventory(ulong id)=>true;
  public RetrievalSlot[] Read(ulong id)=>Stock.GetValueOrDefault(id)>0?[new(100,Stock[id],10000,0)]:[];
  public int Held(uint id)=>Count;
  public bool Withdraw(ulong id,RetrievalSlot slot,int amount){Stock[id]-=amount;Count+=amount;return true;}
  public bool Close(ulong id)=>true;
  public void Release()=>Released=true;
 }
}
