namespace EquinoxCompanion;
public static class FollowTravelRecovery
{
    public static long Deadline(FollowPortalSignal s)=>s.ExpiresAt>0?Math.Min(s.ExpiresAt,s.SentAt+120000):s.SentAt+120000;
    public static bool PendingFresh(FollowPortalSignal s,long now)=>now>=s.SentAt&&now<Deadline(s);
    public static bool OwnEstate(FollowPortalSignal s,ulong self)=>s.TravelKind=="friendestate"&&self!=0&&
        ulong.TryParse(s.FriendContentId,out var owner)&&owner==self&&s.Destination is "Private Estate" or "Free Company Estate";
    public static bool ReturnedDoor(FollowPortalSignal s,bool absent,bool nearby,bool loading,bool departed,uint world,uint territory,uint map)=>
        s.TravelKind=="door"&&absent&&nearby&&!loading&&!departed&&world==s.CurrentWorld&&territory==s.Territory&&map==s.MapId;
}
