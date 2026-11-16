using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class NarrowEntry561Tests
{
 [ModuleInitializer] internal static void Run(){
  var collision=new Entry(false);
  var search=new CollisionRouteSearch(collision,Vector3.Zero,new(8,0,2.5f),.25f,bidirectional:false,narrowApproach:true);
  for(var i=0;i<100&&!search.Finished&&search.Expanded<250;i++)search.Advance(20,64);
  if(search.Path==null)throw new Exception("Narrow plot opening was deferred behind unrelated detours");
  for(var i=1;i<search.Path.Length;i++)if(!collision.Traverse(search.Path[i-1],search.Path[i]))throw new Exception("Entry path bypassed collision checks");
  var closed=new CollisionRouteSearch(new Entry(true),Vector3.Zero,new(8,0,2.5f),.25f,bidirectional:false,narrowApproach:true);
  for(var i=0;i<100&&!closed.Finished&&closed.Expanded<250;i++)closed.Advance(20,64);
  if(closed.Path!=null)throw new Exception("Closed plot entrance was crossed");
  Console.WriteLine($"561 narrow entry reached in {search.Expanded} nodes; solid entrance rejected.");
 }
 private sealed class Entry(bool closed):IWalkingCollision
 {
  private bool Open(Vector3 p)=>p.X>=-10&&p.X<=8.5f&&p.Z>=-10&&p.Z<=10&&
   (p.X<=3||!closed&&(MathF.Abs(p.Z)<=.13f&&p.X<=3.63f||MathF.Abs(p.X-3.5f)<=.13f&&p.Z>=0&&p.Z<=2.63f||MathF.Abs(p.Z-2.5f)<=.13f&&p.X>=3.37f));
  public bool Ground(Vector3 p,out Vector3 floor){floor=new(p.X,0,p.Z);return Open(p);}
  public bool Traverse(Vector3 a,Vector3 b){for(var i=0;i<=100;i++)if(!Open(Vector3.Lerp(a,b,i/100f)))return false;return true;}
 }
}
