using System.Numerics;
namespace EquinoxCompanion;
public static class FollowPortalPolicy
{
    public static bool IsConfirmationSupported(string text)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>500||!text.TrimEnd().EndsWith('?'))return false;
        if(new[]{"gil","purchase","buy ","discard","delete","abandon","spend","pay "}.Any(w=>text.Contains(w,StringComparison.OrdinalIgnoreCase)))return false;
        return new[]{"Use the teleporter", "Enter ", "Leave ", "Proceed to ", "Travel to ", "Move to ", "Exit "}.Any(p=>text.StartsWith(p,StringComparison.OrdinalIgnoreCase));
    }
    public static bool CanUse(FollowPortalSignal s,long now,long armedAt,string name,uint homeWorld,uint currentWorld,uint territory,uint mapId,string seenEntity,double secondsSinceSeen,Vector3 self)
    {
        if(s.Confirmation.Length>0&&!IsConfirmationSupported(s.Confirmation))return false;
        if(s.Id.Length!=32||!s.Id.All(Uri.IsHexDigit)||s.HandlerType is not (2 or 20)||s.BaseId==0||s.Confirmation.Length>500)return false;
        if(s.SentAt<armedAt||s.SentAt>now+5000||s.SentAt<now-120000||s.ExpiresAt<=now||s.ExpiresAt>now+125000)return false;
        if(!FollowThemSession.Matches(name,homeWorld,s.Name,s.HomeWorld)||s.CurrentWorld!=currentWorld||s.Territory!=territory||s.MapId!=mapId)return false;
        if(string.IsNullOrEmpty(seenEntity)||seenEntity!=s.EntityId||!double.IsFinite(secondsSinceSeen)||secondsSinceSeen<0||secondsSinceSeen>120)return false;
        if(!float.IsFinite(s.X)||!float.IsFinite(s.Y)||!float.IsFinite(s.Z))return false;
        return Vector3.DistanceSquared(self,new(s.X,s.Y,s.Z))<=9;
    }
}
