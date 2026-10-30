using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class GardenObjectDiscovery333Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  var now=DateTimeOffset.UtcNow;var address=new Address("house",410,641,15,15,0,false,false,"Rafflesia","Shirogane");
  GardenTargetDetails[] Objects(int patch=1)=>Enumerable.Range(0,8).Select(b=>new GardenTargetDetails((uint)((b<<24)|((patch-1)<<16)|(14<<8)|3),12,2,45)).ToArray();
  var source=Objects();var mapped=GardenObjectDiscovery.Missing(address,source,[],now);
  Check(mapped.Length==8&&mapped.Select(m=>m.Bed).Order().SequenceEqual(Enumerable.Range(1,8)),"Complete native set discovers eight beds");
  Check(mapped.All(m=>m.Patch==1&&m.Plot==15),"Decoded plot and patch");
  Check(GardenObjectDiscovery.Missing(address,source.Take(7),[],now).Length==0,"Partial loaded set cannot infer mapping");
  Check(GardenObjectDiscovery.Missing(address,source.Concat([source[0]]),[],now).Length==0,"Duplicate bed rejects group");
  Check(GardenObjectDiscovery.Missing(address,Objects(2),[],now).All(m=>m.Patch==2),"Rotated furniture does not affect encoded identity");
  Check(GardenObjectDiscovery.Missing(address with{Plot=16},source,[],now).Length==0,"Adjacent plot excluded");
  Check(GardenObjectDiscovery.Missing(address,source.Select((t,i)=>i==7?t with{X=30}:t),[],now).Length==0,"Different physical groups cannot combine");
  Check(GardenObjectDiscovery.Missing(address,source.Select((t,i)=>i==0?t with{X=float.NaN}:t),[],now).Length==0,"Invalid coordinates excluded");
  Check(GardenObjectDiscovery.Missing(address,source,mapped,now).Length==0,"Existing mappings are not rewritten");
  var partial=mapped.Take(3).Select(m=>m with{Patch=2}).ToArray();
  Check(GardenObjectDiscovery.Missing(address,source,partial,now).All(m=>m.Patch==2),"Existing manual batch identity preserved");
  Check(GardenObjectDiscovery.Missing(address,source,[mapped[0] with{Bed=3}],now).Length==0,"Conflicting manual bed numbering requires review");
  foreach(var plot in Enumerable.Range(1,60))
   foreach(var patch in Enumerable.Range(1,3))
    foreach(var bed in Enumerable.Range(1,8)){
     var arg=(uint)(((bed-1)<<24)|((patch-1)<<16)|((plot-1)<<8)|255);
     Check(GardenObjectDiscovery.Decode(arg)==(plot,patch,bed),"Identity decoding bounds");
    }
  Check(GardenObjectDiscovery.Decode(0x08000000)==null&&GardenObjectDiscovery.Decode(0x00030000)==null&&GardenObjectDiscovery.Decode(0x00003C00)==null,"Out-of-range identities rejected");
  Console.WriteLine("PASS automatic garden identity discovery and existing mapping preservation");
 }
}
