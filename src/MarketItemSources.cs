using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed class ItemSources
    {
        internal readonly SortedSet<string> Kinds = new(StringComparer.Ordinal);
        internal readonly SortedSet<string> Details = new(StringComparer.Ordinal);
        internal string Label = "";
    }
    private readonly Dictionary<uint, ItemSources> marketSources = [];
    private IEnumerator<bool>? marketSourceReader;
    private bool marketSourcesReady, marketSourcesFailed;
    private void AddMarketSource(uint id, string kind, string detail)
    {
        if(id == 0) return;
        if(!marketSources.TryGetValue(id, out var source)) marketSources[id] = source = new();
        if(source.Kinds.Add(kind)) source.Label = string.Join(" / ", source.Kinds);
        if(detail.Length > 0) source.Details.Add(detail);
    }
    // Lumina rows are read on the UI thread in bounded slices, once per plugin session.
    private void PumpMarketSources()
    {
        if(marketSourcesReady || marketSourcesFailed) return;
        try
        {
            marketSourceReader ??= ReadMarketSources().GetEnumerator();
            var started = Stopwatch.GetTimestamp();
            do
            {
                if(!marketSourceReader.MoveNext())
                { marketSourceReader.Dispose(); marketSourceReader = null; marketSourcesReady = true; break; }
            } while(Stopwatch.GetElapsedTime(started).TotalMilliseconds < 1);
        }
        catch(Exception ex)
        {
            marketSourcesFailed = true; marketSourceReader?.Dispose(); marketSourceReader = null;
            Log.Warning(ex, "Item source index unavailable");
        }
    }
    private IEnumerable<bool> ReadMarketSources()
    {
        foreach(var group in DataManager.GetSubrowExcelSheet<GilShopItem>())
            foreach(var row in group)
            { AddMarketSource(row.Item.RowId, "Vendor", "Vendor · gil shop (stock may require quest progress)"); yield return true; }
        foreach(var shop in DataManager.GetExcelSheet<SpecialShop>())
        {
            var name = shop.Name.ToString();
            foreach(var offer in shop.Item)
            {
                foreach(var reward in offer.ReceiveItems)
                    AddMarketSource(reward.Item.RowId, "Vendor", "Vendor exchange · " + (name.Length > 0 ? name : "special-currency shop") + " (unlock/currency requirements may apply)");
                yield return true;
            }
        }
        foreach(var group in DataManager.GetSubrowExcelSheet<GCScripShopItem>())
            foreach(var row in group)
            { AddMarketSource(row.Item.RowId, "Vendor", $"Grand Company vendor · {row.CostGCSeals:N0} seals · rank requirements apply"); yield return true; }
        foreach(var row in DataManager.GetExcelSheet<Recipe>())
        {
            AddMarketSource(row.ItemResult.RowId, "Crafted", $"Crafted · {row.CraftType.Value.Name} · level {row.RecipeLevelTable.Value.ClassJobLevel}");
            yield return true;
        }
        var gathering = new Dictionary<uint, uint>();
        foreach(var row in DataManager.GetExcelSheet<GatheringItem>())
        {
            gathering[row.RowId] = row.Item.RowId;
            AddMarketSource(row.Item.RowId, "Gathered", row.IsHidden ? "Gathered · hidden item" : ""); yield return true;
        }
        foreach(var point in DataManager.GetExcelSheet<GatheringPoint>())
        {
            if(point.TerritoryType.RowId != 0 && point.GatheringPointBase.RowId != 0)
            {
                var node = point.GatheringPointBase.Value;
                // Spearfishing nodes use a different item sheet.
                if(node.GatheringType.RowId < 4)
                {
                    var zone = point.TerritoryType.Value.PlaceName.Value.Name.ToString();
                    var area = point.PlaceName.Value.Name.ToString();
                    foreach(var item in node.Item)
                        if(gathering.TryGetValue(item.RowId, out var id))
                            AddMarketSource(id, "Gathered", $"Gathered · {node.GatheringType.Value.Name} · {zone}" + (area.Length > 0 ? " / " + area : "") + $" · level {node.GatheringLevel}");
                }
            }
            yield return true;
        }
        foreach(var spot in DataManager.GetExcelSheet<FishingSpot>())
        {
            var zone = spot.TerritoryType.Value.PlaceName.Value.Name.ToString();
            foreach(var item in spot.Item)
                AddMarketSource(item.RowId, "Fishing", $"Fishing · {zone} / {spot.PlaceName.Value.Name}");
            yield return true;
        }
        foreach(var task in DataManager.GetExcelSheet<RetainerTaskNormal>())
        {
            if(task.Item.RowId != 0)
                AddMarketSource(task.Item.RowId, "Retainer", "Retainer venture · " + (task.GatheringLog.RowId != 0 ? "gathering" : task.FishingLog.RowId != 0 ? "fishing" : "hunting"));
            yield return true;
        }
    }
    private string MarketSourceLabel(uint id) => marketSources.TryGetValue(id, out var source) ? source.Label : marketSourcesReady ? "Source not indexed" : marketSourcesFailed ? "Sources unavailable" : "Looking up sources…";
    private void DrawMarketItemSources(uint id, string name)
    {
        if(id == 0) return;
        ImGui.TextWrapped("Sources: " + MarketSourceLabel(id));
        if(!marketSourcesReady && !marketSourcesFailed) ImGui.TextDisabled("Source lookup is still loading…");
        if(ImGui.TreeNode("Source details"))
        {
            if(marketSources.TryGetValue(id, out var source))
                foreach(var detail in source.Details) ImGui.TextWrapped(detail);
            ImGui.TextWrapped("Sources use local game data. Hidden nodes, monster drops, rewards and some exchanges may be missing. Availability can depend on unlocks.");
            if(Pi.InstalledPlugins.Any(p => p.IsLoaded && p.InternalName == "ItemVendorLocation"))
            {
                if(ImGui.Button("Show vendor locations"))
                {
                    try { if(!Commands.ProcessCommand("/pvendor " + name.Replace('\r', ' ').Replace('\n', ' '))) marketTravelStatus = "Vendor lookup is unavailable."; }
                    catch(Exception) { marketTravelStatus = "Vendor lookup failed. Check Item Vendor Location is enabled."; }
                }
                if(ImGui.IsItemHovered()) ImGui.SetTooltip("Search Item Vendor Location for NPC names and locations, like Artisan's vendor lookup.");
            }
            else ImGui.TextDisabled("Enable Item Vendor Location for NPC names and vendor locations.");
            ImGui.TreePop();
        }
    }
}
