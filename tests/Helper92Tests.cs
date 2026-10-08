using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper92Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 92 "+name);}
  Test("active following green",HelperStatusPolicy.Tone("FOLLOWING — Companion")=="working");
  Test("paused yellow",HelperStatusPolicy.Tone("Paused by leader")=="waiting");
  Test("loading yellow",HelperStatusPolicy.Tone("LOADING")=="waiting");
  Test("stopping yellow",HelperStatusPolicy.Tone("STOPPING")=="waiting");
  Test("stopped grey",HelperStatusPolicy.Tone("STOPPED")=="inactive");
  Test("stale data not green",HelperStatusPolicy.Tone("Following",stale:true)=="inactive");
  Test("quest failure retained over resumed movement",HelperStatusPolicy.Tone("Following · Quest Helper blocked: quest unavailable")=="blocked");
  Test("transport error red",HelperStatusPolicy.Tone("HTTP 429 failed")=="error");
  Test("negative send not green",HelperStatusPolicy.Tone("No meeting request sent.",notice:true)=="blocked");
  Test("explicit stopped beats old issue",HelperStatusPolicy.Tone("Quest Helper blocked",stopped:true)=="inactive");
  Test("leader shows highest severity",HelperStatusPolicy.Combine(["working","waiting","error"])=="error");
  Test("leader mixed working blocked orange",HelperStatusPolicy.Combine(["working","blocked"])=="blocked");
 }
}
