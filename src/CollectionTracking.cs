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
            collectionCharacter = Player.ContentId;
            collectionReadyAt = now.AddSeconds(15);
            nextCollectionGroup = 0;
            return;
        }
        if (now < collectionReadyAt) return;
        if (collectionGroups is null)
        {
            var path = Path.Combine(Pi.AssemblyLocation.DirectoryName!, "collection-ids.json");
            if (!File.Exists(path)) { collectionStatus = "Collection catalogue missing; reinstall this plugin build."; return; }
            var entries = JsonSerializer.Deserialize<CatalogueEntry[]>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            collectionGroups = entries.Where(x => x.Id > 0).GroupBy(x => x.Category).Select(g => g.DistinctBy(x=>x.Id).ToArray()).ToArray();
            // Read the installed game's festival quest definitions, not a fixed event/year list.
            var seasonal = new List<CatalogueEntry>();
            foreach(var q in DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>(Dalamud.Game.ClientLanguage.English))
            {
                if(q.Festival.RowId==0)continue;
                var name=q.Name.ToString();var festival=q.Festival.Value.Name.ToString();
                if(string.IsNullOrWhiteSpace(name)||name.Length>100||name.Any(char.IsControl)||
                   string.IsNullOrWhiteSpace(festival)||festival.Length>100||festival.Any(char.IsControl))continue;
                var rewards = new List<EventQuestReward>();
                var itemIds = q.OptionalItemReward.Select(x=>x.RowId).ToList();
                if(q.ItemRewardType is 1 or 3 or 5)itemIds.AddRange(q.Reward.Select(x=>x.RowId));
                foreach(var id in itemIds.Where(id=>id>0).Distinct().Take(12))
                {
                    var itemName=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(id)?.Name.ToString();
                    if(!string.IsNullOrWhiteSpace(itemName)&&itemName.Length<=100&&!itemName.Any(char.IsControl))rewards.Add(new(id,itemName));
                }
                seasonal.Add(new("quest",q.RowId,0,new(q.RowId,name,q.Festival.RowId,festival,(int)q.ClassJobLevel[0],
                    q.PreviousQuest.Select(x=>x.RowId).Where(id=>id>0).ToArray(),rewards.ToArray())));
            }
            collectionGroups=collectionGroups.Concat(seasonal.OrderBy(x=>x.Id).Chunk(8)).ToArray();
        }
        if (collectionGroups.Length == 0) return;
        var segmentIndex = nextCollectionGroup++ % collectionGroups.Length;
        var group = collectionGroups[segmentIndex];
        var unlocked = new List<uint>(); var obtained = new List<uint>(); var known = new List<uint>();
        if (group[0].Category == "hairstyle" && hairstyleUnlocks is null)
            hairstyleUnlocks = DataManager.GetExcelSheet<Lumina.Excel.Sheets.CharaMakeCustomize>(Dalamud.Game.ClientLanguage.English)
                .Where(x => x.HintItem.RowId > 0 && x.UnlockLink > 0)
                .GroupBy(x => x.HintItem.RowId)
                .ToDictionary(g => g.Key, g => g.Select(x => (uint)x.UnlockLink).Distinct().ToArray());
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
                "framerkit" => state->PlayerState.IsFramersKitUnlocked(entry.Id),
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
            subs.Add(new(i,s.NameString,s.RankId,s.ReturnTime,s.RegisterTime,[s.HullId,s.SternId,s.BowId,s.BridgeId],s.CurrentExplorationPoints.ToArray(),ReadSubmarineRepair(i)));
        }
        // An unloaded panel is not evidence that all vessels have been deleted.
        if (subs.Count > 0) KeepDiscovery(new(Guid.NewGuid().ToString("N"), "submarines.observed", now, actor, null, Voyage: new(fcId,subs.ToArray())));
    }

}
