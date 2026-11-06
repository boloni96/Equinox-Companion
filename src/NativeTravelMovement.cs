using System.Numerics;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nativeMovementDiagnosticAt;
    private bool NativeTravelMovementReady()
    {
        if(!followStopPending&&!followStopUnconfirmed&&!nativeFollowRequested)return true;
        if(nativeFollowRequested&&!followStopPending&&!followStopUnconfirmed)RequestFollowMovementStop();
        nativeTravelStatus=nativeAethernetStatus="Waiting for game follow to stop before walking";
        RecordNativeMovementWait("Game follow still owns movement",new{followStopPending,followStopUnconfirmed,nativeFollowRequested});
        return false;
    }
    private bool TryNativeTravelGround(Vector3 approach,Vector3 player,Vector3 crystal,out Vector3 floor)
    {
        // Object origins can float above the floor. Ground() only probes within
        // 0.7 yalms of the supplied height; start at the character's floor height.
        // Retain a planned goal so approaching does not continually restart it.
        if((independentSearch!=null||independentPath!=null)&&Vector3.DistanceSquared(independentGoal,approach)<36){floor=independentGoal;return true;}
        if(NativeAetheryteApproach.Ground(independentCollision,approach,player,out floor))return true;
        RecordNativeMovementWait("No supported aetheryte approach",new{player=new{x=player.X,y=player.Y,z=player.Z},crystal=new{x=crystal.X,y=crystal.Y,z=crystal.Z},approach=new{x=approach.X,y=approach.Y,z=approach.Z},territory=Client.TerritoryType});
        return false;
    }
    private void RecordNativeMovementWait(string reason,object detail)
    {
        var now=DateTimeOffset.UtcNow;if(now<nativeMovementDiagnosticAt)return;
        nativeMovementDiagnosticAt=now.AddSeconds(5);
        RecordTravelPrompt("Walking waiting",new{reason,detail});
    }
}
