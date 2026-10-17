using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class CharacterTravel151Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool value,string why){if(!value)throw new Exception("Character travel: "+why);}
  Check(CharacterTravelPolicy.ValidTarget("Leonis Verelle","Rafflesia"),"normal identity");
  Check(CharacterTravelPolicy.ValidTarget("Afahokn Sohr-aen","Phoenix"),"hyphenated identity");
  Check(!CharacterTravelPolicy.ValidTarget("~ Leonis Verelle","Rafflesia"),"decorated names must use observed identity");
  Check(!CharacterTravelPolicy.ValidTarget("Leonis Verelle@Other","Rafflesia"),"reject command separator");
  Check(!CharacterTravelPolicy.ValidTarget("Leonis Verelle","Rafflesia extra"),"reject command destination injection");
  Check(!CharacterTravelPolicy.NeedsConfirmation("p","a","p","a"),"same Person/account direct switch");
  Check(CharacterTravelPolicy.NeedsConfirmation("p","a","p2","a"),"different Person asks");
  Check(CharacterTravelPolicy.NeedsConfirmation("p","a","p","a2"),"different account asks");
  Check(CharacterTravelPolicy.NeedsConfirmation("","","p","a"),"unknown current identity asks");
  var c=new SharedCharacter("c","Leonis Verelle","Rafflesia","Dynamis","NA","Main",[]);
  var actor=new Actor("1","Leonis Verelle",1,2,"Rafflesia","Coeurl");
  Check(CharacterTravelPolicy.IsCurrent(c,actor),"visiting world still same character");
  Check(!CharacterTravelPolicy.IsCurrent(c with{ContentId="2"},actor),"distinct content ID wins over names");
 }
}
