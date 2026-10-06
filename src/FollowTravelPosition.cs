using System.Numerics;
namespace EquinoxCompanion;
public sealed record FollowTravelPosition(float X,float Y,float Z)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Vector3 Point=>new(X,Y,Z);
    public static FollowTravelPosition From(Vector3 p)=>new(p.X,p.Y,p.Z);
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Valid=>float.IsFinite(X)&&float.IsFinite(Y)&&float.IsFinite(Z)&&Math.Abs(X)<100000&&Math.Abs(Y)<100000&&Math.Abs(Z)<100000;
}
public sealed class FollowStationaryGate
{
    private Vector3 anchor;
    private DateTimeOffset? since;
    public void Reset()=>since=null;
    public bool Observe(DateTimeOffset now,Vector3 position,bool ready)
    {
        if(!ready||!float.IsFinite(position.X)||!float.IsFinite(position.Y)||!float.IsFinite(position.Z)){Reset();return false;}
        if(since==null||Vector3.DistanceSquared(anchor,position)>.0004f){anchor=position;since=now;return false;}
        return now-since>=TimeSpan.FromMilliseconds(750);
    }
}
public static class FollowApproachPolicy
{
    public static bool CanApproach(FollowPortalSignal s,Vector3 self)=>s.TravelKind!="world"&&s.Approach is {Valid:true} a&&
        float.IsFinite(self.X)&&float.IsFinite(self.Y)&&float.IsFinite(self.Z)&&Vector3.DistanceSquared(self,a.Point)<=3600&&Math.Abs(self.Y-a.Y)<=5;
}

public static class FollowArrivalPolicy
{
    public static bool HasDeparted(FollowPortalSignal source,bool preparing,bool sawLoading,uint world,uint territory,uint map,Vector3 start,Vector3 current)=>
        !preparing&&(sawLoading||world!=source.CurrentWorld||territory!=source.Territory||map!=source.MapId||Vector3.DistanceSquared(start,current)>144);
}
public static class FollowEstatePrice
{
    public static uint? Read(IEnumerable<string> texts)
    {
        var fees=texts.Select(t=>System.Text.RegularExpressions.Regex.Match(t,@"^([0-9][0-9,]*)\s*(?:gil|\p{Co})?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Where(m=>m.Success).ToArray();
        return fees.Length==1&&uint.TryParse(fees[0].Groups[1].Value.Replace(",",""),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var fee)?fee:null;
    }
}
