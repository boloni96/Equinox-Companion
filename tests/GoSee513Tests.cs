using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class GoSee513Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool ok,string why){if(!ok)throw new Exception("GO SEE arrival: "+why);}
  var gate=Vector3.Zero;
  Check(NativeHousingPolicy.GoSeeArrival(true,false,new(8,0,0),gate,true,false),"teleport street landing finishes without collision or inward walk");
  Check(NativeHousingPolicy.GoSeeArrival(true,false,new(4,0,0),gate,false,true),"visible street approach finishes outside plot");
  Check(!NativeHousingPolicy.GoSeeArrival(true,false,new(8,0,0),gate,false,true),"walking must reach entrance approach");
  Check(!NativeHousingPolicy.GoSeeArrival(true,false,new(4,0,0),gate,false,false),"blocked street approach is not arrival");
  Check(!NativeHousingPolicy.GoSeeArrival(false,true,gate,gate,true,true),"wrong ward never completes");
  Check(!NativeHousingPolicy.GoSeeArrival(true,false,new(0,5,0),gate,true,true),"different elevation is not landing");
  Check(!NativeHousingPolicy.GoSeeArrival(true,false,new(30,0,0),gate,true,true),"remote or cancelled teleport is not arrival");
  Check(NativeHousingPolicy.GoSeeArrival(true,true,new(15,0,0),gate,false,false),"already on exact plot needs no extra walk");
  Check(!NativeHousingPolicy.GoSeeArrival(true,true,new(float.NaN,0,0),gate,true,true),"invalid position rejected");
  Console.WriteLine("513 GO SEE arrival regression checks passed.");
 }
}
