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
    public float Penalty(Vector3 point)
    {
        if(!float.IsFinite(point.X)||!float.IsFinite(point.Z))return 0;
        var u=((point.X+offsetX)*scale+1024)/2048*width;
        var v=((point.Z+offsetZ)*scale+1024)/2048*height;
        if(u<0||v<0||u>=width||v>=height)return 0;
        return costs[(int)v*width+(int)u]/255f;
    }
}
