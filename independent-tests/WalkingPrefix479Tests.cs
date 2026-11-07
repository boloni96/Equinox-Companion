using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingPrefix479Tests
{
    [ModuleInitializer] internal static void Run(){
        var goblet=NavigationStreetCorridors.Guide(341,new(159.14963f,-30,-32.31259f),new(112.62928f,-14.994898f,-24.354433f));
        if(goblet==null||!goblet.Any(p=>p.X<130&&p.Z<-36))throw new Exception("Goblet 22 needs the actual stair turn, not the map shortcut along the retaining wall.");
        var shiro=NavigationStreetCorridors.Guide(641,new(100.159904f,30.07f,24.50072f),new(78.73712f,22.02f,47.635876f));
        if(shiro==null||!shiro.Any(p=>p.X<87&&p.Z<=20))throw new Exception("Shirogane 11 must approach the street's stair corner.");
        if(NavigationStreetCorridors.Guide(341,new(159,-30,-32),new(200,-14,-24))!=null)throw new Exception("A different plot must not inherit this street hint.");
        var geometry=new Corridor();
        Vector3[] guide=[new(0,0,12),new(12,0,12),new(12,0,0)];
        var search=new CollisionRouteSearch(geometry,Vector3.Zero,new(12,0,0),.25f,streetGuide:guide);
        Vector3[]? prefix=null;
        for(var i=0;i<300&&prefix==null&&!search.Finished;i++){
            search.Advance(100,1);prefix=search.TakeVerifiedPrefix();
        }
        if(prefix==null||prefix.Length<20||search.Finished)throw new Exception("A verified road section must become available before full route completion.");
        if(search.TakeVerifiedPrefix()!=null)throw new Exception("Transfer a verified prefix only once.");
        for(var i=1;i<prefix.Length;i++)if(!geometry.Traverse(prefix[i-1],prefix[i]))throw new Exception("Prefix contains an unchecked connection.");
        for(var i=0;i<500&&!search.Finished;i++)search.Advance(100,100);
        if(search.Path==null)throw new Exception("Planner must finish from the committed road endpoint.");
        var joined=WalkingSteering.JoinContinuation(prefix,search.Path);
        if(joined==null||joined.Length!=search.Path.Length)throw new Exception("Full route must join an already walking prefix without repeating it.");
        var opening=new[]{new Vector3(-1,0,0)}.Concat(prefix).ToArray();
        joined=WalkingSteering.JoinContinuation(opening,search.Path);
        if(joined==null||joined.Length!=search.Path.Length+1)throw new Exception("Previously walked opening must remain before the shared prefix.");
        var wrong=prefix.ToArray();wrong[2]+=Vector3.UnitY;
        if(WalkingSteering.JoinContinuation(wrong,search.Path)!=null)throw new Exception("Mismatched floor prefix cannot merge.");
        foreach(var sign in new[]{-1,1}){
            var post=new Post{AllowedSide=sign};var seed=new WalkingRoadSeed(post,Vector3.Zero,[new(0,0,8)]);
            for(var i=0;i<100&&!seed.Finished;i++)seed.Step();
            if(seed.Points[^1].Z<7.9f||!seed.Points.Any(p=>p.X*sign>.1f))throw new Exception("Road seed must pass a small obstacle on its clear side.");
            for(var i=1;i<seed.Points.Length;i++)if(!post.Traverse(seed.Points[i-1],seed.Points[i]))throw new Exception("A side passage skipped physical validation.");
        }
        var closed=new WalkingRoadSeed(new Post{AllowedSide=0},Vector3.Zero,[new(0,0,8)]);
        for(var i=0;i<100&&!closed.Finished;i++)closed.Step();
        if(closed.Points[^1].Z>=.5f)throw new Exception("Blocked street must not produce an unsafe prefix.");
        Console.WriteLine($"479 verified road prefix streaming and continuous full-path handoff passed ({prefix.Length} samples; {search.Expanded} expansions).");
    }
    private sealed class Post:IWalkingCollision {
        public int AllowedSide;
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f||a.X*AllowedSide<-.01f||b.X*AllowedSide<-.01f)return false;
            if(AllowedSide==0)return a.Z<.5f&&b.Z<.5f;
            var edge=b-a;var obstacle=new Vector3(0,0,.5f);
            var t=edge.LengthSquared()<.0001f?0:Math.Clamp(Vector3.Dot(obstacle-a,edge)/edge.LengthSquared(),0,1);
            return Vector3.Distance(obstacle,a+edge*t)>.25f;
        }
    }
    private sealed class Corridor:IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f&&p.X>=0&&p.X<=12&&p.Z>=0&&p.Z<=12;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f)return false;
            // The last guide section is obstructed. The prefix must end before it.
            if((a.Z>=5)!=(b.Z>=5)){
                var crossing=Vector3.Lerp(a,b,(5-a.Z)/(b.Z-a.Z));
                if(crossing.X>=10)return false;
            }
            return Ground(a,out _)&&Ground(b,out _);
        }
    }
}
