namespace EquinoxCompanion;
internal enum WalkingInputDecision { Inactive, Expired, Allowed }
// Refreshed only after all framework-thread checks and route work succeed.
internal sealed class WalkingInputLease
{
    public static bool CanSupplyGround(float left,float forward,float turn)=>
        float.IsFinite(left)&&float.IsFinite(forward)&&float.IsFinite(turn)&&left==0&&forward==0&&turn==0;
    public DateTimeOffset Until { get; private set; }
    public void Arm(DateTimeOffset readyAt)=>Until=readyAt.AddMilliseconds(180);
    public void Revoke()=>Until=default;
    public WalkingInputDecision Check(DateTimeOffset now)=>Until==default?WalkingInputDecision.Inactive:
        now>=Until?WalkingInputDecision.Expired:WalkingInputDecision.Allowed;
}
