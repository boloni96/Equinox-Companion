using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool workshopWalking;
    private unsafe bool ApproachWorkshopTarget(IGameObject target,float range,DateTimeOffset now)
    {
        var self=Objects.LocalPlayer;if(self==null)return false;
        if(Vector3.DistanceSquared(self.Position,target.Position)<=range*range){
            if(workshopWalking)StopIndependentWalk("Workshop target reached");
            workshopWalking=false;return true;
        }
        if(!workshopWalking){workshopWalking=true;workshopDeadline=now.AddMinutes(2);}
        // All three workshop objects share the interior. Use collision geometry,
        // not outdoor map roads or housing ward routes, and stop beside the object.
        IndependentWalk(target.Position,range,now,allowFlight:false,geometryOnly:true,objectRangeArrival:true);
        workshopStatus="Walking to "+target.Name.TextValue+" · "+independentWalkStatus;
        return false;
    }
}
