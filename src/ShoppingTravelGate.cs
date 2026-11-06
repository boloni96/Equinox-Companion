namespace EquinoxCompanion;
// World ID can change before the game is ready to accept another teleport.
// Keep the original shopping request and require a stable ready interval.
public sealed class ShoppingTravelGate
{
    private DateTimeOffset? readySince;private DateTimeOffset nextRequest;
    public void Reset(){readySince=null;nextRequest=default;}
    public bool Ready(DateTimeOffset now,bool ready){if(!ready){readySince=null;return false;}readySince??=now;return now-readySince.Value>=TimeSpan.FromSeconds(2);}
    public bool MayRequest(DateTimeOffset now){if(now<nextRequest)return false;nextRequest=now.AddSeconds(2);return true;}
}
