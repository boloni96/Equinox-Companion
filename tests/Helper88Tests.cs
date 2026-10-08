using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper88Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 88 "+name);}
  Test("HTTP200 rejection is not delivery",FollowTravelReceipt.Describe(new(false,0,"Paused")).StartsWith("Travel not queued"));
  Test("zero recipients is not delivery",FollowTravelReceipt.Describe(new(true,0)).StartsWith("Travel not queued"));
  Test("legacy response does not claim delivery",FollowTravelReceipt.Describe(new(true)).Contains("did not confirm"));
  Test("queued is not arrival",FollowTravelReceipt.Describe(new(true,1)).Contains("not yet confirmed arrived"));
  Test("missing reply is not success",FollowTravelReceipt.Describe(null).Contains("unconfirmed"));
 }
}
