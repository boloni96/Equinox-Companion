using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class LoginTravel514Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(LoginTravelRoute actual,LoginTravelRoute expected,string why){if(actual!=expected)throw new Exception("Login travel: "+why);}
  foreach(var stayOut in new[]{false,true}){
   Check(NativeTravelPolicy.FromCharacterList(10,10,20,1,1,2,stayOut),LoginTravelRoute.TransferDc,"home to another DC");
   Check(NativeTravelPolicy.FromCharacterList(20,10,30,2,1,3,stayOut),LoginTravelRoute.ReturnHome,"guest to third DC returns home first");
   Check(NativeTravelPolicy.FromCharacterList(20,10,10,2,1,1,stayOut),LoginTravelRoute.ReturnHome,"guest returns home");
   Check(NativeTravelPolicy.FromCharacterList(11,10,10,1,1,1,stayOut),LoginTravelRoute.ReturnHome,"same-DC home return uses character menu");
   Check(NativeTravelPolicy.FromCharacterList(10,10,10,1,1,1,stayOut),LoginTravelRoute.Unavailable,"already there");
  }
  Check(NativeTravelPolicy.FromCharacterList(10,10,11,1,1,1,false),LoginTravelRoute.LoginThenWorld,"same DC world visit logs in once");
  Check(NativeTravelPolicy.FromCharacterList(10,10,11,1,1,1,true),LoginTravelRoute.Unavailable,"no-login must not silently log in");
  Check(NativeTravelPolicy.FromCharacterList(20,10,11,2,1,1,true),LoginTravelRoute.Unavailable,"home DC non-home destination needs login");
  Check(NativeTravelPolicy.FromCharacterList(20,10,21,2,1,2,true),LoginTravelRoute.ReturnHome,"guest same DC can return then transfer without login");
  Check(NativeTravelPolicy.FromCharacterList(20,10,21,2,1,2,false),LoginTravelRoute.LoginThenWorld,"guest same DC uses in-game visit when allowed");
  Check(NativeTravelPolicy.FromCharacterList(0,10,20,1,1,2,false),LoginTravelRoute.Unavailable,"unknown character world rejected");
  Check(NativeTravelPolicy.FromCharacterList(10,10,20,0,1,2,false),LoginTravelRoute.Unavailable,"unknown DC rejected");
  Console.WriteLine("514 character-menu travel route checks passed.");
 }
}
