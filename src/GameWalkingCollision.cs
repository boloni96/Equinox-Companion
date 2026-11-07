using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using GameFramework=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
namespace EquinoxCompanion;

// All native queries run on the framework thread. No native pointers enter a worker.
internal sealed class GameWalkingCollision:IWalkingCollision
{
    private readonly Dictionary<Vector3,(bool Found,Vector3 Ground)>? groundCache;
    private readonly NavigationRoadMap? roadGuide;
    private readonly Dictionary<Vector3,(bool Found,Vector3 Ground)> liveSweepGround=[];
    private bool samplingLiveSweep;
    private bool liveDecision;
    private readonly Dictionary<(Vector3,Vector3),(bool Clear,Vector3? Blocked,string? Failure)> decisionSweeps=[];
    internal void BeginLiveDecision(){liveDecision=true;decisionSweeps.Clear();liveSweepGround.Clear();}
    internal void EndLiveDecision(){liveDecision=false;decisionSweeps.Clear();liveSweepGround.Clear();}

    private readonly WalkingTraversalCache traversalCache=new();
    internal GameWalkingCollision(bool cacheGround=false,NavigationRoadMap? roadGuide=null){this.roadGuide=roadGuide;if(cacheGround)groundCache=new();}
    public float RoutePenalty(Vector3 point)=>roadGuide?.Penalty(point)??0;
    private const float Radius=.6f,Height=1.85f;
    private readonly Dictionary<Vector3,float> wallCache=[];
    private readonly Dictionary<(Vector3 Origin,Vector3 Direction,float Distance,int Mask),(bool Found,RaycastHit Hit)> rayCache=[];
    public long NativeQueries { get; private set; }
    public long CacheHits { get; private set; }
    private long sliceDeadline;
    public void BeginSlice(double milliseconds){
        if(rayCache.Count>60000)rayCache.Clear();
        sliceDeadline=System.Diagnostics.Stopwatch.GetTimestamp()+(long)(Math.Max(.1,milliseconds)*System.Diagnostics.Stopwatch.Frequency/1000);
    }
    public void EndSlice()=>sliceDeadline=0;
    private long liveWallAt;
    private readonly Dictionary<Vector3,float> liveWallCache=[];
    public float WallPenalty(Vector3 point){
        var key=new Vector3(MathF.Round(point.X*4)/4,MathF.Round(point.Y*4)/4,MathF.Round(point.Z*4)/4);
        if(groundCache==null){
            var now=Environment.TickCount64;if(now-liveWallAt>150){liveWallAt=now;liveWallCache.Clear();}
            if(liveWallCache.TryGetValue(key,out var live))return live;
        }
        if(groundCache!=null&&wallCache.TryGetValue(key,out var cached))return cached;
        Ready();const float preferred=3f;var nearest=preferred;
        for(var i=0;i<8;i++){
            var angle=i*MathF.PI/4;var direction=new Vector3(MathF.Cos(angle),0,MathF.Sin(angle));
            if(Hit(point+new Vector3(0,.65f,0),direction,preferred,out var hit,WalkingCollisionFilter.Movement))nearest=MathF.Min(nearest,CollisionRouteSearch.Horizontal(point,hit.Point));
        }
        var penalty=Math.Clamp((preferred-nearest)/(preferred-Radius),0,1);
        if(groundCache!=null&&wallCache.Count<100000)wallCache[key]=penalty;
        if(groundCache==null&&liveWallCache.Count<512)liveWallCache[key]=penalty;
        return penalty;
    }
    public bool ComfortableTraverse(Vector3 from,Vector3 to)=>Traverse(from,to)&&WalkingClearance.PreservesRoom(from,to,WallPenalty);
    internal Vector3? LastBlockedAt { get; private set; }
    public string? TraversalFailure { get; private set; }
    private bool OneWayBlocked(Vector3 a,Vector3 b){var delta=b-a;var length=delta.Length();return length>.001f&&Hit(a,delta/length,length,out _,WalkingCollisionFilter.Movement);}
    private bool Blocked(Vector3 a,Vector3 b)=>WalkingClearance.SegmentBlocked(a,b,OneWayBlocked);
    internal static unsafe bool Available(){
        var f=GameFramework.Instance();var c=f==null?null:f->BGCollisionModule;
        return c!=null&&!c->ShuttingDown&&c->LoadInProgressCounter==0;
    }
    private static unsafe void Ready()
    {
        var f=GameFramework.Instance();var c=f==null?null:f->BGCollisionModule;
        if(c==null||c->ShuttingDown||c->LoadInProgressCounter>0)throw new InvalidOperationException("Waiting for collision geometry to finish loading.");
    }
    private unsafe bool Hit(Vector3 origin,Vector3 direction,float distance,out RaycastHit hit,int materialMask=WalkingCollisionFilter.Support)
    {
        var key=(origin,direction,distance,materialMask);
        if(groundCache!=null&&rayCache.TryGetValue(key,out var saved)){CacheHits++;hit=saved.Hit;return saved.Found;}
        if(sliceDeadline!=0&&System.Diagnostics.Stopwatch.GetTimestamp()>=sliceDeadline)throw new NavigationSliceExpiredException();
        var framework=GameFramework.Instance();var module=framework==null?null:framework->BGCollisionModule;
        if(module==null)throw new InvalidOperationException("Collision geometry is unavailable.");
        int* flags=stackalloc int[4]{materialMask,0,0,0};
        var result=default(RaycastHit);
        NativeQueries++;var found=module->RaycastMaterialFilter(&result,&origin,&direction,distance,1,flags);hit=result;
        if(groundCache!=null&&rayCache.Count<65536)rayCache[key]=(found,hit);
        return found;
    }
    public bool Ground(Vector3 near,out Vector3 ground)
    {
        var samples=groundCache??(samplingLiveSweep||liveDecision?liveSweepGround:null);
        if(samples!=null&&samples.TryGetValue(near,out var cached)){ground=cached.Ground;return cached.Found;}
        var found=ReadGround(near,out ground);
        if(samples!=null&&samples.Count<100000)samples[near]=(found,ground);
        return found;
    }
    internal string GroundFailure { get; private set; }="";
    private bool ReadGround(Vector3 near,out Vector3 ground)
    {
        Ready();ground=default;GroundFailure="";
        if(!Hit(near+new Vector3(0,.7f,0),-Vector3.UnitY,1.5f,out var hit)){GroundFailure="No support ray hit";return false;}
        // Triangle cross product is available even when a collider omits its normal.
        var normal=hit.Normal;if(normal.LengthSquared()<.1f)normal=Vector3.Cross(hit.V2-hit.V1,hit.V3-hit.V1);
        if(normal.LengthSquared()<.0001f||Vector3.Normalize(normal).Y<.65f){GroundFailure=$"Support normal rejected: {normal}";return false;}
        if(hit.Point.Y-near.Y>.65f||near.Y-hit.Point.Y>.7f){GroundFailure=$"Support height rejected: {hit.Point.Y-near.Y}";return false;}
        ground=hit.Point;
        if(Hit(ground+new Vector3(0,.12f,0),Vector3.UnitY,Height-.12f,out _,WalkingCollisionFilter.Movement)){GroundFailure="Headroom blocked";return false;}
        return true;
    }
    internal bool InteractionVisible(Vector3 floor,Vector3 marker)
    {
        Ready();
        var eye=floor+new Vector3(0,1.1f,0);
        var delta=marker-eye;var length=delta.Length();
        // Ignore the marker's own surface, but do not interact through intervening walls.
        // Interaction markers may be inside the target's own movement collider.
        // Do not mistake that collider for an intervening visibility wall.
        // Physical walking still uses the stricter movement filter everywhere.
        return length<.3f||!Hit(eye,delta/length,length-.25f,out _);
    }
    public bool Traverse(Vector3 from,Vector3 to)
    {
        Ready();
        // Reuse exact support samples only within this live sweep. Never carry
        // them into a later movement decision where geometry may have changed.
        if(liveDecision&&decisionSweeps.TryGetValue((from,to),out var saved)){
            LastBlockedAt=saved.Blocked;TraversalFailure=saved.Failure;return saved.Clear;
        }
        samplingLiveSweep=groundCache==null;if(!liveDecision)liveSweepGround.Clear();
        try{
            Vector3? blockedAt;
            TraversalFailure=null;
            var clear=groundCache!=null?traversalCache.Check(this,from,to,Blocked,out blockedAt):WalkingGroundTraversal.Check(this,from,to,Blocked,out blockedAt,rejected:reason=>TraversalFailure=reason);
            if(groundCache!=null)TraversalFailure=traversalCache.Failure;
            LastBlockedAt=blockedAt;
            if(liveDecision)decisionSweeps[(from,to)]=(clear,blockedAt,TraversalFailure);
            return clear;
        }finally{samplingLiveSweep=false;if(!liveDecision)liveSweepGround.Clear();}
    }
}
