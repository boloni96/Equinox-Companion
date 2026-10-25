using EquinoxCompanion;

static class AutoRetainerPauseTests
{
    public static void Run(Action<string,bool> check,DateTimeOffset now)
    {
        var api=new FakeAutoRetainer();var pause=new SellerAutoRetainerPause(api);
        api.Loaded=false;check("without AutoRetainer Seller can start",pause.Acquire(now).Ready&&api.Writes.Count==0&&!pause.Held);
        api.Loaded=true;api.Busy=true;
        check("busy AutoRetainer is suppressed before waiting",!pause.Acquire(now).Ready&&pause.Held&&api.Suppressed&&api.Calls.IndexOf("set:true")<api.Calls.IndexOf("busy"));
        api.Busy=false;check("idle needs settling before Seller",!pause.Check(now.AddSeconds(1)).Ready&&pause.Check(now.AddSeconds(2)).Ready);
        pause.Release(now.AddSeconds(3));check("previous running state restored",!api.Suppressed&&!pause.Held&&!pause.RestorePending);
        api.Suppressed=true;api.Writes.Clear();pause.Acquire(now.AddSeconds(4));pause.Release(now.AddSeconds(5));
        check("pre-existing AutoRetainer pause stays paused",api.Suppressed&&api.Writes.Count==0&&!pause.Held);
        api.Suppressed=false;api.ThrowAfterPause=true;
        check("partial IPC failure blocks Seller",pause.Acquire(now.AddSeconds(6)).Error.Length>0&&pause.Held&&api.Suppressed);
        pause.Release(now.AddSeconds(7));check("partially applied pause is restored",!api.Suppressed&&!pause.Held);
        api.ThrowAfterPause=false;pause.Acquire(now.AddSeconds(8));api.FailRelease=true;pause.Release(now.AddSeconds(9));
        check("failed restoration remains tracked",pause.Held&&pause.RestorePending&&api.Suppressed);
        check("unrestored pause cannot be overwritten by new run",pause.Acquire(now.AddSeconds(9)).Error.Length>0);
        api.FailRelease=false;pause.Release(now.AddSeconds(11));check("temporary restoration failure is retried",!pause.Held&&!pause.RestorePending&&!api.Suppressed);
        pause.Acquire(now.AddSeconds(12));api.Suppressed=false;
        check("external resume stops handoff",pause.Check(now.AddSeconds(13)).Error.Length>0);
        var writes=api.Writes.Count;pause.Release(now.AddSeconds(14));check("external resume is left alone",api.Writes.Count==writes&&!pause.Held);
        api.Busy=false;pause.Acquire(now.AddSeconds(15));api.FailRead=true;
        check("unverifiable pause never reports ready",!pause.Check(now.AddSeconds(16)).Ready);
        api.FailRead=false;pause.Release(now.AddSeconds(17));

        var port=new PausePort(now);var run=new SellerRun(port);
        port.Api.Busy=true;port.Pause.Acquire(now);run.Start([new(1,"Retainer")],100,20,false,new(),[],now);
        for(var i=1;i<5;i++){port.Now=now.AddSeconds(i);run.Tick(port.Now);}
        check("waiting handoff performs no native Seller actions",run.Running&&run.State==SellerRun.Phase.Prepare&&port.NativeActions==0);
        port.Api.Busy=false;port.Now=now.AddSeconds(5);run.Tick(port.Now);port.Now=now.AddSeconds(6);run.Tick(port.Now);
        check("settled handoff advances without another start click",run.State==SellerRun.Phase.Select&&port.NativeActions==0);
        port.Now=now.AddSeconds(7);run.Tick(port.Now);check("Seller starts only after handoff",port.NativeActions==1&&port.Api.Suppressed);
        run.Stop("cancel");check("stop restores pause through runner release",!port.Api.Suppressed&&port.Releases==1);

        port=new(now);run=new(port);port.Api.Busy=true;port.Pause.Acquire(now);run.Start([new(1,"Retainer")],100,20,true,new(),[],now);
        for(var i=1;i<50;i++){port.Now=now.AddSeconds(i);run.Tick(port.Now);}
        check("busy timeout restores without touching native UI",!run.Running&&!port.Api.Suppressed&&port.NativeActions==0&&port.Releases==1);
        port=new(now);run=new(port);port.Api.Busy=true;port.Pause.Acquire(now);run.Start([new(1,"Retainer")],100,20,true,new(),[],now);port.Context=false;run.Tick(now.AddSeconds(1));
        check("character change during wait restores AutoRetainer",!run.Running&&!port.Api.Suppressed&&port.NativeActions==0);
        port=new(now);run=new(port);port.Api.Busy=true;port.Pause.Acquire(now);run.Start([new(1,"Retainer")],100,20,false,new(),[],now);run.Stop("cancel waiting");
        check("cancel while waiting restores without menu actions",!port.Api.Suppressed&&port.NativeActions==0&&port.Releases==1);
        port=new(now);run=new(port);port.Pause.Acquire(now);run.Start([new(1,"Retainer")],100,20,false,new(),[],now);
        for(var i=1;i<15;i++){port.Now=now.AddSeconds(i);run.Tick(port.Now);}
        check("normal completion restores AutoRetainer",!run.Running&&!port.Api.Suppressed&&port.Releases==1);
    }
    private sealed class FakeAutoRetainer:ISellerAutoRetainer
    {
        public bool Loaded=true,Suppressed,Busy,ThrowAfterPause,FailRelease,FailRead;
        public List<bool> Writes=[];public List<string> Calls=[];
        public bool Available=>Loaded;
        public bool GetSuppressed(){Calls.Add("get");if(FailRead)throw new InvalidOperationException();return Suppressed;}
        public void SetSuppressed(bool value){Calls.Add("set:"+value.ToString().ToLowerInvariant());Writes.Add(value);if(!value&&FailRelease)throw new InvalidOperationException();Suppressed=value;if(value&&ThrowAfterPause)throw new InvalidOperationException();}
        public bool IsBusy(){Calls.Add("busy");return Busy;}
    }
    private sealed class PausePort:ISellerPort
    {
        public DateTimeOffset Now;public bool Context=true;public int NativeActions,Releases;
        public readonly FakeAutoRetainer Api=new();public readonly SellerAutoRetainerPause Pause;
        public PausePort(DateTimeOffset now){Now=now;Pause=new(Api);}
        public SellerPreparation Prepare()=>Pause.Check(Now);
        public bool ContextMatches(ulong c,uint w)=>Context;
        public bool SelectRetainer(SellerRetainerRef r){NativeActions++;return true;}
        public bool OpenMarket(ulong r){NativeActions++;return true;}
        public MarketStock[]? ReadStock(ulong r)=>[];
        public bool OpenEditor(ulong r,MarketStock i)=>throw new Exception("Unexpected editor");
        public bool EditorMatches(ulong r,MarketStock i)=>false;
        public bool BeginComparison(uint i)=>throw new Exception("Unexpected comparison");
        public SellerComparison? Comparison=>null;
        public bool ApplyPrice(ulong r,MarketStock i,int p)=>throw new Exception("Unexpected price write");
        public bool PriceConfirmed(ulong r,MarketStock i,int p)=>false;
        public bool CancelEditor(ulong r,MarketStock i)=>true;
        public bool CloseRetainer(ulong r)=>true;
        public void Review(SellerRetainerRef r,MarketStock i,SellerDecision d,bool applied){}
        public void Release(){Releases++;Pause.Release(Now);}
    }
}
