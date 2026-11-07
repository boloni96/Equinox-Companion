using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class DirectApproach491Tests
{
    [ModuleInitializer] internal static void Run(){
        var target=new Vector3(30,3,0);
        foreach(var blocked in new[]{false,true}){
            var floor=new Floor(blocked);
            bool Reach(Vector3 p)=>Vector3.Distance(p,target)<=6;
            var search=new CollisionRouteSearch(floor,Vector3.Zero,target,5,arrival:Reach,streetGuide:[Vector3.Zero,new(0,0,8),new(30,0,8),target]);
            var streamed=new List<Vector3>();
            for(var i=0;i<10000&&!search.Finished;i++){
                search.Advance(100,1);
                if(search.TakeVerifiedPrefix() is {} section)streamed.AddRange(section.Skip(streamed.Count>0?1:0));
            }
            if(search.Path is not {} path||!Reach(path[^1]))throw new Exception("Direct approach or its bounded obstacle fallback must reach interaction range.");
            if(!blocked&&path.Any(p=>MathF.Abs(p.Z)>.001f))throw new Exception("Open approach followed the bent map guide instead of the straight line.");
            if(blocked&&!path.Any(p=>MathF.Abs(p.Z)>=2))throw new Exception("Blocked direct approach must find a genuine detour.");
            if(streamed.Count<4)throw new Exception("Verified direct progress must be available before the full approach finishes.");
            for(var i=1;i<path.Length;i++)if(!floor.Traverse(path[i-1],path[i]))throw new Exception("Direct approach bypassed physical collision.");
            if(blocked&&!search.DiagnosticSnapshot().RejectedConnections.Any(r=>r.Reason.Contains("test wall")))throw new Exception("Direct rejection must be exported for diagnosis.");
        }
        Console.WriteLine("491 direct-first interaction routes, streaming, obstacle fallback and rejection diagnostics passed.");
    }
    private sealed class Floor(bool blocked):IWalkingCollision {
        public string? TraversalFailure=>"test wall";
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f)return false;
            for(var i=0;i<=24;i++){
                var p=Vector3.Lerp(a,b,i/24f);
                if(blocked&&p.X>21&&p.X<23&&MathF.Abs(p.Z)<2)return false;
            }
            return true;
        }
    }
}
