using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private LatestSnapshotWriter<Configuration> configurationWriter = null!;
    private bool configurationDirty;
    private DateTimeOffset configurationDue;
    private double maxDrawMs,maxUpdateMs,maxSnapshotMs;
    private string configurationError="";
    private void SaveConfiguration()
    {
        // Coalesce a burst without postponing persistence indefinitely.
        if(!configurationDirty)configurationDue=DateTimeOffset.UtcNow.AddMilliseconds(250);
        configurationDirty=true;
    }
    private void PumpConfiguration()
    {
        if(configurationWriter.Failure is {} error)
        {
            configurationError="Local save failed: "+error.GetType().Name+". Retrying; keep Companion loaded.";
            if(!configurationDirty){configurationDirty=true;configurationDue=DateTimeOffset.UtcNow.AddSeconds(5);}
        }
        else configurationError="";
        if(!configurationDirty||DateTimeOffset.UtcNow<configurationDue)return;
        var started=Stopwatch.GetTimestamp();
        try{var snapshot=config.Snapshot();configurationWriter.Enqueue(snapshot);configurationDirty=false;}
        catch(Exception e){configurationDue=DateTimeOffset.UtcNow.AddSeconds(5);configurationError="Local snapshot failed: "+e.GetType().Name;}
        finally{maxSnapshotMs=Math.Max(maxSnapshotMs,Stopwatch.GetElapsedTime(started).TotalMilliseconds);}
    }
    private void FlushConfiguration()
    {
        try{configurationWriter.Flush(config.Snapshot());configurationDirty=false;}
        catch(Exception e){Log.Error(e,"Equinox local save failed during unload; retain the previous saved configuration.");}
    }
    private void Update(IFramework framework)
    {
        var started=Stopwatch.GetTimestamp();
        try{UpdateCore(framework);PumpConfiguration();}
        finally{maxUpdateMs=Math.Max(maxUpdateMs,Stopwatch.GetElapsedTime(started).TotalMilliseconds);}
    }
    private void Draw()
    {
        var started=Stopwatch.GetTimestamp();
        try{DrawCore();}
        finally{maxDrawMs=Math.Max(maxDrawMs,Stopwatch.GetElapsedTime(started).TotalMilliseconds);}
    }
    private void DrawPerformanceDiagnostics()
    {
        ImGui.TextUnformatted("Performance since reset (milliseconds)");
        ImGui.TextUnformatted($"Peak draw: {maxDrawMs:F2} · update: {maxUpdateMs:F2} · save snapshot: {maxSnapshotMs:F2}");
        ImGui.TextUnformatted(configurationDirty||configurationWriter.Busy?"Local save pending…":"Local save complete.");
        if(configurationError.Length>0)ImGui.TextWrapped(configurationError);
        if(ImGui.Button("Reset timing measurements"))maxDrawMs=maxUpdateMs=maxSnapshotMs=0;
        ImGui.TextWrapped("Reset, reproduce one hitch, then note these values. File serialization and writing run on a single background worker. These timings do not measure other plugins or the game's rendering.");
        ImGui.Separator();
    }
}
