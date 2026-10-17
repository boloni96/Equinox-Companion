using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class FriendEstate157Tests
{
 [ModuleInitializer]internal static void Run()
 {
  void Check(bool value,string why){if(!value)throw new Exception("Friend estate: "+why);}
  Check(FriendEstateTravelPolicy.MatchesRow(["Private Estate","The Lavender Beds","Ward 17, Plot 41","249 gil"],"Lavender Beds",17,41),"matching address");
  Check(FriendEstateTravelPolicy.MatchesRow(["Mist","17th Ward","Plot 41"],"Mist",17,41),"ordinal ward");
  Check(!FriendEstateTravelPolicy.MatchesRow(["Mist","Ward 17, Plot 41"],"Mist",17,4),"plot is exact");
  Check(!FriendEstateTravelPolicy.MatchesRow(["Mist","Ward 17, Plot 41"],"Empyreum",17,41),"district differs");
  Check(!FriendEstateTravelPolicy.MatchesRow(["Private Estate","249 gil"],"Mist",17,41),"unknown address never guessed");
  Check(!FriendEstateTravelPolicy.MatchesRow(["Mist","Ward 17, Plot 41","Ward 18"],"Mist",17,41),"ambiguous row rejected");
  Check(FriendEstateTravelPolicy.SameSession(1,2,1,2,true,false),"original session");
  Check(!FriendEstateTravelPolicy.SameSession(1,2,3,2,true,false),"character switch cancels lookup");
  Check(!FriendEstateTravelPolicy.SameSession(1,2,1,3,true,false),"world change cancels lookup");
  Check(!FriendEstateTravelPolicy.SameSession(1,2,1,2,true,true),"loading/combat cancels lookup");
  Console.WriteLine("PASS 157 friend estate exact-address selection and session cancellation");
 }
}
