using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class BringTravel512Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool ok,string why){if(!ok)throw new Exception("Bring travel: "+why);}
  Check(!NativeTravelPolicy.TransferBeforeLogin(10,10,1,1),"already at destination logs in once");
  Check(!NativeTravelPolicy.TransferBeforeLogin(10,11,1,1),"same DC needs in-game World Visit");
  Check(NativeTravelPolicy.TransferBeforeLogin(10,20,1,2),"different DC transfers before login");
  foreach(var source in new uint[]{0,10})foreach(var destination in new uint[]{0,20})
   if(source==0||destination==0)Check(!NativeTravelPolicy.TransferBeforeLogin(source,destination,1,2),"unknown world does not start transfer");
  Check(!NativeTravelPolicy.TransferBeforeLogin(10,20,0,2)&&!NativeTravelPolicy.TransferBeforeLogin(10,20,1,0),"unknown DC does not start transfer");
  Check(!NativeTravelPolicy.ReturnFirst(10,10,1,2),"home character transfers directly");
  Check(NativeTravelPolicy.ReturnFirst(20,10,2,3),"visiting character returns home first");
  Check(NativeTravelPolicy.ContinueDcFromHome(true,10,10,30,1,3),"home return continues to final DC without login");
  Check(!NativeTravelPolicy.ContinueDcFromHome(true,10,10,11,1,1),"home DC destination needs World Visit after login");
  Check(!NativeTravelPolicy.ContinueDcFromHome(true,10,10,10,1,1),"home is final destination: login there");
  Console.WriteLine("512 destination-before-login regression checks passed.");
 }
}
