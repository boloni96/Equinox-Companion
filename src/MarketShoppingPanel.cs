using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed class ShoppingEntry(MarketShoppingRequest request)
    {
        internal MarketShoppingRequest Request = request;
        internal uint ItemId, PlanWorld;
        internal bool Resolved;
        internal string Status = "Preparing item names…";
        internal MarketResult? Result;
        internal MarketWorldPlan[] Plans = [];
        internal DateTimeOffset Due;
        internal int Failures;
    }
    private readonly List<ShoppingEntry> marketShoppingEntries = [];
    private bool marketShoppingMode;
    private string marketImportText = "", marketImportError = "";
    private CancellationTokenSource? marketShoppingCancellation;
    private Task<MarketResult>? marketShoppingTask;
    private ShoppingEntry? marketShoppingActive;
    private DateTimeOffset marketShoppingNext;
    private int marketShoppingCursor;
    private bool? marketShoppingExpand;
    private void RestoreMarketShopping()
    {
        foreach(var request in (config.MarketShoppingItems ?? []).Take(MarketShopping.MaxItems))
            if(request is not null && !string.IsNullOrWhiteSpace(request.Name) && request.Quantity is > 0 and <= MarketShopping.MaxQuantity)
                marketShoppingEntries.Add(new(request));
    }
    private void SaveMarketShopping()
    {
        config.MarketShoppingItems = marketShoppingEntries.Select(x => x.Request).ToList(); SaveConfiguration();
    }
    private void CancelMarketShopping()
    {
        marketShoppingCancellation?.Cancel(); marketShoppingCancellation?.Dispose(); marketShoppingCancellation = null;
        marketShoppingTask = null; marketShoppingActive = null;
    }
    private void ResetMarketShoppingResults()
    {
        CancelMarketShopping(); marketShoppingCursor = 0;
        foreach(var entry in marketShoppingEntries) { entry.Result = null; entry.Plans = []; entry.Due = default; entry.Failures = 0; if(entry.ItemId != 0) entry.Status = marketableIds.Contains(entry.ItemId) ? "Queued…" : "Not marketable · see sources below."; }
        // The single-item result also belongs to the previous filter scope.
        CancelMarketSearch(); marketResult = null; marketAutoRefreshAt = default;
    }
    private void DrawMarketShoppingToolbar()
    {
        if(ImGui.Button("Import Artisan list")) { marketImportError = ""; ImGui.OpenPopup("Import shopping list"); }
        ImGui.SameLine();
        if(ImGui.Button(marketShoppingMode ? "Single item search" : $"Shopping list ({marketShoppingEntries.Count})"))
        {
            marketShoppingMode = !marketShoppingMode;
            if(marketShoppingMode) CancelMarketSearch(); else CancelMarketShopping();
        }
        ImGui.SetNextWindowSize(new Vector2(580, 390), ImGuiCond.FirstUseEver);
        if(ImGui.BeginPopupModal("Import shopping list", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped("Artisan: Export Remaining Ingredients as Plain Text. Paste the list below, then import.");
            if(ImGui.Button("Paste clipboard")) marketImportText = ImGui.GetClipboardText() ?? "";
            ImGui.InputTextMultiline("##ArtisanList", ref marketImportText, 20001, new Vector2(550, 190));
            ImGui.TextDisabled("Example: 3x Ashen Alumen · one item per line. Up to 100 items.");
            if(marketShoppingEntries.Count > 0) ImGui.TextWrapped("Import replaces your saved shopping list. Repeated item names are combined.");
            if(marketImportError.Length > 0) ImGui.TextWrapped(marketImportError);
            if(ImGui.Button("Import list"))
            {
                var imported = MarketShopping.Parse(marketImportText);
                if(imported.Errors.Length > 0) marketImportError = string.Join("\n", imported.Errors.Take(5));
                else
                {
                    CancelMarketSearch(); CancelMarketShopping(); marketShoppingEntries.Clear();
                    marketShoppingEntries.AddRange(imported.Items.Select(x => new ShoppingEntry(x)));
                    marketShoppingCursor = 0; marketShoppingMode = true; marketShoppingExpand = false;
                    SaveMarketShopping(); ImGui.CloseCurrentPopup();
                }
            }
            ImGui.SameLine(); if(ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        ImGui.Separator();
    }
    private void PumpMarketShopping()
    {
        var now = DateTimeOffset.UtcNow;
        if(marketShoppingTask?.IsCompleted == true && marketShoppingActive is {} completed)
        {
            if(marketShoppingTask.IsCompletedSuccessfully)
            {
                var incoming = marketShoppingTask.Result;
                completed.Result = MarketRefresh.Merge(completed.Result, incoming);
                completed.Failures = incoming.FailedRegions.Length == 0 ? 0 : Math.Min(4, completed.Failures + 1);
                completed.Plans = MarketShopping.Plans(completed.Result.Listings, completed.Request.Quantity);
                completed.Due = MarketRefresh.Next(incoming, completed.Failures, config.MarketRefreshSeconds);
                completed.Status = incoming.FailedRegions.Length > 0 ? "Some regions unavailable; older listings retained." : completed.Plans.Length == 0 ? "No market listings returned." : "Checked " + incoming.FetchedAt.LocalDateTime.ToString("HH:mm:ss");
            }
            else
            {
                _ = marketShoppingTask.Exception;
                completed.Failures = Math.Min(4, completed.Failures + 1);
                completed.Due = MarketRefresh.Next(new([], [], now), completed.Failures);
                completed.Status = "Price check failed; retry queued.";
            }
            marketShoppingTask = null; marketShoppingActive = null;
            marketShoppingCancellation?.Dispose(); marketShoppingCancellation = null;
        }
        if(!marketIndexReady) return;
        foreach(var entry in marketShoppingEntries.Where(x => !x.Resolved))
        {
            entry.Resolved = true;
            if(!marketItemNames.TryGetValue(entry.Request.Name, out var id))
            { entry.Status = "No matching item. Check the English item name."; continue; }
            if(id == 0) { entry.Status = "Ambiguous item name; use single-item search."; continue; }
            entry.ItemId = id;
            entry.Status = marketableIds.Contains(id) ? "Queued…" : "Not marketable · see sources below.";
        }
        if(marketShoppingTask is not null || now < marketShoppingNext) return;
        for(var scanned = 0; scanned < marketShoppingEntries.Count; scanned++)
        {
            marketShoppingCursor %= marketShoppingEntries.Count;
            var entry = marketShoppingEntries[marketShoppingCursor++];
            if(entry.ItemId == 0 || !marketableIds.Contains(entry.ItemId) || now < entry.Due) continue;
            var selected = marketWorlds?.FirstOrDefault(w => w.Id == marketWorld) ?? default;
            var id = entry.ItemId; var region = marketWorld == 0 ? marketRegion : selected.Region;
            var quality = marketQuality; var world = marketWorld; var worldName = selected.Name ?? "";
            marketShoppingCancellation = new(); var token = marketShoppingCancellation.Token;
            marketShoppingActive = entry; entry.Status = "Checking prices…"; marketShoppingNext = now.AddSeconds(2);
            var force = entry.Result is not null;
            marketShoppingTask = Task.Run(() => marketSearch.Search(id, region, quality, token, force, world, worldName), token);
            break;
        }
    }
    private void DrawMarketShopping()
    {
        if(DrawMarketFilters()) ResetMarketShoppingResults();
        PumpMarketShopping();
        if(ImGui.Button("Expand all")) marketShoppingExpand = true;
        ImGui.SameLine(); if(ImGui.Button("Collapse all")) marketShoppingExpand = false;
        ImGui.SameLine(); if(ImGui.Button("Refresh list")) foreach(var entry in marketShoppingEntries) entry.Due = default;
        ImGui.TextWrapped("Price checks are queued one item at a time while this window is open. Large lists take longer than the selected refresh interval.");
        ImGui.TextWrapped("Estimates use whole stacks from the cheapest unit-price listings returned, or a cheaper single sufficient stack. They exclude tax and may buy extra; not a guaranteed lowest-cost combination.");
        if(marketShoppingEntries.Count == 0) ImGui.TextWrapped("Import your remaining ingredients from Artisan to start.");
        if(marketTravelStatus.Length > 0) ImGui.TextWrapped(marketTravelStatus);
        if(ImGui.BeginChild("ShoppingItems", new Vector2(0, 0), false))
        {
            for(var i = 0; i < marketShoppingEntries.Count; i++)
            {
                var entry = marketShoppingEntries[i]; ImGui.PushID(i);
                var best = entry.Plans.FirstOrDefault(x => x.Complete);
                var summary = best is not null ? $" · est. {best.Cost:N0} gil · {best.World}" : entry.Plans.Length > 0 ? " · partial stock" : " · " + entry.Status;
                if(entry.ItemId != 0) summary = " · " + MarketSourceLabel(entry.ItemId) + summary;
                if(marketShoppingExpand is {} expanded) ImGui.SetNextItemOpen(expanded, ImGuiCond.Always);
                var open = ImGui.TreeNodeEx($"{entry.Request.Quantity:N0} × {entry.Request.Name}{summary}###ShoppingItem", ImGuiTreeNodeFlags.SpanAvailWidth);
                if(open)
                {
                    var quantity = entry.Request.Quantity; ImGui.SetNextItemWidth(140);
                    if(ImGui.InputInt("Needed", ref quantity) && quantity is > 0 and <= MarketShopping.MaxQuantity)
                    { entry.Request = entry.Request with { Quantity = quantity }; entry.Plans = MarketShopping.Plans(entry.Result?.Listings ?? [], quantity); SaveMarketShopping(); }
                    ImGui.SameLine(); var remove = ImGui.SmallButton("Remove item");
                    ImGui.TextWrapped(entry.Status);
                    DrawMarketItemSources(entry.ItemId, entry.Request.Name);
                    if(entry.Result is {} result)
                    {
                        if(result.FailedRegions.Length > 0) ImGui.TextWrapped("Unavailable / possibly older: " + string.Join(", ", result.FailedRegions));
                        ImGui.TextDisabled("Stock shown is limited to the returned 100 listings per queried region/world.");
                        DrawMarketShoppingPlans(entry);
                        if(ImGui.TreeNode("All returned listings")) { DrawMarketListings(result, entry.Request.Name, "ShoppingListings", 240); ImGui.TreePop(); }
                    }
                    ImGui.TreePop();
                    if(remove)
                    {
                        if(ReferenceEquals(marketShoppingActive, entry)) CancelMarketShopping();
                        marketShoppingEntries.RemoveAt(i); SaveMarketShopping(); ImGui.PopID(); break;
                    }
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild(); marketShoppingExpand = null;
    }
    private void DrawMarketShoppingPlans(ShoppingEntry entry)
    {
        if(!ImGui.BeginTable("WorldPlans", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY, new Vector2(0, 180))) return;
        foreach(var name in new[] { "World", "Available", "Plan quantity", "Plan gil", "Stacks", "Travel" }) ImGui.TableSetupColumn(name);
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        foreach(var plan in entry.Plans)
        {
            ImGui.PushID((int)plan.WorldId); ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextUnformatted(plan.World);
            if(ImGui.IsItemHovered()) ImGui.SetTooltip(MarketDataCenter(plan.WorldId) + " / " + plan.Region);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(plan.Available.ToString("N0"));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(plan.BuyQuantity.ToString("N0") + (plan.Complete ? "" : " · short"));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(plan.Cost.ToString("N0"));
            ImGui.TableNextColumn(); if(ImGui.SmallButton("View")) entry.PlanWorld = plan.WorldId;
            ImGui.TableNextColumn(); var here = Player.IsLoaded && Player.CurrentWorld.RowId == plan.WorldId;
            ImGui.BeginDisabled(!Player.IsLoaded || here || DateTimeOffset.UtcNow < marketTravelNext);
            if(ImGui.SmallButton(here ? "Here" : "Travel")) TravelToMarketWorld(plan.WorldId);
            ImGui.EndDisabled(); ImGui.PopID();
        }
        ImGui.EndTable();
        if(entry.Plans.FirstOrDefault(x => x.WorldId == entry.PlanWorld) is {} selected)
        {
            ImGui.TextWrapped($"{selected.World}: buy {selected.BuyQuantity:N0} for {selected.Cost:N0} gil before tax · need {entry.Request.Quantity:N0}.");
            DrawMarketListings(new(selected.Purchases, [], entry.Result!.FetchedAt), entry.Request.Name, "ChosenStacks", 170);
        }
    }
}
