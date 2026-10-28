namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private SellerAutoRetainerPause sellerAutoRetainerPause=null!;
    private string sellerLastAutoRetainerStatus="";
    private sealed class SellerAutoRetainerPort:ISellerAutoRetainer
    {
        public bool Available=>Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName=="AutoRetainer");
        public bool GetSuppressed()=>Pi.GetIpcSubscriber<bool>("AutoRetainer.GetSuppressed").InvokeFunc();
        public void SetSuppressed(bool value)=>Pi.GetIpcSubscriber<bool,object>("AutoRetainer.SetSuppressed").InvokeAction(value);
        public bool IsBusy()=>Pi.GetIpcSubscriber<bool>("AutoRetainer.PluginState.IsBusy").InvokeFunc();
    }
    private void TraceAutoRetainerHandoff()
    {
        if(sellerLastAutoRetainerStatus==sellerAutoRetainerPause.Status)return;
        sellerLastAutoRetainerStatus=sellerAutoRetainerPause.Status;
        TraceSeller("AutoRetainer",sellerAutoRetainerPause.Status,new {sellerAutoRetainerPause.Held,sellerAutoRetainerPause.PreviouslySuppressed,sellerAutoRetainerPause.RestorePending});
    }
    private SellerPreparation PrepareSellerAutomation()
    {
        if(retrievalRun?.Running==true)return new(false,"Retriever is running.","Stop retrieval before starting Seller.");
        var result=sellerAutoRetainerPause.Check(DateTimeOffset.UtcNow);TraceAutoRetainerHandoff();
        if(!result.Ready)return result;
        if(!AcquireMarketBuddyLock())return new(false,sellerNotice,sellerNotice);
        return result;
    }
    private void UpdateSellerAutoRetainer(DateTimeOffset now)
    {
        // Restoration also runs after Market is disabled; a temporary IPC error
        // must not silently leave another plugin paused forever.
        if(sellerAutoRetainerPause.RestorePending)sellerAutoRetainerPause.Release(now);
        if(sellerRun.Running&&sellerRun.State!=SellerRun.Phase.Prepare&&sellerAutoRetainerPause.Held){
            var state=sellerAutoRetainerPause.Check(now);
            if(!state.Ready){
                var reason=state.Error.Length>0?state.Error:"AutoRetainer started another action; Seller stopped before its next action.";
                TraceSeller("Handoff lost",reason);sellerRun.Stop(reason);
            }
        }
        TraceAutoRetainerHandoff();
    }
}
