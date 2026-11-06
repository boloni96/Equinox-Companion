using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string eventCelebrationStatus="Waiting for account completion data.";
    private (string Identity,string Account)? completedEventCelebration;
    private string EventEvidenceKey(SharedCharacter c)=>CelebrationKey(config.PairingKey+"|keybound-5466|"+c.Id);
    private static bool CelebrationActorMatches(Actor actor,SharedCharacter c)=>
        !string.IsNullOrWhiteSpace(c.ContentId)?actor.ContentId==c.ContentId:
        string.Equals(actor.Name,c.Name,StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(c.World)&&string.Equals(actor.HomeWorldName,c.World,StringComparison.OrdinalIgnoreCase);
    private static bool CelebrationStorage(string key)=>key.StartsWith("bag:")||key.StartsWith("retainer:")||key is "armoire" or "dresser";
    private static unsafe bool CelebrationLiveQuest()=>QuestManager.IsQuestComplete(QuestToolsPolicy.KeywardBound);
    private unsafe bool CelebrationLiveReward(uint item)
    {
        // Check bag ownership first. Avoid expensive unlock queries for owned items.
        if(ArmoireInventory(item).Held>0)return true;
        var state=UIState.Instance();
        if(state==null||!state->PlayerState.IsLoaded)return false;
        var row=ExdModule.GetItemRowById(item);
        return row!=null&&state->IsItemActionUnlocked(row)==1;
    }
    private IEnumerable<bool> ReadEventCelebrationEvidence(SharedCharacter c,SharedCharacter current,AdventureTask[] tasks,Action<AccountEventEvidence> receive)
    {
        var key=EventEvidenceKey(c);var saved=config.EventCelebrationEvidence.GetValueOrDefault(key);
        var rewards=celebrationOffers!.Select(o=>o.Item).ToHashSet();
        var history=celebrationHistory.Read(c);
        var items=(saved?.Items??[]).ToHashSet();items.UnionWith(history.Items);
        var quest=saved?.QuestComplete==true||history.QuestComplete||(c.Id==current.Id&&CelebrationLiveQuest());
        yield return true;
        foreach(var task in tasks){
            if(!quest&&task.Done&&task.ServerConfirmed&&AdventurePolicy.TaskCharacterId(task,[c])==c.Id)quest=true;
            yield return true;
        }
        foreach(var location in config.SharedRoster?.ItemLocations??[]){
            yield return true;
            if(location.OwnerId!=c.Id||!CelebrationStorage(location.Key))continue;
            foreach(var item in location.Items){if(rewards.Contains(item))items.Add(item);yield return true;}
        }
        if(ulong.TryParse(c.ContentId,out var cid))foreach(var receipt in keyboundPurchases!.Entries.ToArray()){
            if(receipt.Character==cid&&receipt.Confirmed&&rewards.Contains(receipt.Item))items.Add(receipt.Item);
            yield return true;
        }
        if(c.Id==current.Id)foreach(var offer in celebrationOffers!){
            if(!items.Contains(offer.Item)&&CelebrationLiveReward(offer.Item))items.Add(offer.Item);
            yield return true;
        }
        var eventItems=items.Intersect(rewards).Order().ToArray();
        var evidence=new AccountEventEvidence(quest,eventItems);
        if((quest||eventItems.Length>0)&&(saved==null||saved.QuestComplete!=quest||!saved.Items.SequenceEqual(eventItems))){
            config.EventCelebrationEvidence=new(config.EventCelebrationEvidence){[key]=evidence};SaveConfiguration();
        }
        receive(evidence);
    }
    private void DrawEventCelebrationStatus()
    {
        if(!Dalamud.Bindings.ImGui.ImGui.CollapsingHeader("Completion celebration"))return;
        Dalamud.Bindings.ImGui.ImGui.TextWrapped(eventCelebrationStatus);
        if(completedEventCelebration is {} complete&&Dalamud.Bindings.ImGui.ImGui.Button("Show Good Job again"))
            QueueAccountCelebration(complete.Identity,complete.Account,"Keybound Brawler + reward list",true);
    }
}
