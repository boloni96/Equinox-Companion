using System.Numerics;
namespace EquinoxCompanion;
internal static class WalkingBacktrack
{
    // Only reverse the observed trail, checking every connector against current geometry.
    public static Vector3[] Build(Vector3 current,IReadOnlyList<Vector3> trail,Func<Vector3,Vector3,bool> clear)
    {
        var result=new List<Vector3>{current};var travelled=0f;
        for(var i=trail.Count-1;i>=0;i--){
            var last=result[^1];var point=trail[i];var length=Vector3.Distance(last,point);
            if(length<.3f)continue;
            if(length>2.6f||!clear(last,point))break;
            result.Add(point);travelled+=length;
            if(travelled>=3||result.Count>=8)break;
        }
        return travelled>=.75f?result.ToArray():[];
    }
}
internal sealed class RecentWalkingBlocks
{
    private sealed record Block(uint Territory,uint World,Vector3 Point,DateTimeOffset At);
    private readonly List<Block> entries=[];
    public void Add(uint territory,uint world,Vector3 point,DateTimeOffset now){
        entries.RemoveAll(x=>now-x.At>TimeSpan.FromMinutes(5)||x.Territory==territory&&x.World==world&&Vector3.DistanceSquared(x.Point,point)<.25f);
        entries.Add(new(territory,world,point,now));if(entries.Count>32)entries.RemoveAt(0);
    }
    public Vector3[] Get(uint territory,uint world,DateTimeOffset now){
        entries.RemoveAll(x=>now-x.At>TimeSpan.FromMinutes(5));
        return entries.Where(x=>x.Territory==territory&&x.World==world).Select(x=>x.Point).ToArray();
    }
}
