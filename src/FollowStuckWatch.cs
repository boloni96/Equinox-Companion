using System.Numerics;
namespace EquinoxCompanion;
public sealed class FollowStuckWatch
{
    private Vector3 anchor;
    private DateTimeOffset? lastProgress;
    private Vector3? waitingLeader;
    private float? waitingDistance;
    private bool lostLeader;
    public bool WaitingForPickup {get;private set;}
    public void Reset(){lastProgress=null;WaitingForPickup=false;waitingLeader=null;waitingDistance=null;lostLeader=false;}
    public void Pause(){lastProgress=null;}
    public bool Observe(DateTimeOffset now,Vector3 self,float? distance,bool resume,int seconds,Vector3? leader=null)
    {
        if(WaitingForPickup){
            if(distance==null){lostLeader=true;return true;}
            var moved=leader is {} current&&waitingLeader is {} previous?Vector3.DistanceSquared(current,previous)>.25f:waitingDistance is {} old&&Math.Abs(distance.Value-old)>.5f;
            if(resume&&(lostLeader||moved)){Reset();return false;}return true;
        }
        if(distance is null or <=3){lastProgress=null;return false;}
        if(lastProgress==null||Vector3.DistanceSquared(self,anchor)>=.25f){anchor=self;lastProgress=now;return false;}
        if(now-lastProgress>=TimeSpan.FromSeconds(Math.Clamp(seconds,5,600))){WaitingForPickup=true;waitingLeader=leader;waitingDistance=distance;return true;}
        return false;
    }
}
