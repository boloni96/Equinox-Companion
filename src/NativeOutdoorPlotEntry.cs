using System.Numerics;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private void WalkOutdoorPlotEntry(OutdoorPlotEntry entry,DateTimeOffset now)
    {
        if(Objects.LocalPlayer is not {} player)return;
        var goal=entry.Goal??entry.Next(player.Position);
        // Prepare the following checked segment before the current one ends.
        // Keep the existing path and input lease instead of stop/start per step.
        if(entry.Steps<6&&independentPath is {Length:>1}&&independentSearch==null&&
            Vector3.Distance(independentGoal,goal)<.1f&&Vector3.Distance(player.Position,goal)<1.2f){
            var end=goal;var next=end+entry.Direction*3;
            var continuation=VerifiedWalkingOpening.ShortDirect(independentCollision,end,next);
            if(continuation.Length>1){
                independentPath=independentPath.Concat(continuation.Skip(1)).ToArray();
                goal=independentGoal=entry.Next(end);independentArrived=false;
            }
        }
        if(!IndependentWalk(goal,.3f,now,allowFlight:false,directOnly:true))return;
        entry.Goal=null;
    }
}
