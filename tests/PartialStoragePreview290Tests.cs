using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class PartialStoragePreview290Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string n,bool ok){if(!ok)throw new Exception(n);Console.WriteLine("PASS partial preview290 "+n);}
  var known=new HashSet<ulong>{1,2};
  var rules=StoragePreview.KnownRules([new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true),new(1,3,[],[],AllItems:true),new(3,1,[],[],AllItems:true),new(2,1,[],[],AllItems:true)],known);
  Check("unknown sources and destinations excluded",rules.Length==2&&rules[1].From==2&&rules[1].To==1);
  var inv=new Dictionary<ulong,ChestStack[]>{{0,[new(0,0,0,0,false,0),new(0,1,0,0,false,0)]},{1,[new(10000,0,100,10,false,99)]},{2,[new(10000,0,100,5,false,99)]}};
  var result=StorageRouting.Build(new("Partial",1,[rules[1]],0),inv,_=>0,includeExistingBags:false);
  var rows=StoragePreview.Describe(result.Moves);
  Check("known transfers still preview without missing retainer",rows.Length>0&&rows.All(x=>known.Contains(x.From)&&known.Contains(x.To)));
  Check("unknown destination never treated as empty",StoragePreview.KnownRules([new(1,3,[],[],AllItems:true)],known).Length==0);
 }
}
