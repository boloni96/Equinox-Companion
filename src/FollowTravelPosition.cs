using System.Numerics;
namespace EquinoxCompanion;
public static class FollowDutyLeavePolicy
{
    public static string ObsoleteReason(FollowPortalSignal s,bool enabled,bool loaded,bool loading,uint world,uint territory,uint duty)
    {
        if(s.TravelKind!="leaveDuty")return "";
        if(!enabled)return "Duty exit assistance is disabled; request discarded.";
        if(!loaded||loading||world==0||territory==0)return "";
        if(s.CurrentWorld!=world||duty!=0&&duty!=s.DutyId)return "Duty exit request belongs to a different World or duty; discarded.";
        return duty==0&&territory!=s.Territory?"Already outside the recorded duty; no exit is needed.":"";
    }
}
public sealed record HelperMeetingDestination(uint Id,uint Territory,uint Map,float X,float Z);
public static class HelperMeetingPolicy
{
    public static HelperMeetingDestination? Select(IEnumerable<HelperMeetingDestination> candidates,uint territory,uint map,Vector3 leader)=>
        territory==0||map==0||!float.IsFinite(leader.X)||!float.IsFinite(leader.Z)?null:candidates.Where(c=>c.Id!=0&&c.Territory==territory&&c.Map==map&&float.IsFinite(c.X)&&float.IsFinite(c.Z)).OrderBy(c=>Vector2.DistanceSquared(new(c.X,c.Z),new(leader.X,leader.Z))).ThenBy(c=>c.Id).FirstOrDefault();
}
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
    // Only a successful native request plus an observed loading cycle can replace
    // position proximity. A rejected/cancelled cast or merely being in the same
    // territory cannot complete the queued instruction.
    public static bool CompletedNativeTeleport(FollowPortalSignal s,bool accepted,bool sawLoading,bool loading,bool loaded,uint world,uint territory,uint map,uint instance)=>
        s.TravelKind is "teleport" or "estate" && accepted && sawLoading && !loading && loaded &&
        s.ArrivalWorld!=0 && s.ArrivalTerritory!=0 && world==s.ArrivalWorld && territory==s.ArrivalTerritory && map==s.ArrivalMap &&
        FollowInstancePolicy.Arrived(s.ArrivalInstance,instance);

    // Recover a queued aethernet leg already completed manually. Never treat
    // proximity in the source area as proof of travel.
    public static bool AlreadyAtAethernetArrival(FollowPortalSignal s,uint world,uint territory,uint map,Vector3 position)=>
        s.TravelKind=="aethernet" && (territory!=s.Territory||map!=s.MapId) &&
        s.Arrival is {Valid:true} a && world==s.ArrivalWorld && territory==s.ArrivalTerritory && map==s.ArrivalMap &&
        Vector3.DistanceSquared(position,a.Point)<225 && Math.Abs(position.Y-a.Y)<3;

    public static bool AlreadyAtTravelArrival(FollowPortalSignal s,uint world,uint territory,uint map,Vector3 position,bool acceptedPartyOffer)=>
        s.TravelKind is "aethernet" or "teleport" or "estate" or "friendestate" or "boundary" or "door" or "transport" &&
        (territory!=s.Territory||map!=s.MapId||s.TravelKind=="teleport"&&s.SourceKind=="CurrentMapMeeting"||acceptedPartyOffer&&s.TravelKind=="teleport"||s.TravelKind=="boundary"&&s.DutyId!=0&&Vector3.DistanceSquared(position,new(s.X,s.Y,s.Z))>144) &&
        s.Arrival is {Valid:true} a && world==s.ArrivalWorld && territory==s.ArrivalTerritory && map==s.ArrivalMap &&
        Vector3.DistanceSquared(position,a.Point)<225 && Math.Abs(position.Y-a.Y)<3;

    public static FollowPortalSignal[] RecoveryTail(IEnumerable<FollowPortalSignal> queue,long now,bool meetAtTeleports)=>
        queue.SkipWhile(s=>s.ExpiresAt<=now||!IndependentRecovery(s,meetAtTeleports)).ToArray();

    public static bool IndependentRecovery(FollowPortalSignal s,bool meetAtTeleports)=>
        s.TravelKind=="world"||meetAtTeleports&&s.TravelKind is "teleport" or "estate" or "friendestate";

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

