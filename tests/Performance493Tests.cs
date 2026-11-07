using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Performance493Tests
{
 [ModuleInitializer] internal static void Run(){
  var now=DateTimeOffset.UtcNow;
  if(SaveSnapshotPolicy.Ready(true,true,now,now.AddSeconds(-30)))throw new Exception("Busy writer must not allocate replacement snapshots");
  if(!SaveSnapshotPolicy.Ready(true,false,now,now)||SaveSnapshotPolicy.Ready(false,false,now,now)||SaveSnapshotPolicy.Ready(true,false,now,now.AddSeconds(1)))throw new Exception("Dirty and due save gate");
  var cache=new UiProjectionCache<int>(250);var source=new object();var builds=0;
  for(var t=0;t<250;t++)if(cache.Get(source,"filter",t,()=>++builds)!=1)throw new Exception("Unchanged display rebuilt");
  if(cache.Get(source,"changed",249,()=>++builds)!=2||cache.Get(new object(),"changed",249,()=>++builds)!=3||cache.Get(source,"filter",500,()=>++builds)!=4)throw new Exception("Changed filters/source and expiry must refresh");
  Console.WriteLine("493 busy-save gate and display cache invalidation passed.");
 }
}
