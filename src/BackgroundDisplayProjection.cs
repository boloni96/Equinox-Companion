namespace EquinoxCompanion;
// Snapshot factories run on the caller; only the returned managed calculation runs on a worker.
public sealed class BackgroundDisplayProjection<T> where T:class
{
    private object? source;
    private bool initialized;
    private string revision="";
    private Task<(T Value,double Ms)>? task;
    private T? value;
    public double BuildMs {get;private set;}
    public bool Busy=>task is {IsCompleted:false};
    public string Error {get;private set;}="";
    public T? Get(object? currentSource,string currentRevision,Func<Func<T>> snapshot)
    {
        if(task is {} running){
            if(!running.IsCompleted)return null;
            task=null;
            if(running.IsCompletedSuccessfully){value=running.Result.Value;BuildMs=running.Result.Ms;}
            else{_=running.Exception;value=null;Error="Could not prepare this view. Refresh or reopen after the data changes.";}
        }
        if(initialized&&ReferenceEquals(source,currentSource)&&revision==currentRevision)return value;
        initialized=true;source=currentSource;revision=currentRevision;value=null;Error="";
        var calculate=snapshot();
        task=Task.Run(()=>{
            var started=System.Diagnostics.Stopwatch.GetTimestamp();
            return (calculate(),System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        });
        return null;
    }
}
