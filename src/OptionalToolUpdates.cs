namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<string,DateTimeOffset> optionalToolRetry=[];
    private readonly Dictionary<string,string> optionalToolFailures=[];
    private Dictionary<string,Action<DateTimeOffset>>? optionalToolUpdates;
    private bool OptionalToolHasWork(string name)=>name switch
    {
        "Equinox Market"=>true,
        "Gil Trader"=>gilTrade.Running,
        "Trade item shortcut"=>pendingTradeItemShortcut is not null,
        "Camera"=>OptionalToolAvailable("Camera")&&config.RememberCameraHeight||cameraHeightDefaultPending,
        "QuickLoot"=>config.EnableQuickLoot,
        "Coffer markers"=>config.EnableCofferMarkers,
        "Quest Helper"=>config.EnableFollowThem||helperPermission.Active||helperTextAdvanceOwned||helperNativeEscapeUntil!=default||helperSceneHook?.IsEnabled==true||helperStatusTask is not null||helperLeaderTask is not null||helperControlTask is not null||helperSendTask is not null||helperFollowers.Length>0,
        "FollowThem"=>config.EnableFollowThem||followStopPending||followStopUnconfirmed||followNativePulseUntil!=default,
        "Follow movement"=>followStopPending||followStopUnconfirmed||followNativePulseUntil!=default,
        "Follow portals"=>config.EnableFollowThem||followPortalHook?.IsEnabled==true||portalSendTask is not null||portalReadTask is not null||followLeaseId.Length>0||followLeaseTask is not null||outgoingTrips.Count>0||outgoingPortal is not null||receivedPortal is not null,
        _=>true
    };
    private void RunOptionalTool(string name,DateTimeOffset now)
    {
        // Pending teardown remains live even after a setting is switched off.
        if(!OptionalToolHasWork(name)){optionalToolRetry.Remove(name);optionalToolFailures.Remove(name);return;}
        if(optionalToolRetry.TryGetValue(name,out var retry)&&now<retry)return;
        optionalToolUpdates??=new()
        {
            ["Equinox Market"]=UpdateEquinoxMarket,
            ["Gil Trader"]=UpdateGilTrader,
            ["Trade item shortcut"]=UpdateTradeItemShortcut,
            ["Camera"]=UpdateCameraTool,["QuickLoot"]=UpdateQuickLoot,["Coffer markers"]=UpdateCofferMarkers,
            ["Follow portals"]=UpdatePortalTool,["Quest Helper"]=UpdateHelper,
            ["FollowThem"]=UpdateFollowThem,["Follow movement"]=UpdateFollowMovementTool
        };
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try{optionalToolUpdates[name](now);optionalToolRetry.Remove(name);optionalToolFailures.Remove(name);}
        catch(Exception ex)
        {
            optionalToolFailures[name]=ex.GetType().Name+" · retrying in 5 seconds";
            optionalToolRetry[name]=now.AddSeconds(5);
            errorJournal.Record("optional-"+name,"Optional tool update deferred; gameplay tracking continues.",exceptionType:ex.GetType().Name);
        }
        finally{TrackPerformance(name,started);}
    }
    private void UpdateCameraTool(DateTimeOffset now)=>UpdateCameraHeight();
    private void UpdateFollowMovementTool(DateTimeOffset now)=>UpdateFollowMovementStop(now);
    private void UpdatePortalTool(DateTimeOffset now)
    {
        // A disabled receiver must observe/discard its completed request, never
        // replay it and never keep the idle shutdown path awake indefinitely.
        if(!config.EnableFollowThem&&portalReadTask?.IsCompleted==true){_=portalReadTask.Exception;portalReadTask=null;}
        UpdateFollowPortalRelay(now);
    }
}
