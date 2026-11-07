using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Streaming488Tests
{
    [ModuleInitializer] internal static void Run(){
        foreach(var sign in new[]{-1,1}){
            var collision=new Corridor(sign);var search=new CollisionRouteSearch(collision,Vector3.Zero,new(20,0,0),.3f,bidirectional:false);
            var streamed=new List<Vector3>();var beforeCompletion=false;
            for(var i=0;i<20000&&!search.Finished;i++){
                search.Advance(100,1);
                if(search.TakeVerifiedPrefix() is not {} prefix)continue;
                beforeCompletion|=!search.Finished;
                if(streamed.Count>0&&Vector3.Distance(streamed[^1],prefix[0])>.001f)throw new Exception("Streamed routes must connect without returning to the original start.");
                streamed.AddRange(prefix.Skip(streamed.Count==0?0:1));
                for(var j=1;j<prefix.Length;j++)if(!collision.Traverse(prefix[j-1],prefix[j]))throw new Exception("Streamed an unchecked obstacle crossing.");
            }
            if(!beforeCompletion||streamed.Count<3||search.Path is not {} path)throw new Exception("Geometry searches must stream a safe section before completing, then find the destination.");
            if(path.Length<streamed.Count||!path.Take(streamed.Count).SequenceEqual(streamed))throw new Exception("Final route abandoned the streamed ancestry.");
            for(var j=1;j<path.Length;j++)if(!collision.Traverse(path[j-1],path[j]))throw new Exception("Final route crosses a wall.");
        }
        var t=new NavigationPhaseTimer();var start=DateTimeOffset.UnixEpoch;t.Begin(start);
        t.SetPlanning(false,start.AddSeconds(41));
        if(t.Expired(start.AddSeconds(46)))throw new Exception("Planning time must not consume the walking budget.");
        t.Progress(start.AddSeconds(100));
        if(t.Expired(start.AddSeconds(150))||!t.Expired(start.AddSeconds(191)))throw new Exception("Real movement refreshes the walking deadline, but idle waiting does not.");
        var waiting=new NavigationPhaseTimer();waiting.Begin(start);waiting.Progress(start.AddSeconds(89));
        if(!waiting.Expired(start.AddSeconds(91)))throw new Exception("Planning must remain bounded without a verified section.");
        Console.WriteLine("488 streamed geometry sections, ancestry, mirrored obstacles and progress deadlines passed.");
    }
    private sealed class Corridor(int sign):IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f&&p.X>=-3&&p.X<=24&&MathF.Abs(p.Z)<=9;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f)return false;
            for(var i=0;i<=24;i++){
                var p=Vector3.Lerp(a,b,i/24f);
                if(!Ground(p,out _)||p.X>8&&p.X<10&&p.Z*sign<3)return false;
            }
            return true;
        }
    }
}
