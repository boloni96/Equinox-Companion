using EquinoxCompanion;
internal static class BackgroundDisplayTests
{
    private sealed record Result(int Version);
    internal static void Run()
    {
        var cache=new BackgroundDisplayProjection<Result>();var source=new object();
        var release=new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(cache.Get(source,"old",()=>()=>release.Task.GetAwaiter().GetResult())!=null)throw new Exception("Cold view must wait without blocking.");
        if(cache.Get(source,"new",()=>()=>new Result(2))!=null)throw new Exception("In-flight work must not expose stale data.");
        release.SetResult(new Result(1));Result? result=null;
        if(!SpinWait.SpinUntil(()=>{result=cache.Get(source,"new",()=>()=>new Result(2));return result!=null;},5000)||result!.Version!=2)throw new Exception("Superseded worker result must be discarded and latest snapshot rebuilt.");
        if(cache.Get(source,"new",()=>throw new Exception("Unchanged view rebuilt"))?.Version!=2)throw new Exception("Completed view must be cached.");
        var now=DateTimeOffset.UtcNow;var first=new Actor("42","Old",1,1,"World");
        var last=first with {Name="Current"};
        var data=LocalHousingProjection.Build([new("old","storage.observed",now,first,null),new("new","character.observed",now.AddSeconds(1),last,null)],[],[],[]);
        if(data.Actors.Length!=1||data.Actors[0].Name!="Current")throw new Exception("Local housing must retain the latest character identity.");
        Console.WriteLine("PASS438 nonblocking display preparation, stale snapshot rejection, reuse and character identity");
    }
}
