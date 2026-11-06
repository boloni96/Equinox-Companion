using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nextGardenNotice;
    private readonly GardenNoticeSession gardenNotices=new();
    private Task<((string Text,ushort Color)[] Messages,double Ms)>? gardenNoticeTask;
    private string gardenNoticeInput="";
    private SharedRoster? gardenNoticeRoster;
    private bool gardenNoticePending;
    private long gardenNoticeGeneration, gardenNoticeTaskGeneration;
    private double gardenNoticeBuildMs;
    private string GardenNoticeInput=>$"{Player.ContentId}:{config.PairingKey}:{config.NotifyGardenCare}:{config.NotifyGardenTending}:{config.NotifyGardenRisk}:{config.SyncEnabled}:{config.Planting.Count}:{config.Planting.LastOrDefault()?.EventId}:{config.Tending.Count}:{config.Tending.LastOrDefault()?.EventId}:{GardenMappingVersion}:{completedGardenPlans.Count}";
    private void OnGardenNoticeLogin(){gardenNotices.Login();gardenNoticeGeneration++;gardenNoticePending=false;}
    private void UpdateGardenCareNotices(DateTimeOffset now)
    {
        if(!Player.IsLoaded||Player.ContentId==0)return;
        if(gardenNotices.ObserveCharacter(Player.ContentId)){
            gardenNoticeGeneration++;gardenNoticePending=false;
            nextGardenNotice=now.AddSeconds(20);nextRosterRead=default;return;
        }
        if(gardenNoticeTask is {} task){
            if(!task.IsCompleted)return;
            gardenNoticeTask=null;
            if(task.IsCompletedSuccessfully){
                gardenNoticeBuildMs=task.Result.Ms;
                if(gardenNoticeTaskGeneration==gardenNoticeGeneration){
                    if(gardenNoticeInput==GardenNoticeInput&&ReferenceEquals(gardenNoticeRoster,config.SharedRoster)){
                        gardenNoticePending=false;
                        foreach(var message in task.Result.Messages)
                            PrintEquinox(new SeStringBuilder().AddText("[Equinox] ").AddUiForeground(message.Color).AddText(message.Text).AddUiForegroundOff().Build());
                    }else nextGardenNotice=now; // Changed care or character data must be recalculated.
                }
            }else{
                _=task.Exception;nextGardenNotice=now.AddSeconds(30);
                errorJournal.Record("garden-notice","Garden summary preparation failed; will retry.");
            }
        }
        if(!config.NotifyGardenCare||!config.SyncEnabled||now<nextGardenNotice||config.SharedRoster is not {} roster)return;
        nextGardenNotice=now.AddSeconds(30);
        if(roster.GardenCare is null||!gardenNoticePending&&!gardenNotices.TrySummary())return;
        gardenNoticePending=true;
        var planting=config.Planting.ToArray();var tending=config.Tending.ToArray();
        // Enrich only the small local-action set on the framework; the large history stays managed.
        var actors=new Dictionary<Actor,Actor>();var addresses=new Dictionary<Address,Address>();
        Actor ActorNames(Actor a){if(!actors.TryGetValue(a,out var named))actors[a]=named=WithWorldNames(a);return named;}
        Address AddressNames(Address a){if(!addresses.TryGetValue(a,out var named))addresses[a]=named=WithAddressNames(a);return named;}
        var local=planting.Select(p=>new SyncEvent(p.EventId,"garden.planted",p.ConfirmedAt,ActorNames(p.Actor),AddressNames(p.Address),p.Patch,p.Bed,p.Plant))
            .Concat(tending.Select(t=>new SyncEvent(t.EventId,"garden.tended",t.ConfirmedAt,ActorNames(t.Actor),AddressNames(t.Address),t.Patch,t.Bed))).ToArray();
        var mappings=(gardenBedSync?.Mappings??[]).Select(e=>e with {Address=AddressNames(e.Address!)}).ToArray();
        var history=config.Discoveries.ToArray();var completed=new Dictionary<string,DateTimeOffset>(completedGardenPlans);
        var path=Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art","catalog");
        var tend=config.NotifyGardenTending;var risk=config.NotifyGardenRisk;
        gardenNoticeInput=GardenNoticeInput;gardenNoticeRoster=roster;gardenNoticeTaskGeneration=gardenNoticeGeneration;
        gardenNoticeTask=Task.Run(()=>{
            var watch=System.Diagnostics.Stopwatch.GetTimestamp();
            var messages=GardenNoticeBuilder.Build(roster,planting,tending,history,local,mappings,completed,path,now,tend,risk);
            return (messages,System.Diagnostics.Stopwatch.GetElapsedTime(watch).TotalMilliseconds);
        });
    }
}
