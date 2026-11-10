using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class RegionTravel520Tests
{
 [ModuleInitializer] internal static void Run(){
  for(uint region=1;region<=4;region++){
   if(!NativeTravelPolicy.UsesWorldVisit(10,11,region,region))throw new Exception("Regional travel must use aetheryte World Visit");
   for(uint other=1;other<=4;other++)if(other!=region&&NativeTravelPolicy.UsesWorldVisit(10,11,region,other))throw new Exception("Cross-region transfer must retain lobby travel");
  }
  if(NativeTravelPolicy.UsesWorldVisit(0,11,2,2)||NativeTravelPolicy.UsesWorldVisit(10,11,0,0)||!NativeTravelPolicy.UsesWorldVisit(10,10,0,0))throw new Exception("Unknown regions must not expand travel eligibility");
  if(NativeTravelPolicy.RegionReachable(2,3)||!NativeTravelPolicy.RegionReachable(2,4)||NativeTravelPolicy.RegionReachable(4,2))throw new Exception("Regional routing must preserve home-region restrictions");
  if(NativeTravelPolicy.ExactChoice(new[]{"Aether","Primal","Crystal","Dynamis"},"Dynamis")!=3||NativeTravelPolicy.ExactChoice(new[]{"Dynamis","Dynamis"},"Dynamis")!=-1||NativeTravelPolicy.ExactChoice(new[]{"Aether"},"Dynamis")!=-1)throw new Exception("DC selector must match exactly and uniquely");
  if(NativeTravelPolicy.FromCharacterList(1,2,3,10,11,12,false,2,2,2)!=LoginTravelRoute.LoginThenWorld||NativeTravelPolicy.FromCharacterList(1,2,3,10,11,12,true,2,2,2)!=LoginTravelRoute.Unavailable||NativeTravelPolicy.FromCharacterList(1,2,3,40,11,12,true,4,2,2)!=LoginTravelRoute.Unavailable)throw new Exception("Character menu must not attempt removed regional DC transfers");
  Console.WriteLine("520 same-region routing, cross-region bounds and exact menu selection passed.");
 }
}
