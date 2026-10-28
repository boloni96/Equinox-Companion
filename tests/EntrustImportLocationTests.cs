using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class EntrustImportLocationTests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS entrust location "+label);}
  var dir=Path.Combine(Path.GetTempPath(),"equinox-import-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
  try{
   File.WriteAllText(Path.Combine(dir,"AutoRetainer.json"),"{\"IMProtectList\":[10]}");
   Check("legacy settings",EntrustSelection.ReadProtectionDirectory(dir).SequenceEqual(new uint[]{10}));
   Directory.CreateDirectory(Path.Combine(dir,"AutoRetainer"));var modern=Path.Combine(dir,"AutoRetainer","DefaultConfig.json");
   File.WriteAllText(modern,"{\"InventoryPlans\":[{\"IMProtectList\":[20,30]}]}");
   Check("modern settings override stale legacy",EntrustSelection.ReadProtectionDirectory(dir).Order().SequenceEqual(new uint[]{20,30}));
   File.Delete(Path.Combine(dir,"AutoRetainer.json"));
   Check("modern only installation works",EntrustSelection.ReadProtectionDirectory(dir).Length==2);
   File.WriteAllText(modern,"{}");File.WriteAllText(Path.Combine(dir,"AutoRetainer.json"),"{\"IMProtectList\":[10]}");
   var rejected=false;try{EntrustSelection.ReadProtectionDirectory(dir);}catch(ArgumentException){rejected=true;}Check("broken modern file never reads stale legacy",rejected);
  }finally{Directory.Delete(dir,true);}
  var old=new RetrievalPreset("4x shark submarines",[new(100,"Shark part",4)]);
  var imported=new RetrievalPreset("Entrust plan 1",[new(200,"Ore",1)],Entrust:new([],[],[]));
  var plans=EntrustSelection.AddImportedPlan([old],old with{Items=[new(100,"Shark part",8)]},old.Name,imported);
  Check("existing edits preserved separately",plans.Length==2&&plans[0].Items[0].Quantity==8&&plans[1].Entrust!=null);
  plans=EntrustSelection.AddImportedPlan([old,imported],old,null,imported);
  Check("draft and duplicate import never overwrite existing names",plans.Select(x=>x.Name).Distinct().Count()==4&&plans[0].Name==old.Name&&plans[^1].Name=="Entrust plan 1 (2)");
  var full=Enumerable.Range(0,25).Select(i=>old with{Name="Plan "+i}).ToArray();var blocked=false;
  try{EntrustSelection.AddImportedPlan(full,old,null,imported);}catch(ArgumentException){blocked=true;}
  Check("limit failure leaves original collection intact",blocked&&full.Length==25&&full[0].Name=="Plan 0");
 }
}
