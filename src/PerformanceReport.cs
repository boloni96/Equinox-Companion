using System.Diagnostics;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly PerformanceHitchHistory performanceHitches=new();
    private readonly RollingPerformance frameworkIntervals=new();
    private long performanceFrame,performanceContextFrame=-1;
    private double previousFrameworkClock;
    private object? performanceHitchContext;
    private readonly DateTimeOffset performanceStartedAt=DateTimeOffset.UtcNow;
    private DateTimeOffset performanceResetAt=DateTimeOffset.UtcNow;
    private void BeginPerformanceFrame()
    {
        performanceFrame++;
        var clock=PerformanceClock;
        if(previousFrameworkClock>0)frameworkIntervals.Record("Whole-game framework interval",(clock-previousFrameworkClock)*1000,clock);
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
        var work=CurrentIndicatorWork();var open=new List<string>();
        void Window(string name,bool shown){if(shown)open.Add(name);}
        Window("Companion",mainWindow?.IsOpen==true);Window("Gardening",plantingWindow?.IsOpen==true);
        Window("Adventure",adventureWindow?.IsOpen==true);Window("Organizer",retrieverWindow?.IsOpen==true);
        Window("Market",equinoxMarketWindow?.IsOpen==true);Window("Seller",sellerWindow?.IsOpen==true);
        Window("Gil Trader",gilTraderWindow?.IsOpen==true);Window("Helper",companionHelperWindow?.IsOpen==true);
        Window("Testing",testingWindow?.IsOpen==true);Window("Registration",registrationWindow?.IsOpen==true);
        Window("Housing bids",bidWindow?.IsOpen==true);Window("Item search",itemSearchWindow?.IsOpen==true);
        Window("Price search",marketWindow?.IsOpen==true);Window("Fashion",fashionWindow?.IsOpen==true);
        static string Brief(string? s)=>s is null?"":s.Length<=240?s:s[..240];
        return new {
            loggedIn=Player.IsLoaded,territory=Client.TerritoryType,section=config.CompanionSection,openWindows=open.ToArray(),
            activity=work?.Label??"Idle",progress=work?.Count??"",detail=Brief(work?.Detail),
            celebrationChecking=celebrationEventWork!=null,celebrationStatus=Brief(eventCelebrationStatus),celebrationLastTrigger,celebrationLastTriggerAt,
            cofferAutoStatus=Brief(cofferAutoStatus),cofferStatus=Brief(cofferStatus),
            gardenCare=gardenCareRun?.Running==true,seller=sellerRun?.Running==true,organizer=organizeRunning,
            keyboundBuying,followArmed=followSession.Armed,
            savePending=configurationDirty,saveWorkerBusy=configurationWriter?.Busy==true,
            syncInFlight=syncTask!=null,syncStatus=Brief(syncStatus),pendingSyncRecords,
            managedBytes=GC.GetTotalMemory(false),allocatedBytes=GC.GetTotalAllocatedBytes(false),
            gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)
        };
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
            slowStageHistory=performanceHitches.Snapshot(clock),totalSlowStageSamples=performanceHitches.TotalRecorded,
            wholeGameFrameIntervals=frameworkIntervals.Snapshot(clock),current=CapturePerformanceContext(),
            dataSizes=new {discoveries=config.Discoveries.Count,houses=config.Houses.Count,planting=config.Planting.Count,tending=config.Tending.Count,
                people=config.SharedRoster?.People.Length??0,characters=config.SharedRoster?.People.Sum(p=>p.Characters.Length)??0,
                itemLocations=config.SharedRoster?.ItemLocations?.Length??0,adventures=adventureBoards.Length,
                observationIndexRecords=discoveryReads.IndexedRecords,observationIndexRevision=discoveryReads.Revision},
            features=new {config.SyncEnabled,config.SyncCharacterDetails,config.SyncCollections,config.EnableFollowThem,config.EnableRetriever,
                config.EnableGilTrader,config.EnableCofferMarkers,config.EnableKeyboundBrawler,config.WorkIndicatorEnabled},
            optionalTools=optionalToolFailures.ToDictionary(x=>x.Key,x=>x.Value),
            runtime=new {version=Environment.Version.ToString(),os=Environment.OSVersion.VersionString,processors=Environment.ProcessorCount,is64Bit=Environment.Is64BitProcess}
        };
    }
    private void ResetPerformanceReport()
    {
        rollingPerformance.Clear();frameworkIntervals.Clear();performanceHitches.Clear();performanceStages.Clear();
        performancePanelRows=[];nextPerformancePanelRead=0;maxDrawMs=maxUpdateMs=maxSnapshotMs=0;
        previousFrameworkClock=0;performanceContextFrame=-1;performanceHitchContext=null;performanceResetAt=DateTimeOffset.UtcNow;
    }
}
