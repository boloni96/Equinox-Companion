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
    private static float HorizontalDistanceSquared(Vector3 a,Vector3 b)=>(a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z);
    public void Reset(){lastProgress=null;WaitingForPickup=false;waitingLeader=null;waitingDistance=null;lostLeader=false;}
    public void Pause(){lastProgress=null;}
    public bool Observe(DateTimeOffset now,Vector3 self,float? distance,bool resume,int seconds,Vector3? leader=null)
    {
        if(WaitingForPickup){
            if(distance==null){lostLeader=true;return true;}
            var moved=leader is {} current&&waitingLeader is {} previous?Vector3.DistanceSquared(current,previous)>.25f:waitingDistance is {} old&&distance.Value<old-.5f;
            if(resume&&(distance<=3||lostLeader&&distance<=10||moved&&waitingDistance is {} priorDistance&&distance.Value<priorDistance-.5f)){Reset();return false;}return true;
        }
        if(distance is null or <=3){lastProgress=null;return false;}
        if(lastProgress==null||HorizontalDistanceSquared(self,anchor)>=.25f){anchor=self;lastProgress=now;return false;}
        if(now-lastProgress>=TimeSpan.FromSeconds(Math.Clamp(seconds,5,600))){WaitingForPickup=true;waitingLeader=leader;waitingDistance=distance;return true;}
        return false;
    }
}