public static class FollowAethernetEntry
{
    public static int RowCount(int valueCount)=>valueCount is >262 and <=1024?Math.Min(64,valueCount-262):0;
    public static bool IsDestination(uint kind,string? name)=>kind==0&&!string.IsNullOrWhiteSpace(name)&&name.Length<=100;
}
public static class FollowDeparturePolicy
{
    public static bool Arrived(uint sourceTerritory,uint expectedTerritory,uint currentTerritory,float distance,bool sawLoading,bool teleport)=>
        (!teleport||currentTerritory==expectedTerritory)&&(currentTerritory!=sourceTerritory||distance>12||sawLoading);
}

public static class FollowCrystalApproach
{
    public static Vector3 Point(Vector3 crystal,Vector3 leader,float radius)
    {
        var horizontal=new Vector3(leader.X-crystal.X,0,leader.Z-crystal.Z);
        var reach=Math.Clamp(radius,0,10)+1.75f;
        var height=leader.Y-crystal.Y;
        reach=MathF.Sqrt(Math.Max(.0625f,reach*reach-height*height));
        if(horizontal.LengthSquared()<=reach*reach)return leader;
        var edge=crystal+Vector3.Normalize(horizontal)*reach;
        return new(edge.X,leader.Y,edge.Z);
    }
}

public sealed class FollowApproachProgress
{
    private DateTimeOffset since;
    private float best;
    public void Reset(DateTimeOffset now,Vector3 position,Vector3 goal){since=now;best=Vector2.Distance(new(position.X,position.Z),new(goal.X,goal.Z));}
    public bool Stuck(DateTimeOffset now,Vector3 position,Vector3 goal,int seconds)
    {
        var distance=Vector2.Distance(new(position.X,position.Z),new(goal.X,goal.Z));
        if(distance<best-.25f){best=distance;since=now;}
        return now-since>=TimeSpan.FromSeconds(Math.Clamp(seconds,5,600));
    }
}


public static class FollowAethernetSource
{
    public static bool Matches(FollowPortalSignal s,long now,string name,uint home,uint world,uint territory,uint map,string entity)=>
        s.BaseId!=0&&s.SourceKind is "Aetheryte" or "EventObj"&&s.Approach is {Valid:true}&&
        now>=s.SentAt&&now-s.SentAt<=120000&&s.Name==name&&s.HomeWorld==home&&s.CurrentWorld==world&&
        s.Territory==territory&&s.MapId==map&&s.EntityId==entity;
    public static bool InRange(FollowPortalSignal s,Vector3 self,Vector3 source,float nativeRadius,float margin)=>
        Vector3.Distance(self,source)<=nativeRadius+margin||
        s.TravelKind=="aethernet"&&s.SourceRadius is >=0 and <=10&&s.Approach is {Valid:true} point&&
        Vector3.DistanceSquared(self,point.Point)<=2.25f&&Vector3.Distance(self,source)<=s.SourceRadius+margin;
}

public static class FollowAethernetArrival
{
    public static uint Resolve(IEnumerable<(uint Id,uint Territory,string Name)> candidates,uint territory,string name)
    {
        if(territory==0||string.IsNullOrWhiteSpace(name))return 0;
        var matches=candidates.Where(x=>x.Id!=0&&x.Territory==territory&&string.Equals(x.Name.Trim(),name.Trim(),StringComparison.Ordinal))
            .Select(x=>x.Id).Distinct().Take(2).ToArray();
        return matches.Length==1?matches[0]:0;
    }
    public static bool Confirmed(FollowPortalSignal trip,bool selected,bool sawLoading,bool loading,bool loaded,uint world,uint territory,uint map,uint instance,Vector3 self,Vector3 destination)=>
        trip.TravelKind=="aethernet"&&selected&&sawLoading&&!loading&&loaded&&
        trip.ArrivalWorld!=0&&trip.ArrivalTerritory!=0&&world==trip.ArrivalWorld&&territory==trip.ArrivalTerritory&&map==trip.ArrivalMap&&
        FollowInstancePolicy.Arrived(trip.ArrivalInstance,instance)&&
        Vector3.DistanceSquared(self,destination)<=400&&Math.Abs(self.Y-destination.Y)<=10;
}

