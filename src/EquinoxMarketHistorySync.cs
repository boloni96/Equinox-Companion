namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly MarketHistoryClient marketHistoryClient=new();
    private readonly CancellationTokenSource marketHistoryCancel=new();
    private SharedMarketHistoryState marketSharedHistory=SharedMarketHistoryState.Empty("");
    private LatestSnapshotWriter<SharedMarketHistoryState> marketSharedWriter=null!;
    private Task<SharedMarketHistoryState>? marketSharedLoad;
    private Task<SharedMarketHistoryResult>? marketSharedTask;
    private string marketSharedKey="",marketSharedRequestKey="",marketSharedStatus="Waiting for paired Market history.";
    private DateTimeOffset marketSharedNext;
    private Task<MarketLedger>? marketViewTask;
    private MarketLedger? marketView;
    private MarketIncome[]? marketViewIncome;
    private MarketExpense[]? marketViewExpenses;
    private MarketPriceLog[]? marketViewPrices;
    private MarketHistoryRecord[]? marketViewRemote;
    private string marketViewKey="",marketViewCharacters="";
    private MarketLedger MarketHistoryLedger=>marketView??marketLedger;
    private void InitializeMarketHistorySync()
    {
        var path=Path.Combine(Pi.GetPluginConfigDirectory(),"market-shared-history.json");
        marketSharedWriter=new(state=>{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",System.Text.Json.JsonSerializer.Serialize(state,MarketHistorySharing.Json));File.Move(path+".tmp",path,true);});
        marketSharedLoad=Task.Run(()=>{
            if(!File.Exists(path))return SharedMarketHistoryState.Empty("");
            var saved=System.Text.Json.JsonSerializer.Deserialize<SharedMarketHistoryState>(File.ReadAllText(path),MarketHistorySharing.Json);
            return saved is {Records:not null,Sent:not null,Cursor:>=0}&&saved.Records.All(MarketHistorySharing.Valid)?saved:throw new IOException("Invalid shared Market cache");
        });
    }
    private void UpdateMarketHistorySync(DateTimeOffset now)
    {
        if(marketSharedLoad is {IsCompleted:true}){
            if(marketSharedLoad.IsCompletedSuccessfully)marketSharedHistory=marketSharedLoad.Result;
            else{_ = marketSharedLoad.Exception;marketSharedStatus="Shared Market cache could not be read; downloading a fresh copy.";}
            marketSharedLoad=null;
        }
        if(marketSharedLoad!=null)return;
        var enabled=config.SyncEnabled&&config.Seller.SyncToJournal&&config.PairingKey.Length==64;
        var key=enabled?config.PairingKey:"";
        if(marketSharedKey!=key){marketSharedKey=key;marketView=null;marketViewRemote=null;marketSharedNext=default;
            if(marketSharedHistory.Scope!=MarketHistorySharing.Hash(key))marketSharedHistory=SharedMarketHistoryState.Empty(MarketHistorySharing.Hash(key));}
        if(marketSharedTask is {IsCompleted:true}){
            if(marketSharedTask.IsCompletedSuccessfully&&marketSharedRequestKey==key){var result=marketSharedTask.Result;marketSharedHistory=result.State;marketSharedStatus=result.Status;marketSharedNext=now.AddSeconds(result.More?2:30);if(result.Changed)marketSharedWriter.Enqueue(marketSharedHistory);}
            else{_ = marketSharedTask.Exception;marketSharedNext=now.AddSeconds(30);}
            marketSharedTask=null;
        }
        if(marketViewTask is {IsCompleted:true}){
            if(marketViewTask.IsCompletedSuccessfully&&marketViewKey==key)marketView=marketViewTask.Result;
            else _=marketViewTask.Exception;
            marketViewTask=null;
        }
        var characters=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Select(c=>c.ContentId).Where(c=>c.Length>0).Distinct().ToArray();
        var characterScope=string.Join(",",characters.Order());
        var remote=enabled?marketSharedHistory.Records:Array.Empty<MarketHistoryRecord>();
        if(marketViewTask==null&&(marketViewCharacters!=characterScope||!ReferenceEquals(marketViewIncome,marketLedger.Income)||!ReferenceEquals(marketViewExpenses,marketLedger.Expenses)||!ReferenceEquals(marketViewPrices,marketLedger.Prices)||!ReferenceEquals(marketViewRemote,remote))){
            marketViewIncome=marketLedger.Income;marketViewExpenses=marketLedger.Expenses;marketViewPrices=marketLedger.Prices;marketViewRemote=remote;marketViewKey=key;
            var local=marketLedger;marketViewCharacters=characterScope;
            marketViewTask=Task.Run(()=>MarketHistorySharing.View(local,remote.Where(r=>characters.Contains(r.CharacterId)).ToArray()));
        }
        if(!enabled||config.SharedRoster is not {MarketProjectionVersion:>=1} roster||marketSharedTask!=null||now<marketSharedNext||syncTask!=null||readySyncRecords>0)return;
        marketSharedNext=now.AddSeconds(30);marketSharedRequestKey=key;
        var state=marketSharedHistory;var snapshot=marketLedger;
        marketSharedTask=Task.Run(()=>marketHistoryClient.Sync(key,state,snapshot,characters,marketHistoryCancel.Token));
    }
    private void DisposeMarketHistorySync(){marketHistoryCancel.Cancel();marketHistoryClient.Dispose();marketHistoryCancel.Dispose();}
}
