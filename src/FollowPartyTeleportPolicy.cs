namespace EquinoxCompanion;
public static class FollowPartyTeleportPolicy
{
    // Match the accepted destination and capture time, not the leader's exact
    // spawn coordinates: party members need not spawn at the same point/height.
    public static bool Matches(FollowPortalSignal s,uint aetheryte,uint world,long accepted,long now,long armed)=>
        aetheryte!=0&&s.TravelKind=="teleport"&&s.AetheryteId==aetheryte&&s.CurrentWorld==world&&accepted>=armed&&
        now>=accepted&&now-accepted<120000&&Math.Abs(s.SentAt-accepted)<=20000;
}
