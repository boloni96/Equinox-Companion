using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Bindings.ImGui;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed record CatalogueEntry(string Category, uint Id, uint ItemId);
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
        }
        if (collectionGroups.Length == 0) return;
        var group = collectionGroups[nextCollectionGroup++ % collectionGroups.Length];
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
            Collection: new(group[0].Category, known.ToArray(), unlocked.ToArray(), obtained.ToArray())));
        collectionStatus = $"{group[0].Category}: {unlocked.Count}/{group.Length} unlocked · {obtained.Count} reward items held";
    }

    private unsafe void ObserveActivities(DateTimeOffset now)
    {
        if (!config.SyncActivities) return;
        var actor = ReadActor();
        if (!SyncValidation.ActorReady(actor)) return;
        var manager = HousingManager.Instance();
        if (manager == null || manager->WorkshopTerritory == null || manager->CurrentTerritory == null || !manager->CurrentTerritory->IsLoaded()) return;
        var owned = HousingManager.GetOwnedHouseId(FFXIVClientStructs.FFXIV.Client.Game.EstateType.FreeCompanyEstate);
        var here = manager->WorkshopTerritory->HouseId;
        if (owned.Id == 0 || owned.Id == ulong.MaxValue || here.WorldId != owned.WorldId || here.WardIndex != owned.WardIndex || here.PlotIndex != owned.PlotIndex || here.TerritoryTypeId != owned.TerritoryTypeId) return;
        var fc = config.Discoveries.LastOrDefault(x=>x.Kind=="character.updated" && x.Actor.ContentId==actor.ContentId)?.Character?.FreeCompany;
        if (fc is null) return;
        var subs = new List<SubmarineDetails>();
        for (var i=0;i<4;i++)
        {
            ref var s = ref manager->WorkshopTerritory->Submersible.Data[i];
            if (s.RegisterTime == 0 || s.RankId == 0 || string.IsNullOrWhiteSpace(s.NameString)) continue;
            subs.Add(new(i,s.NameString,s.RankId,s.ReturnTime,s.RegisterTime,[s.HullId,s.SternId,s.BowId,s.BridgeId],s.CurrentExplorationPoints.ToArray()));
        }
        // An unloaded panel is not evidence that all vessels have been deleted.
        if (subs.Count > 0) KeepDiscovery(new(Guid.NewGuid().ToString("N"), "submarines.observed", now, actor, null, Voyage: new(fc.Id,subs.ToArray())));
    }

    private void DrawSubmarines()
    {
        ImGui.TextWrapped("Open the FC workshop voyage panel to refresh timers. Paired observations are shared automatically.");
        var observations = new List<SharedVoyage>(config.SharedRoster?.Voyages ?? []);
        foreach (var e in config.Discoveries.Where(x => x.Voyage is not null))
        {
            var fc = config.Discoveries.LastOrDefault(x => x.Character?.FreeCompany?.Id == e.Voyage!.FcId)?.Character?.FreeCompany;
            observations.Add(new(e.Voyage!.FcId, fc?.Name ?? "Free Company", e.At, e.Voyage.Submarines));
        }
        foreach (var group in observations.GroupBy(x => x.FcId))
        {
            var latest = group.MaxBy(x => x.At)!;
            ImGui.Separator();
            ImGui.TextUnformatted($"{latest.FcName} · observed {latest.At.LocalDateTime:g}");
            foreach (var s in latest.Submarines)
            {
                var remaining = DateTimeOffset.FromUnixTimeSeconds(s.ReturnTime) - DateTimeOffset.UtcNow;
                var label = s.ReturnTime == 0 ? "No voyage recorded" : remaining <= TimeSpan.Zero ? "Return due — confirm in workshop" : $"Returns in {(int)remaining.TotalHours}h {remaining.Minutes}m";
                ImGui.TextUnformatted($"{s.Name} · rank {s.Rank} · {label}");
            }
        }
        if (observations.Count == 0) ImGui.TextWrapped("No submarines observed yet.");
    }
}
