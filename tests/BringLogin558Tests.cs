using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class BringLogin558Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool yes,string why){if(!yes)throw new Exception("558 "+why);}
  // Rafflesia/Dynamis home, visiting Primal or Crystal: list and in-game world differ.
  foreach(var visitedDc in new uint[]{2,3}){
   Check(CharacterLoginWorldPolicy.ListWorld(410,90,1,visitedDc,2,2)==410,"NA fallback chose visited world");
   Check(CharacterLoginWorldPolicy.MatchesDisplayedDc(1,1,visitedDc,2,2),"Bring rejected home list for NA visitor");
   Check(!CharacterLoginWorldPolicy.MatchesDisplayedDc(visitedDc,1,visitedDc,2,2),"Bring accepted visited DC list for NA visitor");
   Check(NativeTravelPolicy.UsesWorldVisit(visitedDc,1,2,2),"NA Bring attempted lobby transfer");
  }
  Check(CharacterLoginWorldPolicy.ListWorld(410,22,1,4,2,4)==22,"Materia visitor lost visiting list");
  Check(CharacterLoginWorldPolicy.MatchesDisplayedDc(4,1,4,2,4),"Materia visiting list rejected");
  Check(!CharacterLoginWorldPolicy.MatchesDisplayedDc(1,1,4,2,4),"Materia visit incorrectly accepted home list");
  Check(!NativeTravelPolicy.UsesWorldVisit(4,1,4,2),"Materia return changed to regional visit");
  Check(!CharacterLoginWorldPolicy.MatchesDisplayedDc(0,1,2,2,2)&&!CharacterLoginWorldPolicy.MatchesDisplayedDc(1,1,0,2,2),"unknown list accepted");
  Check(CharacterLoginWorldPolicy.ListWorld(410,90,1,1,2,2)==410,"same DC visit lost home list");
  Console.WriteLine("558 Bring home-list verification, fallback selection, NA visitors and separate Materia routing passed.");
 }
}
