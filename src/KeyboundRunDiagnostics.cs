using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly KeyboundRunHistory keyboundRunHistory=new();
    private readonly string keyboundTraceSession=Guid.NewGuid().ToString("N");
    private string keyboundBuyerRun="",keyboundTypingRun="",keyboundAccountRun="",keyboundTraceFingerprint="",keyboundTraceError="";
    private LatestSnapshotWriter<KeyboundRunEvent[]>? keyboundTraceWriter;
    private DateTimeOffset keyboundTraceNext;
    private void InitializeKeyboundRunHistory(){
        try{
            // Each game process owns its file; two clients must not overwrite each other.
            var path=Path.Combine(Pi.GetPluginConfigDirectory(),$"keybound-history-{Environment.ProcessId}.json");
            try { if(File.Exists(path)&&new FileInfo(path).Length<=4*1024*1024)
                keyboundRunHistory.Restore(JsonSerializer.Deserialize<KeyboundRunEvent[]>(File.ReadAllText(path))??[]);
            } catch(Exception e) { keyboundTraceError=e.GetType().Name;Log.Warning(e,"Previous Keybound history could not load"); }
            keyboundTraceWriter=new(snapshot=>{
                var temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(snapshot));File.Move(temporary,path,true);
            });
            TraceKeyboundRun("","plugin-loaded","Plugin loaded; previous history retained. Active runs are not resumed.");
        }catch(Exception e){keyboundTraceError=e.GetType().Name;Log.Warning(e,"Keybound history could not load");}
    }
    private void FlushKeyboundRunHistory(){
        try{keyboundTraceWriter?.Flush(keyboundRunHistory.Snapshot());}
        catch(Exception e){Log.Warning(e,"Keybound history could not flush");}
    }
    private void TraceKeyboundRun(string run,string kind,string status,string source="system",bool includeRewardState=true){
        try{
            var item=includeRewardState?(keyboundUsePending??keyboundBuyItem):null;int? held=null,unlocked=null;
            if(item!=null&&Player.IsLoaded){try{held=KeyboundBagCount(item.Item);unlocked=KeyboundUnlockState(item.Item);}catch{/* Loading is recorded as unknown. */}}
            var names=new[]{"SelectYesno","Talk","SelectString","SelectIconString","ShopExchangeCurrency","ShopExchangeItem","JournalResult","CharaCardDesignSetting","BannerEditor","BannerList"};
            var windows=names.Where(VisibleFollowAddon).ToArray();
            var flags=new[]{ConditionFlag.OccupiedInEvent,ConditionFlag.OccupiedInQuestEvent,ConditionFlag.Casting,ConditionFlag.Mounted,ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,ConditionFlag.InCombat}.Where(c=>Conditions[c]).Select(c=>c.ToString()).ToArray();
            keyboundRunHistory.Add(new(DateTimeOffset.UtcNow,keyboundTraceSession,run,source,kind,Player.IsLoaded?Player.ContentId:0,
                status.Length>1500?status[..1500]:status,item?.Item,item?.Name,includeRewardState&&keyboundUsePending!=null?keyboundUseBefore:null,held,unlocked,
                includeRewardState&&keyboundUsingRewards?keyboundUseDeadline:null,windows,flags));
            keyboundTraceWriter?.Enqueue(keyboundRunHistory.Snapshot());
        }catch(Exception e){keyboundTraceError=e.GetType().Name;Log.Debug(e,"Keybound history observation unavailable");}
    }
    private void ObserveKeyboundRunProgress(DateTimeOffset now){
        if(!keyboundBuying)return;
        var fingerprint=$"{keyboundUsingRewards}|{keyboundUsePending?.Item}|{keyboundBuyStatus}";
        if(fingerprint==keyboundTraceFingerprint&&now<keyboundTraceNext)return;
        var kind=fingerprint==keyboundTraceFingerprint?"still-waiting":"state";
        keyboundTraceFingerprint=fingerprint;keyboundTraceNext=now.AddSeconds(5);
        TraceKeyboundRun(keyboundBuyerRun,kind,keyboundBuyStatus);
    }
    private object KeyboundRunDiagnosticSnapshot()=>new {
        session=keyboundTraceSession,buyerRun=keyboundBuyerRun,typingRun=keyboundTypingRun,accountRun=keyboundAccountRun,
        pendingSince=keyboundUsePending==null?(DateTimeOffset?)null:keyboundUseDeadline.AddSeconds(-30),
        pendingDeadline=keyboundUsePending==null?(DateTimeOffset?)null:keyboundUseDeadline,
        history=keyboundRunHistory.Snapshot(),persistenceError=keyboundTraceWriter?.Failure?.GetType().Name??keyboundTraceError
    };
}
