using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using GameFramework=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe void RecordNavigationObstruction(Vector3 position,Vector3 target,Vector3 blocked,bool stalled)
    {
        // Rare, bounded diagnostic sampling. Broader filters are evidence only,
        // never permission to walk or a replacement for navigation collision checks.
        var probes=new List<object>();
        var framework=GameFramework.Instance();var module=framework==null?null:framework->BGCollisionModule;
        if(stalled&&module!=null&&!module->ShuttingDown&&module->LoadInProgressCounter==0){
            int* flags=stackalloc int[4]{0x7000,0,0,0};
            int* movementFlags=stackalloc int[4]{WalkingCollisionFilter.Movement,0,0,0};
            for(var i=0;i<8;i++)foreach(var height in (ReadOnlySpan<float>)[.25f,.65f,1.5f]){
                var origin=position+new Vector3(0,height,0);var angle=i*MathF.PI/4;
                var direction=new Vector3(MathF.Cos(angle),0,MathF.Sin(angle));
                var normalHit=BGCollisionModule.RaycastMaterialFilter(origin,direction,out var normal,3);
                var movement=default(RaycastHit);
                var movementHit=module->RaycastMaterialFilter(&movement,&origin,&direction,3,1,movementFlags);
                var broader=default(RaycastHit);
                var broaderHit=module->RaycastMaterialFilter(&broader,&origin,&direction,3,1,flags);
                probes.Add(new{height,direction=FollowTravelPosition.From(direction),normalHit,
                    normalPoint=normalHit?FollowTravelPosition.From(normal.Point):null,normalMaterial=normalHit?normal.Material.ToString("X"):null,
                    movementHit,movementPoint=movementHit?FollowTravelPosition.From(movement.Point):null,movementMaterial=movementHit?movement.Material.ToString("X"):null,
                    broaderHit,broaderPoint=broaderHit?FollowTravelPosition.From(broader.Point):null,broaderMaterial=broaderHit?broader.Material.ToString("X"):null});
            }
        }
        RecordTravelPrompt("Navigation obstruction",new{run=independentNavigationRun,position=FollowTravelPosition.From(position),target=FollowTravelPosition.From(target),blocked=FollowTravelPosition.From(blocked),stalled,
            map=independentRoadMapId,mapAvailable=independentRoadGuide!=null,roadPenalty=independentRoadGuide?.Penalty(position),
            waypoint=independentWaypoint,path=independentPath?.Skip(Math.Max(0,independentWaypoint-4)).Take(20).Select(FollowTravelPosition.From).ToArray(),
            travelled=independentWalkedPoints.TakeLast(16).Select(FollowTravelPosition.From).ToArray(),probes});
    }
}
