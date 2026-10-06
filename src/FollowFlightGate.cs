namespace EquinoxCompanion;
public sealed class FollowFlightGate
{
    private int attempts;
    private DateTimeOffset next;
    public void Reset(){attempts=0;next=default;}
    public bool Try(DateTimeOffset now){if(attempts>=3||now<next)return false;attempts++;next=now.AddMilliseconds(500);return true;}
    public bool Exhausted=>attempts>=3;
}
