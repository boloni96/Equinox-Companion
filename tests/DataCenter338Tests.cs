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
  Console.WriteLine("PASS Data Center routing for known and unknown title accounts");
 }
}
