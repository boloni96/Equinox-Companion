using System.Numerics;
namespace EquinoxCompanion;
public static class FollowTravelPolicy
{
    public static bool CanUse(FollowPortalSignal s,long now,long armedAt,string name,uint world,uint currentWorld,uint territory,uint map,string entity,double age,Vector3 position)
    {
        if(s.TravelKind is not ("teleport" or "aethernet" or "ward")||s.HandlerType!=0||s.Id.Length!=32||!s.Id.All(Uri.IsHexDigit))return false;
        if(s.SentAt<armedAt||s.SentAt<now-15000||s.SentAt>now+5000||s.ExpiresAt<=now||s.ExpiresAt>now+20000)return false;
        if(!FollowThemSession.Matches(name,world,s.Name,s.HomeWorld)||s.CurrentWorld!=currentWorld||s.Territory!=territory||s.MapId!=map||string.IsNullOrEmpty(entity)||s.EntityId!=entity||!double.IsFinite(age)||age<0||age>30)return false;
        if(!float.IsFinite(s.X)||!float.IsFinite(s.Y)||!float.IsFinite(s.Z))return false;
        if(s.TravelKind=="teleport"&&s.AetheryteId==0||s.TravelKind=="aethernet"&&(s.BaseId==0||string.IsNullOrWhiteSpace(s.Destination)||s.Destination.Length>100))return false;
        if(s.TravelKind=="ward"&&(s.Ward is <1 or >30||s.DestinationTerritory==0||s.BaseId==0||!FollowPortalPolicy.IsConfirmationSupported(s.Confirmation)))return false;
        return Vector3.DistanceSquared(position,new(s.X,s.Y,s.Z))<=(s.TravelKind=="teleport"?900:9);
    }
}
