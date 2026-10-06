using System.Numerics;
namespace EquinoxCompanion;
public sealed class FollowRecoveryWatch
{
    private Vector3 anchor;
    private DateTimeOffset? progress;
    private int retries;
    public bool AwaitingMovement { get; private set; }
    public void Reset(){progress=null;retries=0;AwaitingMovement=false;}
    public bool Retry(DateTimeOffset now,Vector3 self,float distance)
    {
        if(distance<=3){Reset();return false;}
        if(progress==null||Vector3.DistanceSquared(anchor,self)>.25f){anchor=self;progress=now;retries=0;AwaitingMovement=false;return false;}
        if(now-progress>=TimeSpan.FromSeconds(2))AwaitingMovement=true;
        if(retries>=3||now-progress<TimeSpan.FromSeconds(4))return false;
        retries++;progress=now;return true;
    }
}
