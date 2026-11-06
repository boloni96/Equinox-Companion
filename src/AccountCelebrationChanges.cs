using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly CelebrationCheckGate celebrationChecks=new();
    private IEnumerator<bool>? celebrationEventWork;
    private string celebrationWorkPairing="";
    private long celebrationEvidenceRevision;
    private string celebrationLastTrigger="Startup";
    private DateTimeOffset celebrationLastTriggerAt;
    private ulong celebrationWorkCharacter;
    private void StopCelebrationCheck(){celebrationEventWork?.Dispose();celebrationEventWork=null;}
    private void AdvanceCelebrationCheck()
    {
        if(celebrationEventWork==null)return;
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try{if(!CelebrationWorkBudget.Advance(celebrationEventWork))StopCelebrationCheck();}
        catch(Exception e){StopCelebrationCheck();eventCelebrationStatus="Completion check is waiting: "+e.Message;}
        finally{RecordPerformanceElapsed("Celebration: event check slice",started);}
    }
    private SharedRoster? celebrationRoster;
    private AdventureBoard[]? celebrationBoards;
    private int? celebrationRosterStamp,celebrationBoardStamp;
    private ulong celebrationCharacter;
    private DateTimeOffset celebrationFashionCycle;
    private bool celebrationFashionOpen;
    private void RequestCelebrationCheck(string reason="Completion request"){celebrationLastTrigger=reason;celebrationLastTriggerAt=DateTimeOffset.UtcNow;celebrationEvidenceRevision++;celebrationChecks.Request(celebrationLastTriggerAt);}
    private void UpdateCelebrationChecks(DateTimeOffset now)
    {
        ResetCelebrationScope();
        var characterId=Player.IsLoaded?Player.ContentId:0;
        var cycle=FashionCompletion.Cycle(now);
        var fashionOpen=FashionCompletion.IsOpen(now);
        if(characterId!=celebrationCharacter||cycle!=celebrationFashionCycle||fashionOpen!=celebrationFashionOpen){
            celebrationCharacter=characterId;celebrationFashionCycle=cycle;celebrationFashionOpen=fashionOpen;RequestCelebrationCheck("Character or Fashion cycle changed");
        }
        // Roster refresh timestamps, prices, house timers and presence are not
        // completion evidence. Inspect the relevant fields only when inputs change.
        if(!ReferenceEquals(celebrationRoster,config.SharedRoster)){
            celebrationRoster=config.SharedRoster;var hash=new HashCode();
            foreach(var person in celebrationRoster?.People??[]){
                hash.Add(person.Id);hash.Add(person.Name);
                foreach(var c in person.Characters){
                    hash.Add(c.Id);hash.Add(c.ContentId);hash.Add(c.Name);hash.Add(c.World);hash.Add(c.AccountId);hash.Add(c.Account);
                    hash.Add(c.FashionCompletedAt);hash.Add(c.FashionScore);hash.Add(c.FashionCycle);hash.Add(c.FashionObservedAt);
                    foreach(var wish in c.KeyboundWishlist??[]){hash.Add(wish.Name);hash.Add(wish.Wanted);hash.Add(wish.Obtained);}
                }
            }
            foreach(var location in celebrationRoster?.ItemLocations??[]){
                if(!(location.Key.StartsWith("bag:")||location.Key.StartsWith("retainer:")||location.Key is "armoire" or "dresser"))continue;
                hash.Add(location.OwnerId);hash.Add(location.Key);
                foreach(var item in location.Items)hash.Add(item);
            }
            var stamp=hash.ToHashCode();
            if(celebrationRosterStamp!=stamp){celebrationRosterStamp=stamp;RequestCelebrationCheck("Journal completion evidence changed");}
        }
        if(!ReferenceEquals(celebrationBoards,adventureBoards)){
            celebrationBoards=adventureBoards;var hash=new HashCode();
            foreach(var board in adventureBoards)foreach(var stop in board.Stops)foreach(var task in stop.Tasks){
                if(!AdventurePolicy.IsFashion(task)&&!(task.Rule?.Kind=="event"&&task.Rule.Quests?.Any(q=>QuestToolsPolicy.ShortId(q)==QuestToolsPolicy.KeywardBound)==true))continue;
                hash.Add(task.Id);hash.Add(task.Done);hash.Add(task.ServerConfirmed);hash.Add(task.Rule?.Cid);hash.Add(task.Label);
            }
            var stamp=hash.ToHashCode();
            if(celebrationBoardStamp!=stamp){celebrationBoardStamp=stamp;RequestCelebrationCheck("Adventure completion evidence changed");}
        }
        if(celebrationEventWork!=null&&(celebrationWorkCharacter!=characterId||celebrationWorkPairing!=config.PairingKey))StopCelebrationCheck();
        var ready=characterId!=0&&!keyboundBuying&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
        if(celebrationEventWork==null&&celebrationChecks.Take(now,ready)){
            ObserveSafely("Celebration: fashion check",ObserveAccountFashionCompletion);
            celebrationWorkCharacter=characterId;celebrationWorkPairing=config.PairingKey;
            celebrationEventWork=ObserveEventAccountCompletion(celebrationEvidenceRevision).GetEnumerator();
        }
        if(ready)AdvanceCelebrationCheck();
    }
}
