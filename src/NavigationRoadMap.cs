using System.Numerics;
namespace EquinoxCompanion;
// Soft guidance from the game's unlabelled parchment map. Never collision authority.
public sealed class NavigationRoadMap
{
    private readonly byte[] costs;
    private readonly int width,height;
    private readonly float scale,offsetX,offsetZ;
    public float RoadCoverage { get; }
    private NavigationRoadMap(byte[] values,int w,int h,float size,float x,float z,float coverage){costs=values;width=w;height=h;scale=size/100;offsetX=x;offsetZ=z;RoadCoverage=coverage;}
    public static bool RoadColour(byte r,byte g,byte b,byte a)=>a>=200&&r>=190&&g>=185&&b>=140&&r>=g&&g>=b&&r-g<=25&&g-b<=65;
    public static NavigationRoadMap? FromRgba(byte[] pixels,int w,int h,float size,float x,float z,bool bgra=false)
    {
        if(w is <64 or >4096||h is <64 or >4096||pixels.Length<w*h*4||!float.IsFinite(size)||size is <25 or >800)return null;
        const int block=4;var gw=w/block;var gh=h/block;var road=new bool[gw*gh];var distance=new float[road.Length];var count=0;
        for(var gy=0;gy<gh;gy++)for(var gx=0;gx<gw;gx++){
            var hits=0;for(var dy=0;dy<block;dy++)for(var dx=0;dx<block;dx++){
                var i=((gy*block+dy)*w+gx*block+dx)*4;
                if(RoadColour(pixels[i+(bgra?2:0)],pixels[i+1],pixels[i+(bgra?0:2)],pixels[i+3]))hits++;
            }
            var index=gy*gw+gx;road[index]=hits>=block*block/2;distance[index]=road[index]?10000:0;if(road[index])count++;
        }
        var coverage=(float)count/road.Length;
        // Unrecognized artwork contributes no guidance; retain geometry-only navigation.
        if(coverage<.005f||coverage>.65f)return null;
        for(var y=0;y<gh;y++)for(var xx=0;xx<gw;xx++){
            var i=y*gw+xx;if(xx>0)distance[i]=MathF.Min(distance[i],distance[i-1]+1);if(y>0)distance[i]=MathF.Min(distance[i],distance[i-gw]+1);
        }
        for(var y=gh-1;y>=0;y--)for(var xx=gw-1;xx>=0;xx--){
            var i=y*gw+xx;if(xx+1<gw)distance[i]=MathF.Min(distance[i],distance[i+1]+1);if(y+1<gh)distance[i]=MathF.Min(distance[i],distance[i+gw]+1);
        }
        var worldPerCell=2048f/gw/(size/100);var cost=new byte[road.Length];
        for(var i=0;i<cost.Length;i++)cost[i]=(byte)Math.Clamp((road[i]?.3f*(1-MathF.Min(1,distance[i]*worldPerCell/3)):1)*255,0,255);
        return new(cost,gw,gh,size,x,z,coverage);
    }
    internal int Width=>width;
    internal int Height=>height;
    internal float CellSize=>2048f/width/scale;
    internal (int X,int Z) Cell(Vector3 p)=>((int)MathF.Floor(((p.X+offsetX)*scale+1024)/2048*width),(int)MathF.Floor(((p.Z+offsetZ)*scale+1024)/2048*height));
    internal Vector3 World(int x,int z)=>new(((x+.5f)/width*2048-1024)/scale-offsetX,0,((z+.5f)/height*2048-1024)/scale-offsetZ);
    internal float Cost(int x,int z)=>1+6*costs[z*width+x]/255f;
    public NavigationRoadRoute? BuildRoute(Vector3 start,Vector3 goal)=>NavigationRoadRoute.Build(this,start,goal);
    public float Penalty(Vector3 point)
    {
        if(!float.IsFinite(point.X)||!float.IsFinite(point.Z))return 0;
        var u=((point.X+offsetX)*scale+1024)/2048*width;
        var v=((point.Z+offsetZ)*scale+1024)/2048*height;
        if(u<0||v<0||u>=width||v>=height)return 0;
        return costs[(int)v*width+(int)u]/255f;
    }
}

