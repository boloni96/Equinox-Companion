namespace EquinoxCompanion;

public interface ISellerAutoRetainer
{
    bool Available { get; }
    bool GetSuppressed();
    void SetSuppressed(bool value);
    bool IsBusy();
}
public sealed record SellerPreparation(bool Ready,string Status="",string Error="");

// Suppress scheduling before checking busy: already queued native actions must
// finish before Seller touches menus. Never change AutoRetainer configuration.
public sealed class SellerAutoRetainerPause(ISellerAutoRetainer api)
{
    public bool Held { get; private set; }
    public bool PreviouslySuppressed { get; private set; }
    public bool RestorePending { get; private set; }
    public string Status { get; private set; }="AutoRetainer is not paused by Seller.";
    private DateTimeOffset nextRestore,nextCheck;
    private DateTimeOffset? quietSince;
    private SellerPreparation last=new(false);

    public SellerPreparation Acquire(DateTimeOffset now)
    {
        if(Held||RestorePending)return new(false,Status,"AutoRetainer pause from the previous run has not been released.");
        try {
            if(!api.Available){Status="AutoRetainer is not loaded.";return last=new(true,Status);}
            PreviouslySuppressed=api.GetSuppressed();
            // A setter may change state and then throw. Keep the original state
            // until restoration has been verified, including that failure path.
            Held=true;quietSince=null;nextCheck=default;
            if(!PreviouslySuppressed)api.SetSuppressed(true);
            return Check(now);
        }catch(Exception ex){Status="Could not pause AutoRetainer ("+ex.GetType().Name+").";return last=new(false,Status,Status);}
    }
    public SellerPreparation Check(DateTimeOffset now)
    {
        if(!Held)return last;
        if(now<nextCheck)return last;
        nextCheck=now.AddMilliseconds(100);
        try {
            if(!api.Available)return Fail("AutoRetainer became unavailable; Seller stopped.");
            if(!api.GetSuppressed())return Fail("AutoRetainer resumed outside Seller; Seller stopped.");
            if(api.IsBusy()){
                quietSince=null;Status="AutoRetainer paused · waiting for its current action to finish…";
                return last=new(false,Status);
            }
            quietSince??=now;
            if(now-quietSince.Value<TimeSpan.FromMilliseconds(750)){
                Status="AutoRetainer paused · waiting for the menus to settle…";
                return last=new(false,Status);
            }
            Status=PreviouslySuppressed?"AutoRetainer remains paused (it was already paused).":"AutoRetainer paused by Seller.";
            return last=new(true,Status);
        }catch(Exception ex){return Fail("AutoRetainer pause could not be verified ("+ex.GetType().Name+").");}
    }
    private SellerPreparation Fail(string message){Status=message;return last=new(false,message,message);}
    public void Release(DateTimeOffset now)
    {
        if(!Held&&!RestorePending)return;
        if(RestorePending&&now<nextRestore)return;
        try {
            // If it was already paused, Seller must not resume it. If the user
            // resumed it themselves, leave that explicit change in place too.
            if(!PreviouslySuppressed&&api.Available&&api.GetSuppressed()){
                api.SetSuppressed(false);
                if(api.GetSuppressed())throw new InvalidOperationException("Pause release was not confirmed");
            }
            Held=false;RestorePending=false;quietSince=null;nextCheck=default;
            Status=PreviouslySuppressed?"AutoRetainer left paused, as before Seller.":"AutoRetainer pause released.";
        }catch(Exception ex){RestorePending=true;nextRestore=now.AddSeconds(1);Status="AutoRetainer pause release needs attention ("+ex.GetType().Name+"); retrying.";}
    }
}
