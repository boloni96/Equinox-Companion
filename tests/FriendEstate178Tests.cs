using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class FriendEstate178Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("178: "+label);}
  Check(FriendEstateTravelPolicy.CanInspectFc(true,"123",""),"linked FC missing estate ID may inspect menu");
  Check(FriendEstateTravelPolicy.CanInspectFc(null,"",""),"linked FC before optional data may inspect menu");
  Check(!FriendEstateTravelPolicy.CanInspectFc(false,"",""),"observed non-member excluded");
  Check(!FriendEstateTravelPolicy.CanInspectFc(true,"123","456"),"conflicting known FC excluded");
  Check(FriendEstateTravelPolicy.MatchesFriend(0,"Brielle Moonfall",22,123,"Brielle Moonfall",22),"missing content ID resolved by name and home world");
  Check(!FriendEstateTravelPolicy.MatchesFriend(0,"Brielle Moonfall",22,123,"Brielle Moonfall",33),"same name on other world excluded");
  Check(!FriendEstateTravelPolicy.MatchesFriend(456,"Brielle Moonfall",22,123,"Brielle Moonfall",22),"known content ID cannot be replaced by name");
  Check(FriendEstateTravelPolicy.MatchesRow(["Free Company Estate","Shirogane","Ward 9, Plot 19"],"Shirogane",9,19),"screenshot exact FC address");
  Check(!FriendEstateTravelPolicy.MatchesRow(["Free Company Estate","Shirogane","Ward 9, Plot 20"],"Shirogane",9,19),"other FC plot never selected");
  Console.WriteLine("PASS 178 linked FC lookup, friend identity and exact estate address");
 }
}
