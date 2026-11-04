using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class DataCenter338Tests
{
 [ModuleInitializer] internal static void Run()
 {
  void Check(bool ok,string why){if(!ok)throw new Exception(why);}
  foreach(var relationship in new[]{CharacterLoginRelationship.SameServiceAccount,CharacterLoginRelationship.SameSquareEnixLogin})
   foreach(var known in new[]{true,false})
    Check(CharacterLoginPolicy.IdentifyThroughDataCenter(CharacterLoginMode.Title,
     CharacterLoginPolicy.InitialAction(CharacterLoginMode.Title,relationship,known)),"Verified title login must open Data Center");
  Check(CharacterLoginPolicy.IdentifyThroughDataCenter(CharacterLoginMode.Title,
   CharacterLoginPolicy.InitialAction(CharacterLoginMode.Title,CharacterLoginRelationship.Unverified,false)),"Unknown title login identifies through Data Center");
  Check(!CharacterLoginPolicy.IdentifyThroughDataCenter(CharacterLoginMode.Title,CharacterLoginAction.Confirm),"Do not bypass confirmation");
  Check(!CharacterLoginPolicy.IdentifyThroughDataCenter(CharacterLoginMode.CharacterSelect,CharacterLoginAction.Proceed),"Verified ready list must log in without looping to title");
  Check(!CharacterLoginPolicy.IdentifyThroughDataCenter(CharacterLoginMode.InGame,CharacterLoginAction.Proceed),"Do not open title while logged in");
  Check(CharacterLoginPolicy.NeedsListRouting(0,1,true,1),"ALT slot cannot use MAIN list even with stale target entry");
  Check(CharacterLoginPolicy.NeedsListRouting(1,1,true,0),"Missing target routes account/world before login");
  Check(CharacterLoginPolicy.NeedsListRouting(1,1,false,1),"Loading lists cannot authorize login");
  Check(CharacterLoginPolicy.NeedsListRouting(1,1,true,2),"Ambiguous matches cannot authorize login");
  Check(!CharacterLoginPolicy.NeedsListRouting(1,1,true,1),"Verified exact ALT list does not loop back to title");
  Check(!CharacterLoginPolicy.NeedsListRouting(1,-1,true,1),"Exact live character can verify previously unknown slot");
  Check(CurrentLoginIdentification.WorldSettled(TimeSpan.FromMilliseconds(500),true),"Ready populated world advances after half a second");
  Check(!CurrentLoginIdentification.WorldSettled(TimeSpan.FromMilliseconds(499),true),"Populated world still gets a stable sample");
  Check(!CurrentLoginIdentification.WorldSettled(TimeSpan.FromMilliseconds(500),false),"Empty/loading world is not skipped on fast timing");
  Check(CurrentLoginIdentification.WorldSettled(TimeSpan.FromSeconds(2),false),"Settled empty world can advance");
  Console.WriteLine("PASS Data Center routing for known and unknown title accounts");
 }
}
