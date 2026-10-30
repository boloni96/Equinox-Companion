using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class AdventurePoll336Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(bool ok,string why){if(!ok)throw new Exception(why);}
  Check(AdventurePollPolicy.Seconds(false,false,false)==15,"Closed idle Adventure uses background interval");
  Check(AdventurePollPolicy.Seconds(true,false,false)==3,"Open Adventure retains responsive polling");
  Check(AdventurePollPolicy.Seconds(false,true,false)==3,"Automation polls promptly with window closed");
  Check(AdventurePollPolicy.Seconds(true,true,false)==3,"Open automation remains responsive");
  foreach(var open in new[]{false,true})foreach(var running in new[]{false,true})
   Check(AdventurePollPolicy.Seconds(open,running,true)==5,"Failures retain bounded retries");
  Console.WriteLine("PASS Adventure background polling");
 }
}
