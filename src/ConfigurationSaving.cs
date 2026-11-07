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
    private readonly Dictionary<string, double> performanceStages = [];
    private void TrackPerformance(string stage, long started)
    {
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        performanceStages[stage] = Math.Max(performanceStages.GetValueOrDefault(stage), ms);
        RecordPerformanceSample(stage,ms);
    }
    private long configurationRevision;
    private void SaveConfiguration()
    {
        configurationRevision++;
        // Coalesce a burst without postponing persistence indefinitely.
        if(!configurationDirty)configurationDue=DateTimeOffset.UtcNow.AddSeconds(2);
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
        // Keep edits dirty while serialization is busy; capture the newest state once free.
        if(!SaveSnapshotPolicy.Ready(configurationDirty,configurationWriter.Busy,DateTimeOffset.UtcNow,configurationDue))return;
        var started=Stopwatch.GetTimestamp();
        try{var snapshot=config.Snapshot();configurationWriter.Enqueue(snapshot);configurationDirty=false;}
        catch(Exception e){configurationDue=DateTimeOffset.UtcNow.AddSeconds(5);configurationError="Local snapshot failed: "+e.GetType().Name;}
        finally{maxSnapshotMs=Math.Max(maxSnapshotMs,Stopwatch.GetElapsedTime(started).TotalMilliseconds);RecordPerformanceElapsed("Save snapshot",started); }
    }
    private void FlushConfiguration()
    {
        try{configurationWriter.Flush(config.Snapshot());configurationDirty=false;}
        catch(Exception e){Log.Error(e,"Equinox local save failed during unload; retain the previous saved configuration.");}
    }
    private void Update(IFramework framework)
    {
        BeginPerformanceFrame();
        var started=Stopwatch.GetTimestamp();
        try{UpdateCore(framework);PumpConfiguration();}
        finally{maxUpdateMs=Math.Max(maxUpdateMs,Stopwatch.GetElapsedTime(started).TotalMilliseconds);RecordPerformanceElapsed("Update",started); }
    }
    private void Draw()
    {
        var started=Stopwatch.GetTimestamp();
        try{DrawCore();}
        finally{maxDrawMs=Math.Max(maxDrawMs,Stopwatch.GetElapsedTime(started).TotalMilliseconds);RecordPerformanceElapsed("Draw",started); }
    }
    private void DrawPerformanceDiagnostics()
    {
        ImGui.TextUnformatted("Performance since reset (milliseconds)");
        ImGui.TextUnformatted($"Peak draw: {maxDrawMs:F2} · update: {maxUpdateMs:F2} · save snapshot: {maxSnapshotMs:F2}");
        ImGui.TextUnformatted(configurationDirty||configurationWriter.Busy?"Local save pending…":"Local save complete.");
        if(configurationError.Length>0)ImGui.TextWrapped(configurationError);
        foreach(var stage in performanceStages.OrderByDescending(x => x.Value)) ImGui.TextUnformatted($"{stage.Key}: {stage.Value:F2} ms peak");
        if(ImGui.Button("Reset timing measurements")) ResetPerformanceReport();
        SettingsHelp("Reset, reproduce one hitch, then note these values. File serialization and writing run on a single background worker. These timings do not measure other plugins or the game's rendering.");
        ImGui.Separator();
    }
}
