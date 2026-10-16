using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Bid144Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("144: "+label);}
  var s=new BidWorldSession();var t=DateTimeOffset.UtcNow;
  Check(s.Observe(true,1,86,t)==(true,false,0u),"wait for stable initial world");
  Check(s.Observe(true,1,86,t.AddSeconds(1))==(true,true,86u),"initial world ready");
  Check(!s.Observe(true,1,86,t.AddSeconds(2)).Changed,"no repetitive refresh");
  Check(s.Observe(false,0,0,t.AddSeconds(3))==(true,false,0u),"logout invalidates old results even without drawing");
  Check(!s.Observe(true,2,0,t.AddSeconds(4)).Ready,"missing world not committed");
  s.Observe(true,2,86,t.AddSeconds(5));Check(!s.Observe(true,2,87,t.AddSeconds(5.5)).Ready,"late world replaces candidate");
  Check(s.Observe(true,2,87,t.AddSeconds(6.5))==(true,true,87u),"new character correct world");
  Check(!s.Observe(true,3,87,t.AddSeconds(7)).Ready,"same-world different character refreshes");
  s.Observe(true,3,87,t.AddSeconds(8));Check(!s.Observe(true,3,88,t.AddSeconds(9)).Ready,"world visit refreshes");
  Check(s.Observe(true,3,88,t.AddSeconds(10))==(true,true,88u),"world visit settles");
  Console.WriteLine("PASS 144 character switches, missing/late world, same-world login and world visits");
 }
}
