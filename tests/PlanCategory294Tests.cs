using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class PlanCategory294Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS plan category294 "+label);}
  RetrievalEntry[] listed=[new(8,"Fire crystal",25)];RetrievalEntry[] category=[new(8,"Fire crystal",1),new(9,"Ice crystal",1),new(9,"Ice crystal",1)];
  var result=PlanCategoryRules.Withdraw(listed,category,100);
  Check("whole category adds unlisted types",result.Length==2&&result.Single(x=>x.ItemId==9).Quantity==100);
  Check("explicit quantity wins",result.Single(x=>x.ItemId==8).Quantity==25);
  Check("crystal capacity caps rule",PlanCategoryRules.Withdraw([],category,20000).All(x=>x.Quantity==9999));
  Check("category-only plan works",PlanCategoryRules.Withdraw([],category,10).Length==2);
  ChestStack[] bags=[new(2001,6,8,100,false,9999),new(2001,7,9,150,false,9999),new(0,0,100,50,false,99)];
  var amounts=PlanCategoryRules.Entrust(bags,[8],new HashSet<uint>{59},id=>id<20?59u:10u,RetrievalQuality.Any,20);
  Check("category entrust keeps reserve and excludes explicit items",amounts.Count==1&&amounts[9]==130);
  Check("HQ-only never transfers crystals",PlanCategoryRules.Entrust(bags,[],new HashSet<uint>{59},_=>59,RetrievalQuality.HqOnly,0).Count==0);
  var time=DateTimeOffset.UtcNow;var port=new Port();var run=new RetrievalRun(port);
  run.Start([1],[new(8,"Fire",10),new(9,"Ice",10)],true,time,categoryItems:[8,9]);
  for(var i=0;i<30&&run.Running;i++)run.Tick(time.AddMilliseconds(i*200));
  Check("category run takes available stock and skips absent types",!run.Running&&port.Amount==3&&run.Status.StartsWith("Complete"));
  port=new Port();run=new RetrievalRun(port);run.Start([1],[new(8,"Fire",10),new(9,"Ice",10)],true,time,categoryItems:[8]);
  for(var i=0;i<30&&run.Running;i++)run.Tick(time.AddMilliseconds(i*200));
  Check("missing explicit item still reported",!run.Running&&run.Status.Contains("still missing"));
  var preset=new RetrievalPreset("Crystals",[],SendCategories:[59],CategoryWithdrawQuantity:100,CategoryKeepQuantity:20);
  var restored=JsonSerializer.Deserialize<RetrievalPreset>(JsonSerializer.Serialize(preset))!;
  Check("rules and amounts round trip",restored.Items.Length==0&&restored.SendCategories!.Single()==59&&restored.CategoryWithdrawQuantity==100&&restored.CategoryKeepQuantity==20);
 }
 private sealed class Port:IRetrievalPort
 {
  public int Amount;
  public bool ContextMatches=>true;
  public bool Prepare()=>true;
  public bool Select(ulong id)=>true;
  public bool OpenInventory(ulong id)=>true;
  public RetrievalSlot[] Read(ulong id)=>Amount==0?[new(8,3,12001,6,false)]:[];
  public int Held(uint item)=>item==8?Amount:0;
  public bool Withdraw(ulong id,RetrievalSlot slot,int quantity){Amount+=quantity;return true;}
  public bool Close(ulong id)=>true;
  public void Release(){}
 }
}
