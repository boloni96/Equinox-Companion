using System.Numerics;
namespace EquinoxCompanion;
public static class FollowTravelPolicy
{
    public static bool CanUse(FollowPortalSignal s,long now,long armedAt,string name,uint world,uint currentWorld,uint territory,uint map,string entity,double age,Vector3 position,bool meetAtTeleports=false)
    {
        if(s.TravelKind is not ("teleport" or "aethernet" or "ward" or "transport" or "friendestate" or "estate" or "door" or "boundary")||s.HandlerType!=0||s.Id.Length!=32||!s.Id.All(Uri.IsHexDigit))return false;
        if(s.SentAt<armedAt||s.SentAt<now-120000||s.SentAt>now+5000||s.ExpiresAt<=now||s.ExpiresAt>now+125000)return false;
        var remote=meetAtTeleports&&s.TravelKind is "teleport" or "estate" or "friendestate";
        if(!FollowThemSession.Matches(name,world,s.Name,s.HomeWorld)||s.CurrentWorld!=currentWorld||!remote&&(s.Territory!=territory||s.MapId!=map||string.IsNullOrEmpty(entity)||s.EntityId!=entity||!double.IsFinite(age)||age<0||age>120))return false;
        if(!float.IsFinite(s.X)||!float.IsFinite(s.Y)||!float.IsFinite(s.Z))return false;
        if(s.TravelKind is "teleport" or "estate"&&s.AetheryteId==0||s.TravelKind=="aethernet"&&(s.BaseId==0||string.IsNullOrWhiteSpace(s.Destination)||s.Destination.Length>100))return false;
        if(s.TravelKind=="ward"&&(s.Ward is <1 or >30||s.DestinationTerritory==0||(s.BaseId==0&&s.SourceKind!="boundary")||!FollowPortalPolicy.IsConfirmationSupported(s.Confirmation)))return false;
        if(s.TravelKind=="estate"&&(s.EstateId.Length!=16||!s.EstateId.All(Uri.IsHexDigit)))return false;
        if(s.TravelKind=="door"&&(s.SourceKind!="EventObj"||s.BaseId==0))return false;
        if(s.TravelKind is "transport" or "friendestate"&&!FollowTransportPolicy.Valid(s))return false;
        if(s.ArrivalInstance>9)return false;
        if(s.TravelKind=="boundary"&&(s.SourceKind!="boundary"||s.BaseId!=0||s.DutyId!=0||s.Approach is not {Valid:true}||s.Arrival is not {Valid:true}||s.ArrivalTerritory==s.Territory||s.ArrivalWorld!=s.CurrentWorld))return false;
        if(!float.IsFinite(s.SourceRadius)||s.SourceRadius<0||s.SourceRadius>10)return false;
        return remote||Vector3.DistanceSquared(position,new(s.X,s.Y,s.Z))<=(s.TravelKind is "teleport" or "friendestate" or "estate" or "boundary"?900:MathF.Pow(3+s.SourceRadius,2));
    }
}
