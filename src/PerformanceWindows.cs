namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<(string Name,string Phase),(string Label,long Frame)> measuredWindows=[];
    private string measuredWindow="",measuredOperation="",measuredWindowPhase="";
    private bool measuredWindowOpening;
    private void MeasureWindow(string name,Action draw,string phase="Draw")
    {
        var previous=measuredWindow;var previousOpening=measuredWindowOpening;var previousPhase=measuredWindowPhase;
        var known=measuredWindows.TryGetValue((name,phase),out var cached);
        var label=known?cached.Label:"Window: "+name.Split("###")[0].TrimStart('#')+(phase=="Draw"?"":" · "+phase);
        measuredWindow=name;measuredWindowPhase=phase;measuredWindowOpening=phase=="OnOpen"||(phase=="Draw"&&(!known||cached.Frame<performanceFrame-1));
        measuredWindows[(name,phase)]=(label,performanceFrame);
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try{draw();}finally{
            performanceContextFrame=-1;RecordPerformanceElapsed(label,started);
            measuredWindow=previous;measuredWindowOpening=previousOpening;measuredWindowPhase=previousPhase;
        }
    }
    private void MeasureWork(string stage,Action work)
    {
        var previous=measuredOperation;measuredOperation=stage;
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try{work();}finally{
            performanceContextFrame=-1;RecordPerformanceElapsed(stage,started);measuredOperation=previous;
        }
    }
}
