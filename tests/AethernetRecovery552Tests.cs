using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class AethernetRecovery552Tests
{
    [ModuleInitializer] internal static void Run(){
        const string failure="No connected walkable route found.";
        if(!AethernetRecoveryPolicy.ReturnToMain(false,0,failure))throw new Exception("Reported nearby shard failure must retain destination and fall back");
        if(AethernetRecoveryPolicy.ReturnToMain(true,0,failure))throw new Exception("Main crystal fallback must not repeat");
        if(AethernetRecoveryPolicy.ReturnToMain(false,2,failure))throw new Exception("Menu failure must not start unrelated teleport");
        if(AethernetRecoveryPolicy.ReturnToMain(false,0,"Stopped")||AethernetRecoveryPolicy.ReturnToMain(false,0,"Game ground-movement checks are unavailable for this version."))throw new Exception("Cancellation or unsupported native state must not trigger travel");
    }
}
