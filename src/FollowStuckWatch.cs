using System.Numerics;
namespace EquinoxCompanion;
public sealed class FollowStuckWatch
{
    private Vector3 anchor;
    private DateTimeOffset? lastProgress;
    public bool WaitingForPickup {get;private set;}
    public void Reset(){lastProgress=null;WaitingForPickup=false;}
    public void Pause(){lastProgress=null;}
    public bool Observe(DateTimeOffset now,Vector3 self,float? distance,bool resume,int seconds)
    {
        if(WaitingForPickup){if(resume&&distance is <=3){Reset();return false;}return true;}
        if(distance is null or <=3){lastProgress=null;return false;}
        if(lastProgress==null||Vector3.DistanceSquared(self,anchor)>=.25f){anchor=self;lastProgress=now;return false;}
        if(now-lastProgress>=TimeSpan.FromSeconds(Math.Clamp(seconds,5,600))){WaitingForPickup=true;return true;}
        return false;
    }
}
