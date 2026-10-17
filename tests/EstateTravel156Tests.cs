using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class EstateTravel156Tests
{
 [ModuleInitializer]internal static void Run()
 {
  var estate=new Address("123",1,339,17,41,0,false,false,"Rafflesia","The Mist");
  void Check(bool value,string why){if(!value)throw new Exception("Estate travel: "+why);}
  Check(CharacterTravelPolicy.MatchesEstate(estate,1,"Mist",17,41),"exact current estate and district alias");
  Check(!CharacterTravelPolicy.MatchesEstate(estate,2,"Mist",17,41),"same address on another world");
  Check(!CharacterTravelPolicy.MatchesEstate(estate,1,"Empyreum",17,41),"same ward and plot in another district");
  Check(!CharacterTravelPolicy.MatchesEstate(estate,1,"Mist",17,42),"neighbour plot");
  Check(!CharacterTravelPolicy.MatchesEstate(estate with {Apartment=true},1,"Mist",17,41),"apartments are separate");
  Check(!CharacterTravelPolicy.MatchesEstate(null,1,"Mist",17,41),"unknown ownership uses address travel");
  Console.WriteLine("PASS 156 exact estate address and private/FC routing guards");
 }
}
