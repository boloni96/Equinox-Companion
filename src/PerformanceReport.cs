using System.Diagnostics;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly PerformanceHitchHistory performanceHitches=new();
    private readonly RollingPerformance frameworkIntervals=new();
    private readonly PerformanceHitchHistory frameworkHitches=new();
    private long performanceFrame,performanceContextFrame=-1;
    private double previousFrameworkClock;
    private object? performanceHitchContext;
    private readonly DateTimeOffset performanceStartedAt=DateTimeOffset.UtcNow;
    private DateTimeOffset performanceResetAt=DateTimeOffset.UtcNow;
    private void BeginPerformanceFrame()
    {
        performanceFrame++;
        var clock=PerformanceClock;
        if(previousFrameworkClock>0){
            var elapsed=(clock-previousFrameworkClock)*1000;
            frameworkIntervals.Record("Whole-game framework interval",elapsed,clock);
            frameworkHitches.Record(performanceFrame,"Whole-game framework interval",elapsed,clock,DateTimeOffset.UtcNow,null);
        }
        previousFrameworkClock=clock;
    }
    private void RecordPerformanceSample(string stage,double milliseconds)
    {
        var clock=PerformanceClock;rollingPerformance.Record(stage,milliseconds,clock);
        if(!double.IsFinite(milliseconds)||milliseconds<PerformanceHitchHistory.ThresholdMs)return;
        // Context is captured only on slow frames, at most once plus the root update/draw.
        if(performanceContextFrame!=performanceFrame||stage is "Update" or "Draw"){
            performanceContextFrame=performanceFrame;
            try{performanceHitchContext=CapturePerformanceContext();}
            catch{performanceHitchContext=null;} // Diagnostics must never stop the plugin.
        }
        performanceHitches.Record(performanceFrame,stage,milliseconds,clock,DateTimeOffset.UtcNow,performanceHitchContext);
    }
    private object CapturePerformanceContext()
    {
        var work=CurrentIndicatorWork();
        var open=windows.Windows.Concat(welcomeWindows.Windows).Concat(adventureCutsceneWindows.Windows)
            .Where(w=>w.IsOpen).Select(w=>w.WindowName).Distinct().ToArray();
        static string Brief(string? s)=>s is null?"":s.Length<=240?s:s[..240];
        return new {
            loggedIn=Player.IsLoaded,territory=Client.TerritoryType,section=config.CompanionSection,openWindows=open,window=measuredWindow,windowPhase=measuredWindowPhase,windowOpening=measuredWindowOpening,operation=measuredOperation,
            activity=work?.Label??"Idle",progress=work?.Count??"",detail=Brief(work?.Detail),
            celebrationChecking=celebrationEventWork!=null,celebrationStatus=Brief(eventCelebrationStatus),celebrationLastTrigger,celebrationLastTriggerAt,
            cofferAutoStatus=Brief(cofferAutoStatus),cofferStatus=Brief(cofferStatus),
            gardenCare=gardenCareRun?.Running==true,seller=sellerRun?.Running==true,organizer=organizeRunning,
            keyboundBuying,keyboundPlanDataPending,keyboundPlanWaiting,keyboundStartPending=keyboundStartRequest!=null,followArmed=followSession.Armed,
            saveWarmupPending=configurationWarmup is {IsCompleted:false},savePending=configurationDirty,saveWorkerBusy=configurationWriter?.Busy==true,
            syncInFlight=syncTask!=null,syncStatus=Brief(syncStatus),pendingSyncRecords,
            managedBytes=GC.GetTotalMemory(false),allocatedBytes=GC.GetTotalAllocatedBytes(false),
            gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)
        };
    }
    private T CaptureDiagnosticState<T>(string stage,Func<T> capture)
    {
        var started=Stopwatch.GetTimestamp();
        try{return capture();}
        finally{RecordPerformanceElapsed("Export: "+stage,started);}
    }
    private object CapturePerformanceReport()
    {
        var clock=PerformanceClock;var recent=rollingPerformance.Snapshot(clock);
        return new {
            schemaVersion=1,capturedAtUtc=DateTimeOffset.UtcNow,pluginVersion=typeof(Plugin).Assembly.GetName().Version?.ToString(),
            startedAtUtc=performanceStartedAt,resetAtUtc=performanceResetAt,framesObserved=performanceFrame,
            units="milliseconds",recentWindowSeconds=60,hitchThresholdMs=PerformanceHitchHistory.ThresholdMs,
            hitchCapacity=PerformanceHitchHistory.Capacity,hitchRetentionSeconds=PerformanceHitchHistory.RetentionSeconds,
            notes="Stage timings overlap: do not add nested stages. Frame IDs correlate samples from the same framework update/draw. Whole-game intervals include game work, other plugins, loading and frame limiting; they do not attribute blame to Companion. GC/memory counters are process-wide. Histories are bounded; exports contain no pairing key.",
            maxDrawMs,maxUpdateMs,maxSnapshotMs,configurationError,
            stages=performanceStages.ToDictionary(x=>x.Key,x=>x.Value),recent,
            worstStageHistory=performanceHitches.WorstSnapshot(clock),worstWholeGameIntervals=frameworkHitches.WorstSnapshot(clock),
            slowStageHistory=performanceHitches.Snapshot(clock),totalSlowStageSamples=performanceHitches.TotalRecorded,
            wholeGameFrameIntervals=frameworkIntervals.Snapshot(clock),current=CapturePerformanceContext(),
            dataSizes=new {discoveries=config.Discoveries.Count,houses=config.Houses.Count,planting=config.Planting.Count,tending=config.Tending.Count,
                people=config.SharedRoster?.People.Length??0,characters=config.SharedRoster?.People.Sum(p=>p.Characters.Length)??0,
                itemLocations=config.SharedRoster?.ItemLocations?.Length??0,adventures=adventureBoards.Length,
                housingHistoryBuildMs=housingHistoryProjection.BuildMs,housingHistoryInFlight=housingHistoryProjection.Busy,submarineHistoryBuildMs=submarineDisplayCache.BuildMs,submarineHistoryInFlight=submarineDisplayCache.Busy,keyboundStorageInFlight=keyboundStorageTask is {IsCompleted:false},keyboundStorageCount,gardenNoticeBuildMs,gardenNoticeInFlight=gardenNoticeTask is {IsCompleted:false},celebrationHistoryRecords=celebrationHistory.IndexedRecords,celebrationHistoryBuildMs,celebrationHistoryInFlight=celebrationHistoryTask is {IsCompleted:false},
                observationIndexRecords=discoveryReads.IndexedRecords,observationIndexRevision=discoveryReads.Revision},
            features=new {config.SyncEnabled,config.SyncCharacterDetails,config.SyncCollections,config.EnableFollowThem,config.EnableRetriever,
                config.EnableGilTrader,config.EnableCofferMarkers,config.EnableKeyboundBrawler,config.WorkIndicatorEnabled},
            optionalTools=optionalToolFailures.ToDictionary(x=>x.Key,x=>x.Value),
            runtime=new {version=Environment.Version.ToString(),os=Environment.OSVersion.VersionString,processors=Environment.ProcessorCount,is64Bit=Environment.Is64BitProcess}
        };
    }
    private void ResetPerformanceReport()
    {
        rollingPerformance.Clear();frameworkIntervals.Clear();frameworkHitches.Clear();performanceHitches.Clear();performanceStages.Clear();
        performancePanelRows=[];nextPerformancePanelRead=0;maxDrawMs=maxUpdateMs=maxSnapshotMs=0;
        previousFrameworkClock=0;performanceContextFrame=-1;performanceHitchContext=null;performanceResetAt=DateTimeOffset.UtcNow;
    }
}