// A connected 2D road-distance field, not a walkability mesh. Built on a worker
// from managed texture data. Every selected 3D edge still needs collision approval.
public sealed class NavigationRoadRoute
{
    private readonly NavigationRoadMap map;
    private readonly int minX,minZ,width,height;
    private readonly float[] toGoal,toStart;
    public Vector3[] Points { get; }
    public int Cells=>width*height;
    private NavigationRoadRoute(NavigationRoadMap map,int x,int z,int w,int h,float[] goal,float[] start,Vector3[] points){
        this.map=map;minX=x;minZ=z;width=w;height=h;toGoal=goal;toStart=start;Points=points;
    }
    public float Remaining(Vector3 point,bool reverse=false){
        var cell=map.Cell(point);var x=cell.X-minX;var z=cell.Z-minZ;
        if(x<0||z<0||x>=width||z>=height)return 0;
        var d=(reverse?toStart:toGoal)[z*width+x];
        // Weighting guides, never excludes, an off-road alternative.
        return float.IsFinite(d)?d/2:0;
    }
    internal NavigationRoadRoute? Detour(Vector3 start,Vector3 goal,Vector3[] blocked)=>Build(map,start,goal,blocked);
    internal static NavigationRoadRoute? Build(NavigationRoadMap map,Vector3 start,Vector3 goal,IReadOnlyList<Vector3>? blocked=null){
        if(!float.IsFinite(start.X)||!float.IsFinite(start.Z)||!float.IsFinite(goal.X)||!float.IsFinite(goal.Z))return null;
        var a=map.Cell(start);var b=map.Cell(goal);
        bool Inside((int X,int Z) p)=>p.X>=0&&p.Z>=0&&p.X<map.Width&&p.Z<map.Height;
        if(!Inside(a)||!Inside(b))return null;
        var margin=(int)MathF.Ceiling(Math.Clamp(Vector3.Distance(start,goal),64,256)/map.CellSize);
        var x0=Math.Max(0,Math.Min(a.X,b.X)-margin);var z0=Math.Max(0,Math.Min(a.Z,b.Z)-margin);
        var x1=Math.Min(map.Width-1,Math.Max(a.X,b.X)+margin);var z1=Math.Min(map.Height-1,Math.Max(a.Z,b.Z)+margin);
        var w=x1-x0+1;var h=z1-z0+1;
        var ai=(a.Z-z0)*w+a.X-x0;var bi=(b.Z-z0)*w+b.X-x0;
        // Native geometry can disprove a map connection. Exclude that small
        // area from this route's 2D guide, then let geometry validate the detour.
        // This is per-search feedback, never a persistent or cross-floor ban.
        var forbidden=new bool[w*h];
        if(blocked!=null){
            var radius=MathF.Max(3f,map.CellSize*1.5f);
            for(var z=0;z<h;z++)for(var x=0;x<w;x++){
                var point=map.World(x+x0,z+z0);
                forbidden[z*w+x]=blocked.Any(p=>{
                    if(CollisionRouteSearch.Horizontal(p,point)>radius)return false;
                    var away=new Vector2(start.X-p.X,start.Z-p.Z);
                    var step=new Vector2(point.X-start.X,point.Z-start.Z);
                    // When already beside a newly found boundary, leave room to
                    // escape sideways/away; never trap the start inside its buffer.
                    var escape=away.Length()<radius&&Vector2.Dot(step,away)>=0&&CollisionRouteSearch.Horizontal(p,point)>=away.Length();
                    return !escape;
                });
            }
        }
        forbidden[ai]=forbidden[bi]=false;
        (float[] Distance,int[] Next) Flood(int root){
            var distance=new float[w*h];Array.Fill(distance,float.PositiveInfinity);
            var next=new int[w*h];Array.Fill(next,-1);
            var queue=new PriorityQueue<int,float>();distance[root]=0;queue.Enqueue(root,0);
            while(queue.TryDequeue(out var index,out var cost)){
                if(cost>distance[index])continue;var x=index%w;var z=index/w;
                for(var dz=-1;dz<=1;dz++)for(var dx=-1;dx<=1;dx++){
                    if(dx==0&&dz==0)continue;var xx=x+dx;var zz=z+dz;
                    if(xx<0||zz<0||xx>=w||zz>=h)continue;
                    var other=zz*w+xx;
                    if(forbidden[other]||dx!=0&&dz!=0&&(forbidden[z*w+xx]||forbidden[zz*w+x]))continue;
                    var weight=(map.Cost(x+x0,z+z0)+map.Cost(xx+x0,zz+z0))*.5f;
                    var value=cost+map.CellSize*(dx!=0&&dz!=0?1.4142136f:1)*weight;
                    if(value>=distance[other])continue;distance[other]=value;next[other]=index;queue.Enqueue(other,value);
                }
            }
            return(distance,next);
        }
        var end=Flood(bi);if(!float.IsFinite(end.Distance[ai]))return null;
        var beginning=Flood(ai);var points=new List<Vector3>{start};
        // Cell centres near a wall are not interchangeable with the real actor
        // position. Start/end at the requested coordinates; geometry checks joins.
        for(var i=end.Next[ai];i>=0&&points.Count<=w*h;i=end.Next[i]){
            points.Add(i==bi?goal:map.World(i%w+x0,i/w+z0));if(i==bi)break;
        }
        if(ai==bi&&Vector3.DistanceSquared(start,goal)>.000001f)points.Add(goal);
        return new(map,x0,z0,w,h,end.Distance,beginning.Distance,points.ToArray());
    }
}
