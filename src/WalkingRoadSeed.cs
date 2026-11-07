using System.Numerics;
namespace EquinoxCompanion;
// Validate the map's road line as small supported steps before expanding a grid.
// Artwork supplies X/Z only. Ground height and every connection come from geometry.
internal sealed class WalkingRoadSeed(IWalkingCollision collision,Vector3 start,IReadOnlyList<Vector3> guide)
{
    private readonly List<Vector3> points=[start];
    private int next;
    public bool Finished { get; private set; }
    public Vector3[] Points=>points.ToArray();
    public int Count=>points.Count;
    public void Step(){
        if(Finished)return;
        if(next>=guide.Count||points.Count>=129){Finished=true;return;}
        var current=points[^1];var delta=guide[next]-current;delta.Y=0;
        if(delta.Length()<.1f){next++;return;}
        var probe=current+Vector3.Normalize(delta)*MathF.Min(.5f,delta.Length());
        if(!collision.Ground(probe,out var floor)||MathF.Abs(floor.Y-current.Y)>.45f||!collision.Traverse(current,floor)){
            Finished=true;return;
        }
        points.Add(floor);
    }
}
