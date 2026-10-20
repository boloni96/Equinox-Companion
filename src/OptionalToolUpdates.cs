namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<string,DateTimeOffset> optionalToolRetry=[];
    private readonly Dictionary<string,string> optionalToolFailures=[];
    private Dictionary<string,Action<DateTimeOffset>>? optionalToolUpdates;
    private void RunOptionalTool(string name,DateTimeOffset now)
    {
        if(optionalToolRetry.TryGetValue(name,out var retry)&&now<retry)return;
        optionalToolUpdates??=new()
        {
            ["Camera"]=UpdateCameraTool,["QuickLoot"]=UpdateQuickLoot,["Coffer markers"]=UpdateCofferMarkers,
            ["Follow portals"]=UpdateFollowPortalRelay,["Quest Helper"]=UpdateHelper,
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
}
