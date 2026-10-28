using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class EntrustImportTests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS entrust import "+label);}
  RetrievalEntry? Resolve(string id)=>uint.TryParse(id,out var i)?new(i,"Item "+id,1):null;
  var json=JsonSerializer.Serialize(new{Name="Entrust plan 1",EntrustItems=Enumerable.Range(100,1200),EntrustItemsAmountToKeep=new Dictionary<uint,int>{{100,7}},EntrustCategories=new[]{new{ID=58,AmountToKeep=2}},Duplicates=false,AllowEntrustFromArmory=false,ExcludeProtected=true});
  var p=RetainerRetrieval.Import(json,Resolve,()=>[101]);
  Check("large lists are not truncated",p.Items.Length==1200);
  Check("bag selection without crafting expansion",p.Entrust!=null&&p.Mode==RetrievalMode.CompletedItems&&p.DestinationRetainer==StorageAutoRouting.AllRetainers);
  ChestStack[] bags=[new(0,0,100,10,false,99),new(0,1,100,10,true,99),new(0,2,101,20,false,99),new(0,3,2000,5,false,99),new(0,4,3000,8,false,99)];
  var amounts=p.Entrust!.Amounts(p.Items.Select(x=>x.ItemId),bags,RetrievalQuality.Any,id=>id==2000?58u:1,p.SendCategories!);
  Check("HQ NQ shared keep budget",amounts[100]==13);
  Check("protection and unrelated items untouched",!amounts.ContainsKey(101)&&!amounts.ContainsKey(3000));
  Check("category keep retained",amounts[2000]==3);
  Check("quality filter retained",p.Entrust.Amounts([100],bags,RetrievalQuality.HqOnly,_=>1,[])[100]==3);
  var round=RetainerRetrieval.Import(RetainerRetrieval.Export(p),Resolve);
  Check("preset round trip preserves selection and protection",round.Entrust!.Keep[100]==7&&round.Entrust.Protected.Single()==101&&round.Items.Length==1200);
  Check("copies union of protection lists",EntrustSelection.ReadProtection("{\"Plans\":[{\"IMProtectList\":[1,2]},{\"IMProtectList\":{\"$values\":[2,3]}}]}").Order().SequenceEqual(new uint[]{1,2,3}));
  bool Reject(Action a){try{a();return false;}catch(ArgumentException){return true;}}
  Check("missing protection never silently ignored",Reject(()=>RetainerRetrieval.Import(json,Resolve)));
  Check("unreadable protection never silently ignored",Reject(()=>EntrustSelection.ReadProtection("{}")));
  Check("armoury import explicitly rejected",Reject(()=>RetainerRetrieval.Import(json.Replace("\"AllowEntrustFromArmory\":false","\"AllowEntrustFromArmory\":true"),Resolve,()=>[])));
  Check("duplicate rules not silently changed",Reject(()=>RetainerRetrieval.Import(json.Replace("\"Duplicates\":false","\"Duplicates\":true"),Resolve,()=>[])));
 }
}
