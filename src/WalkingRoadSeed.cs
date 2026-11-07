using System.Numerics;
namespace EquinoxCompanion;
// Validate the map's road line as small supported steps before expanding a grid.
// Artwork supplies X/Z only. Ground height and every connection come from geometry.
internal sealed class WalkingRoadSeed(IWalkingCollision collision,Vector3 start,IReadOnlyList<Vector3> guide)
{
    private readonly List<Vector3> points=[start];
    private int next;
    private int shortcutIndex=-1,shortcutAttempt=-1;
    private List<Vector3>? shortcut;
    private bool TryShortcut(){
        if(shortcut==null){
            if(shortcutAttempt==next)return false;
            shortcutAttempt=next;
            var current=points[^1];var arc=0f;var prior=current;var candidate=-1;
            for(var i=next;i<guide.Count&&i<=next+32;i++){
                arc+=CollisionRouteSearch.Horizontal(prior,guide[i]);prior=guide[i];
                if(arc>20)break;
                var distance=CollisionRouteSearch.Horizontal(current,guide[i]);
                if(i>next&&distance>=2&&distance<=12&&arc>distance+.5f)candidate=i;
            }
            if(candidate<0)return false;
            shortcutIndex=candidate;shortcut=[current];
        }
        var from=shortcut[^1];var delta=guide[shortcutIndex]-from;delta.Y=0;
        if(delta.Length()<.1f){
            if(points.Count+shortcut.Count-1>257){shortcut=null;return false;}
            points.AddRange(shortcut.Skip(1));next=shortcutIndex+1;shortcut=null;return true;
        }
        var probe=from+Vector3.Normalize(delta)*MathF.Min(.5f,delta.Length());
        // Commit nothing until the entire shorter connection passes the same
        // support, stair and swept-body checks as the original guide.
        if(!collision.Ground(probe,out var floor)||MathF.Abs(floor.Y-from.Y)>.45f||!collision.Traverse(from,floor)){
            shortcut=null;return false;
        }
        shortcut.Add(floor);return true;
    }
    public bool Finished { get; private set; }
    public string? StopReason { get; private set; }
    public Vector3? BlockedPoint { get; private set; }
    public Vector3[] Points=>points.ToArray();
    public int Count=>points.Count;
    public void Step(){
        if(Finished)return;
        if(next>=guide.Count||points.Count>=257){StopReason=next>=guide.Count?"Guide completed":"Step limit reached";Finished=true;return;}
        if(TryShortcut())return;
        var current=points[^1];var delta=guide[next]-current;delta.Y=0;
        if(delta.Length()<.1f){next++;return;}
        var probe=current+Vector3.Normalize(delta)*MathF.Min(.5f,delta.Length());
        if(!collision.Ground(probe,out var floor)){BlockedPoint=probe;StopReason=$"Ground unavailable at {probe}";Finished=true;return;}
        if(MathF.Abs(floor.Y-current.Y)>.45f){BlockedPoint=floor;StopReason=$"Step height rejected from {current} to {floor}";Finished=true;return;}
        if(!collision.Traverse(current,floor)){
            var rejection=collision.TraversalFailure;
            // Map centerlines are only hints. Try a fully checked side passage
            // around a railing/post before abandoning the connected street.
            if(TrySidePass(current,guide[next]))return;
            BlockedPoint=floor;StopReason=$"Connection blocked from {current} to {floor}: {rejection??"unspecified collision rejection"}";Finished=true;return;
        }
        points.Add(floor);
    }
    private bool TrySidePass(Vector3 current,Vector3 guidePoint){
        if(points.Count>255)return false;
        var delta=guidePoint-current;delta.Y=0;var distance=delta.Length();
        if(distance<.2f)return false;
        var direction=delta/distance;var side=new Vector3(-direction.Z,0,direction.X);
        foreach(var reach in (ReadOnlySpan<float>)[1f,2f]){
            var probe=current+direction*MathF.Min(reach,distance);
            if(!collision.Ground(probe,out var onward))continue;
            foreach(var width in (ReadOnlySpan<float>)[.75f,1.25f])foreach(var sign in (ReadOnlySpan<float>)[1,-1]){
                var near=Vector3.Lerp(current,onward,.5f)+side*(width*sign);
                if(!collision.Ground(near,out var bend))continue;
                if(!collision.Traverse(current,bend)||!collision.Traverse(bend,onward))continue;
                points.Add(bend);points.Add(onward);return true;
            }
        }
        return false;
    }
}
