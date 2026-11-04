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
    private unsafe AccountEventEvidence ReadEventCelebrationEvidence(SharedCharacter c,SharedCharacter current,IEnumerable<AdventureTask> tasks)
    {
        var key=EventEvidenceKey(c);var saved=config.EventCelebrationEvidence.GetValueOrDefault(key);
        var observations=DiscoveryReads().Kind("collection.observed").Where(e=>CelebrationActorMatches(e.Actor,c)&&e.Collection!=null).ToArray();
        var quest=saved?.QuestComplete==true||
            (c.Id==current.Id&&QuestManager.IsQuestComplete(QuestToolsPolicy.KeywardBound))||
            tasks.Any(t=>t.Done&&t.ServerConfirmed&&AdventurePolicy.TaskCharacterId(t,[c])==c.Id)||
            observations.Any(e=>e.Collection!.Category=="quest"&&e.Collection.Unlocked.Any(q=>QuestToolsPolicy.ShortId(q)==QuestToolsPolicy.KeywardBound));
        var items=(saved?.Items??[]).ToHashSet();
        foreach(var e in DiscoveryReads().Kind("storage.observed").Where(e=>CelebrationActorMatches(e.Actor,c)&&e.Storage!=null&&(e.Storage.Key.StartsWith("bag:")||e.Storage.Key.StartsWith("retainer:")||e.Storage.Key is "armoire" or "dresser")))items.UnionWith(e.Storage!.Items);
        items.UnionWith((config.SharedRoster?.ItemLocations??[]).Where(l=>l.OwnerId==c.Id&&(l.Key.StartsWith("bag:")||l.Key.StartsWith("retainer:")||l.Key is "armoire" or "dresser")).SelectMany(l=>l.Items));
        if(ulong.TryParse(c.ContentId,out var cid))items.UnionWith(keyboundPurchases!.Entries.Where(r=>r.Character==cid&&r.Confirmed).Select(r=>r.Item));
        var rewardIds=keyboundOffers!.Select(o=>o.Item).ToHashSet();
        if(catalogueBase!=null)foreach(var entry in catalogueBase.SelectMany(g=>g).Where(e=>rewardIds.Contains(e.ItemId)))
            if(observations.Any(e=>e.Collection!.Category==entry.Category&&(e.Collection.Unlocked.Contains(entry.Id)||e.Collection.Obtained.Contains(entry.Id))))items.Add(entry.ItemId);
        if(c.Id==current.Id){
            var state=UIState.Instance();
            if(state!=null&&state->PlayerState.IsLoaded)foreach(var offer in keyboundOffers!){
                var row=ExdModule.GetItemRowById(offer.Item);
                if(row!=null&&state->IsItemActionUnlocked(row)==1||ArmoireInventory(offer.Item).Held>0)items.Add(offer.Item);
            }
        }
        var eventItems=items.Intersect(keyboundOffers!.Select(o=>o.Item)).Order().ToArray();
        var evidence=new AccountEventEvidence(quest,eventItems);
        if((quest||eventItems.Length>0)&&(saved==null||saved.QuestComplete!=quest||!saved.Items.SequenceEqual(eventItems))){
            config.EventCelebrationEvidence=new(config.EventCelebrationEvidence){[key]=evidence};SaveConfiguration();
        }
        return evidence;
    }
    private void DrawEventCelebrationStatus()
    {
        if(!Dalamud.Bindings.ImGui.ImGui.CollapsingHeader("Completion celebration"))return;
        Dalamud.Bindings.ImGui.ImGui.TextWrapped(eventCelebrationStatus);
        if(completedEventCelebration is {} complete&&Dalamud.Bindings.ImGui.ImGui.Button("Show Good Job again"))
            QueueAccountCelebration(complete.Identity,complete.Account,"Keybound Brawler + reward list",true);
    }
}
