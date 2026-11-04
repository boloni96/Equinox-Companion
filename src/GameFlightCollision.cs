using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
namespace EquinoxCompanion;
internal sealed class GameFlightCollision:IFlightCollision
{
    private static readonly Vector3[] offsets=[new(0,.3f,0),new(0,1.3f,0),new(0,2.5f,0),new(.8f,1.3f,0),new(-.8f,1.3f,0),new(0,1.3f,.8f),new(0,1.3f,-.8f)];
    public bool Clear(Vector3 from,Vector3 to){
        if(!GameWalkingCollision.Available())return false;
        var delta=to-from;var length=delta.Length();if(!float.IsFinite(length)||length>8)return false;
        // Sweep multiple body-height/width rays, including endpoint cross-sections.
        foreach(var offset in offsets){
            if(length>.01f&&BGCollisionModule.RaycastMaterialFilter(from+offset,delta/length,out _,length))return false;
        }
        foreach(var point in new[]{from,to})foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitZ})
            if(BGCollisionModule.RaycastMaterialFilter(point+Vector3.UnitY*1.3f-axis*.8f,axis,out _,1.6f))return false;
        return true;
    }
}
