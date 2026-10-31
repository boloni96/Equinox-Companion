using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Bindings.ImGui;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed record CatalogueEntry(string Category, uint Id, uint ItemId, EventQuestDefinition? Quest = null);
    private CatalogueEntry[][]? collectionGroups;
    private int nextCollectionGroup;
    private int priorityCollectionTurn;
    private int nextPriorityCollection;
    private readonly HashSet<uint> keyboundCollectionRefreshItems=[];
    private Dictionary<uint, uint[]>? hairstyleUnlocks;
    private DateTimeOffset collectionReadyAt;
    private ulong collectionCharacter;
    private string collectionStatus = "Waiting for loaded character collections.";

    private unsafe void ObserveCollections(DateTimeOffset now)
    {
        if (!config.SyncCollections) return;
        var state = UIState.Instance();
        var actor = ReadActor();
        if (!SyncValidation.ActorReady(actor) || state == null || !state->PlayerState.IsLoaded) return;
        if (collectionCharacter != Player.ContentId)
        {
            collectionCharacter = Player.ContentId;keyboundCollectionRefreshItems.Clear();
            collectionReadyAt = now.AddSeconds(15);
            nextCollectionGroup = 0; priorityCollectionTurn = 0; nextPriorityCollection = 0;
            return;
        }
        if (now < collectionReadyAt) return;
        if (collectionGroups is null) return;
        if (collectionGroups.Length == 0) return;
        var segmentIndex = nextCollectionGroup++ % collectionGroups.Length;
        // Interleave saved adventure quests with the full catalogue. A seasonal
        // quest must not wait for every historical festival segment to cycle.
        var watched = config.CachedAdventures.SelectMany(b => b.Stops).SelectMany(s => s.Tasks)
            .Where(t => t.Rule?.Kind == "event").SelectMany(t => t.Rule?.Quests ?? [])
            .Append(71004u).ToHashSet(); // All Saints' Wake 2026 final quest, game sheet.
        var priority = collectionGroups.Select((g, i) => (g, i))
            .Where(x => x.g.Any(e => e.Category == "quest" && watched.Contains(e.Id) || e.Category == "framerkit" && e.ItemId == 51997)).Select(x => x.i).ToArray();
        if (priority.Length > 0 && priorityCollectionTurn++ % 2 == 0)
        {
            nextCollectionGroup--;
            segmentIndex = priority[nextPriorityCollection++ % priority.Length];
        }
        if(keyboundCollectionRefreshItems.Count>0){
            var requested=Array.FindIndex(collectionGroups,g=>g.Any(e=>keyboundCollectionRefreshItems.Contains(e.ItemId)));
            if(requested>=0){segmentIndex=requested;foreach(var entry in collectionGroups[requested])keyboundCollectionRefreshItems.Remove(entry.ItemId);}
            else keyboundCollectionRefreshItems.Clear();
        }
        var group = collectionGroups[segmentIndex];
        var unlocked = new List<uint>(); var obtained = new List<uint>(); var known = new List<uint>();
        var inventory = InventoryManager.Instance();
        foreach (var entry in group)
        {
            uint hairstyleLink = 0;
            if (entry.Category == "hairstyle")
            {
                // Catalogue IDs vary by appearance. The learned item is mapped only when
                // every matching game row agrees on the same unlock link.
                if (entry.ItemId == 0 || !hairstyleUnlocks!.TryGetValue(entry.ItemId, out var links) || links.Length != 1) continue;
                hairstyleLink = links[0];
            }
            known.Add(entry.Id);
            var owned = entry.Category switch
            {
                "hairstyle" => state->IsUnlockLinkUnlocked(hairstyleLink),
                "mount" => state->PlayerState.IsMountUnlocked(entry.Id),
                "minion" => state->IsCompanionUnlocked(entry.Id),
                "orchestrion" => state->PlayerState.IsOrchestrionRollUnlocked(entry.Id),
                "emote" => state->IsEmoteUnlocked((ushort)entry.Id),
                "barding" => state->Buddy.CompanionInfo.IsBuddyEquipUnlocked(entry.Id),
                "card" => state->IsTripleTriadCardUnlocked((ushort)entry.Id),
                "ornament" => state->PlayerState.IsOrnamentUnlocked(entry.Id),
                "framerkit" => state->PlayerState.IsFramersKitUnlocked(entry.Id) || entry.ItemId==51997 && KeyboundUnlockState(entry.ItemId)==1,
                "quest" => QuestManager.IsQuestComplete(entry.Id),
                _ => false
            };
            if (owned) unlocked.Add(entry.Id);
            if (entry.ItemId > 0 && inventory != null && inventory->GetInventoryItemCount(entry.ItemId) > 0) obtained.Add(entry.Id);
        }
        if (known.Count == 0) return;
        KeepDiscovery(new(Guid.NewGuid().ToString("N"), "collection.observed", now, actor, null,
            Collection: new(group[0].Category, known.ToArray(), unlocked.ToArray(), obtained.ToArray(),
                group[0].Quest is null?0:segmentIndex+1,
                group[0].Quest is null?null:group.Select(x=>x.Quest!).ToArray(),
                group[0].Category=="quest"&&QuestManager.Instance()!=null?known.Where(id=>QuestManager.Instance()->IsQuestAccepted(id)).ToArray():null)));
        collectionStatus = $"{group[0].Category}: {unlocked.Count}/{group.Length} unlocked · {obtained.Count} reward items held";
    }

    private static unsafe bool? ReadSubmarineRepair(int index)
    {
        var inventory=InventoryManager.Instance();if(inventory==null)return null;
        var container=inventory->GetInventoryContainer(InventoryType.HousingInteriorPlacedItems2);
        if(container==null||!container->IsLoaded||container->Size<index*5+4)return null;
        var repair=false;
        for(var part=0;part<4;part++){var item=container->GetInventorySlot(index*5+part);if(item==null||item->ItemId==0)return null;repair|=item->Condition==0;}
        return repair;
    }
    private unsafe int[]? ReadSubmarineStains(int index, ushort[] parts)
    {
        var inventory=InventoryManager.Instance();if(inventory==null)return null;
        var container=inventory->GetInventoryContainer(InventoryType.HousingInteriorPlacedItems2);
        if(container==null||!container->IsLoaded||container->Size<index*5+4)return null;
        var stains=new int[4];
        for(var part=0;part<4;part++)
        {
            var item=container->GetInventorySlot(index*5+part);
            if(item==null||item->ItemId==0)return null;
            var definition=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item->ItemId);
            // Never borrow colours from unrelated or not-yet-loaded equipment.
            if(definition is null||definition.Value.AdditionalData.RowId!=parts[part])return null;
            stains[part]=item->GetStain(0);
        }
        return stains;
    }
    private unsafe void ObserveActivities(DateTimeOffset now)
    {
        if (!config.SyncActivities) return;
        var actor = ReadActor();
        if (!SyncValidation.ActorReady(actor)) return;
        ObserveSubmarineSupplies(actor,now);
        var manager = HousingManager.Instance();
        if (manager == null || manager->WorkshopTerritory == null || manager->CurrentTerritory == null || !manager->CurrentTerritory->IsLoaded()) return;
        var owned = HousingManager.GetOwnedHouseId(FFXIVClientStructs.FFXIV.Client.Game.EstateType.FreeCompanyEstate);
        var here = manager->WorkshopTerritory->HouseId;
        if (owned.Id == 0 || owned.Id == ulong.MaxValue || here.WorldId != owned.WorldId || here.WardIndex != owned.WardIndex || here.PlotIndex != owned.PlotIndex || here.TerritoryTypeId != owned.TerritoryTypeId) return;
        var fcId = CurrentSubmarineFc(actor);
        if (fcId is null) return;
        var subs = new List<SubmarineDetails>();
        for (var i=0;i<4;i++)
        {
            ref var s = ref manager->WorkshopTerritory->Submersible.Data[i];
            if (s.RegisterTime == 0 || s.RankId == 0 || string.IsNullOrWhiteSpace(s.NameString)) continue;
            subs.Add(new(i,s.NameString,s.RankId,s.ReturnTime,s.RegisterTime,[s.HullId,s.SternId,s.BowId,s.BridgeId],s.CurrentExplorationPoints.ToArray(),ReadSubmarineRepair(i),ReadSubmarineStains(i,[s.HullId,s.SternId,s.BowId,s.BridgeId])));
        }
        // An unloaded panel is not evidence that all vessels have been deleted.
        if (subs.Count > 0) KeepDiscovery(new(Guid.NewGuid().ToString("N"), "submarines.observed", now, actor, null, Voyage: new(fcId,subs.ToArray(),1 + new byte[]{10,15,20}.Count(HousingManager.IsSubmarineExplorationExplored))));
    }

}

