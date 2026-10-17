using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private MarketSearchWindow marketWindow = null!;
    private readonly MarketSearch marketSearch = new();
    private CancellationTokenSource? marketCancellation;
    private Task<MarketResult>? marketTask;
    private MarketResult? marketResult;
    private bool marketCommandRegistered, marketIndexReady, marketPendingLookup;
    private string marketQuery = "", marketMessage = "Search by item name or item ID.", marketSelectedName = "";
    private uint marketSelectedId, marketExactId, marketWorld;
    private (uint Id, string Name, int Region)[]? marketWorlds;
    private int marketRegion, marketQuality;
    private DateTimeOffset marketNextSearch, marketAutoRefreshAt;
    private int marketRefreshFailures, marketRefreshChoice = 1;
    private DateTimeOffset marketTravelNext;
    private string marketTravelStatus = "";
    private static readonly int[] MarketRefreshChoices = [15, 30, 60];
    private IEnumerator<Lumina.Excel.Sheets.Item>? marketIndexReader;
    private readonly List<(uint Id, string Name)> marketItems = [];
    private readonly Dictionary<string, uint> marketItemNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<uint> marketableIds = [];
    private (uint Id, string Name)[] marketMatches = [];
    private readonly Dictionary<uint, string> marketDataCenters = [];
    private sealed class MarketSearchWindow : Window
    {
        private readonly Plugin plugin;
        internal MarketSearchWindow(Plugin plugin) : base("Where is it cheapest?###EquinoxMarketSearch")
        {
            this.plugin = plugin; Size = new(950, 540); SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new() { MinimumSize = new(680, 360), MaximumSize = new(float.MaxValue) };
            AllowPinning = true; AllowClickthrough = true; AllowBackgroundBlur = true; RespectCloseHotkey = false;
        }
        public override void Draw()=>plugin.DrawWindowWithLauncher(plugin.DrawMarketSearch);
        public override void PostDraw() => HandleNativeCollapse(this, () => plugin.MinimizeLauncher("Where is it cheapest?"));
    }
    private void InitializeMarketSearch()
    {
        marketRefreshChoice = config.MarketRefreshSeconds == 15 ? 0 : config.MarketRefreshSeconds == 60 ? 2 : 1;
        marketWindow = new(this); windows.AddWindow(marketWindow);
        RestoreMarketShopping();
        marketCommandRegistered = Commands.AddHandler("/whereischeap", new CommandInfo((_, args) => OpenMarketSearch(args)) { HelpMessage = "Find cheapest recorded market listings: /whereischeap item name or ID. Opens a separate window." });
        if(!marketCommandRegistered) Log.Warning("/whereischeap is already registered. Use /equinox cheap instead.");
    }
    private void OpenMarketItem(uint id)
    {
        OpenMarketSearch(SearchItemName(id));
        marketExactId = id;
    }
    private void OpenMarketSearch(string query)
    {
        marketWindow.IsOpen = true; minimizedLaunchers.Remove("Where is it cheapest?");
        if(string.IsNullOrWhiteSpace(query)) return;
        marketShoppingMode = false; CancelMarketShopping();
        marketExactId = 0; marketQuery = query.Trim().Trim('"'); marketPendingLookup = true;
        CancelMarketSearch(); marketResult = null; marketSelectedId = 0; marketMatches = [];
    }
    private void CancelMarketSearch()
    {
        marketCancellation?.Cancel(); marketCancellation?.Dispose(); marketCancellation = null;
        // Search handles network failures; canceled tasks do not mutate UI state.
        marketTask = null;
    }
    private void BuildMarketItemIndex()
    {
        if(marketIndexReady) return;
        marketIndexReader ??= DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).GetEnumerator();
        var start = Stopwatch.GetTimestamp();
        do
        {
            if(!marketIndexReader.MoveNext())
            { marketIndexReader.Dispose(); marketIndexReader = null; marketIndexReady = true; break; }
            var item = marketIndexReader.Current;
            var name = item.Name.ToString();
            if(name.Length == 0) continue;
            if(!marketItemNames.TryAdd(name, item.RowId)) marketItemNames[name] = 0;
            if(item.ItemSearchCategory.RowId != 0 && !item.IsUntradable)
            { marketItems.Add((item.RowId, name)); marketableIds.Add(item.RowId); }
        } while(Stopwatch.GetElapsedTime(start).TotalMilliseconds < 1);
    }
    private void FindMarketItem()
    {
        marketPendingLookup = false; CancelMarketSearch(); marketResult = null; marketMatches = []; marketSelectedId = 0;
        var query = marketQuery.Trim().Trim('"');
        if(query.Length == 0) { marketMessage = "Enter an item name or ID."; return; }
        var exactId = marketExactId; marketExactId = 0;
        if(exactId != 0 || uint.TryParse(query, out exactId))
        {
            var item = marketItems.FirstOrDefault(i => i.Id == exactId);
            if(item.Id == 0) { marketMessage = "This item ID is not a marketable item."; return; }
            SelectMarketItem(item); return;
        }
        var exact = marketItems.FirstOrDefault(i => i.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
        if(exact.Id != 0) { SelectMarketItem(exact); return; }
        marketMatches = marketItems.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Name).Take(100).ToArray();
        if(marketMatches.Length == 1) SelectMarketItem(marketMatches[0]);
        else marketMessage = marketMatches.Length == 0 ? "No marketable items match that name. Try the English name or item ID." : "Choose an item below (up to 100 matches).";
    }
    private void SelectMarketItem((uint Id, string Name) item)
    {
        marketSelectedId = item.Id; marketSelectedName = item.Name; marketQuery = item.Name; marketMatches = [];
        StartMarketSearch();
    }
    private void StartMarketSearch(bool preserveResults = false, bool force = false)
    {
        CancelMarketSearch();
        if(!preserveResults) { marketResult = null; marketRefreshFailures = 0; }
        if(marketSelectedId == 0) return;
        if(DateTimeOffset.UtcNow < marketNextSearch)
        { marketAutoRefreshAt = marketNextSearch; marketMessage = "Searching shortly…"; return; }
        marketNextSearch = DateTimeOffset.UtcNow.AddSeconds(2);
        marketCancellation = new();
        var token = marketCancellation.Token; var id = marketSelectedId; var region = marketRegion; var quality = marketQuality;
        var selectedWorld = marketWorlds?.FirstOrDefault(w => w.Id == marketWorld) ?? default;
        var world = marketWorld; if(world != 0) region = selectedWorld.Region;
        var worldName = selectedWorld.Name ?? "";
        marketTask = Task.Run(() => marketSearch.Search(id, region, quality, token, force, world, worldName), token);
        marketMessage = marketResult is null ? "Searching Universalis…" : "Checking for new prices…";
    }
    private string MarketDataCenter(uint world)
    {
        if(!marketDataCenters.TryGetValue(world, out var name))
            marketDataCenters[world] = name = DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(world)?.DataCenter.Value.Name.ToString() ?? "Unknown";
        return name;
    }
    private void DrawMarketSearch()
    {
        BuildMarketItemIndex();
        PumpMarketSources();
        DrawShoppingDestination();
        DrawMarketShoppingToolbar();
        if(marketShoppingMode) { DrawMarketShopping(); return; }
        if(marketIndexReady && marketPendingLookup) FindMarketItem();
        if(marketTask?.IsCompleted == true)
        {
            if(marketTask.IsCompletedSuccessfully)
            {
                var incoming = marketTask.Result;
                var updated = marketResult is not null && !marketResult.Listings.SequenceEqual(incoming.Listings);
                marketResult = MarketRefresh.Merge(marketResult, incoming);
                marketRefreshFailures = incoming.FailedRegions.Length == 0 ? 0 : Math.Min(4, marketRefreshFailures + 1);
                marketAutoRefreshAt = MarketRefresh.Next(incoming, marketRefreshFailures, MarketRefreshChoices[marketRefreshChoice]);
                marketMessage = incoming.FailedRegions.Length > 0 ? "Some regions unavailable; older results kept for those regions." :
                    marketResult.Listings.Length == 0 ? "No listings returned for the selected filters." :
                    $"{marketResult.Listings.Length} listings · cheapest unit price first" + (updated ? " · Updated" : "");
            }
            else
            {
                _ = marketTask.Exception;
                marketRefreshFailures = Math.Min(4, marketRefreshFailures + 1);
                marketAutoRefreshAt = MarketRefresh.Next(new([], [], DateTimeOffset.UtcNow), marketRefreshFailures);
                marketMessage = "Check failed; previous results kept. Will retry automatically.";
            }
            marketTask = null;
        }
        if(!marketPendingLookup && MarketRefresh.Due(marketWindow.IsOpen, marketSelectedId, marketTask is not null, DateTimeOffset.UtcNow, marketAutoRefreshAt))
            StartMarketSearch(preserveResults: true, force: true);
        ImGui.SetNextItemWidth(Math.Max(200, ImGui.GetContentRegionAvail().X - 130));
        var previousQuery = marketQuery;
        var submit = ImGui.InputTextWithHint("##MarketQuery", "Item name or item ID", ref marketQuery, 200, ImGuiInputTextFlags.EnterReturnsTrue);
        if(previousQuery != marketQuery) marketExactId = 0;
        ImGui.SameLine();
        if(ImGui.Button("X##ClearMarketSearch"))
        {
            CancelMarketSearch(); marketQuery = ""; marketSelectedName = "";
            marketSelectedId = 0; marketExactId = 0; marketPendingLookup = false;
            marketMatches = []; marketResult = null; marketAutoRefreshAt = default;
            marketRefreshFailures = 0; marketMessage = "Search by item name or item ID.";
        }
        if(ImGui.IsItemHovered()) ImGui.SetTooltip("Clear search text and results");
        ImGui.SameLine(); if(ImGui.Button("Find item") || submit) marketPendingLookup = true;
        if(DrawMarketFilters()) { ResetMarketShoppingResults(); if(marketSelectedId != 0) StartMarketSearch(); }
        if(!marketIndexReady) ImGui.TextDisabled("Preparing item names…");
        if(marketSelectedId != 0)
        {
            ImGui.BeginDisabled(marketTask is not null || DateTimeOffset.UtcNow < marketNextSearch);
            if(ImGui.Button("Refresh now")) StartMarketSearch(preserveResults: true, force: true);
            ImGui.EndDisabled(); ImGui.SameLine();
            ImGui.TextUnformatted(marketSelectedName); CopyTextOnClick(marketSelectedName);
            ImGui.SameLine();
            if(ImGui.Button("Sources")) ImGui.OpenPopup("MarketSourceDetails");
            if(ImGui.IsItemHovered()) ImGui.SetTooltip(MarketSourceLabel(marketSelectedId));
            ImGui.SetNextWindowSizeConstraints(new Vector2(300, 0), new Vector2(550, 450));
            if(ImGui.BeginPopup("MarketSourceDetails"))
            {
                ImGui.TextWrapped(marketSelectedName);
                DrawMarketItemSources(marketSelectedId, marketSelectedName);
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            ImGui.TextDisabled(marketTask is not null ? "Checking…" : $"Refresh in {Math.Max(0, (int)Math.Ceiling((marketAutoRefreshAt - DateTimeOffset.UtcNow).TotalSeconds))}s");
        }
        ImGui.TextWrapped(marketMessage);
        if(ImGui.IsItemHovered() && marketResult is {} checkedResult)
            ImGui.SetTooltip($"Last check {checkedResult.FetchedAt.LocalDateTime:HH:mm:ss} · up to 100 listings per queried world/region");
        if(marketResult is {} result)
        {
            if(result.FailedRegions.Length > 0)
                ImGui.TextColored(new Vector4(1, .72f, .3f, 1), "Incomplete search — unavailable: " + string.Join(", ", result.FailedRegions));
        }
        if(marketMatches.Length > 0)
        {
            if(ImGui.BeginChild("MarketMatches", new Vector2(0, 0), true))
                foreach(var item in marketMatches) if(ImGui.Selectable(item.Name + "##" + item.Id)) { SelectMarketItem(item); break; }
            ImGui.EndChild(); return;
        }
        if(marketResult is null) return;
        if(marketTravelStatus.Length > 0) ImGui.TextWrapped(marketTravelStatus);
        DrawMarketListings(marketResult, marketSelectedName, "MarketListings");
    }
    private bool DrawMarketFilters()
    {
        if(!ImGui.BeginTable("MarketFilterGrid", 4, ImGuiTableFlags.SizingStretchSame)) return false;
        ImGui.TableNextColumn(); ImGui.TextDisabled("Region"); ImGui.SetNextItemWidth(-1);
        var changed = ImGui.Combo("##MarketRegion", ref marketRegion, "All regions\0North America\0Europe\0Japan\0Oceania\0");
        if(ImGui.IsItemHovered()) ImGui.SetTooltip("Region");
        if(changed) marketWorld = 0;
        marketWorlds ??= DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().Where(w => w.IsPublic)
            .Select(w => (Id:w.RowId, Name:w.Name.ToString(), Region:(int)w.DataCenter.Value.Region.RowId switch { 1 => 3, 2 => 1, 3 => 2, 4 => 4, _ => 0 }))
            .Where(w => w.Region != 0).OrderBy(w => w.Name).ToArray();
        ImGui.TableNextColumn(); ImGui.TextDisabled("World"); ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("##MarketWorld", marketWorld == 0 ? "All worlds" : marketWorlds.FirstOrDefault(w => w.Id == marketWorld).Name))
        {
            if(ImGui.Selectable("All worlds", marketWorld == 0)) { marketWorld = 0; changed = true; }
            foreach(var world in marketWorlds)
                if((marketRegion == 0 || world.Region == marketRegion) && ImGui.Selectable(world.Name, marketWorld == world.Id))
                { marketWorld = world.Id; changed = true; }
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered()) ImGui.SetTooltip("World");
        ImGui.TableNextColumn(); ImGui.TextDisabled("Quality"); ImGui.SetNextItemWidth(-1);
        changed |= ImGui.Combo("##MarketQuality", ref marketQuality, "Any quality\0NQ only\0HQ only\0");
        if(ImGui.IsItemHovered()) ImGui.SetTooltip("Quality");
        ImGui.TableNextColumn(); ImGui.TextDisabled("Auto-refresh"); ImGui.SetNextItemWidth(-1);
        if(ImGui.Combo("##MarketAutoRefresh", ref marketRefreshChoice, "Refresh: 15s\0" + "Refresh: 30s\0" + "Refresh: 60s\0"))
        {
            config.MarketRefreshSeconds = MarketRefreshChoices[marketRefreshChoice]; SaveConfiguration();
            foreach(var entry in marketShoppingEntries) if(entry.Result is {} loaded) entry.Due = MarketRefresh.Next(loaded, entry.Failures, config.MarketRefreshSeconds);
            if(marketResult is {} current) marketAutoRefreshAt = MarketRefresh.Next(current, marketRefreshFailures, config.MarketRefreshSeconds);
        }
        if(ImGui.IsItemHovered()) ImGui.SetTooltip("Auto-refresh interval while this window is open");
        ImGui.EndTable();
        return changed;
    }
    private void DrawMarketListings(MarketResult result, string itemName, string tableId, float height = 0)
    {
        if(ImGui.BeginTable(tableId, 9, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, height)))
        {
            foreach(var heading in new[] { "Unit gil", "Qty", "Stack gil", "Quality", "World", "DC / Region", "Checked", "Location" }) ImGui.TableSetupColumn(heading);
            ImGui.TableSetupColumn("Travel", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
            var now = DateTimeOffset.UtcNow;
            var clipper = ImGui.ImGuiListClipper(); clipper.Begin(result.Listings.Length);
            while(clipper.Step()) for(var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = result.Listings[i]; ImGui.PushID($"{row.WorldId}:{row.Retainer}:{row.Price}:{row.Quantity}:{row.Hq}:{i}"); ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Price.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Quantity.ToString());
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Total.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Hq ? "HQ" : "NQ");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.World);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(MarketDataCenter(row.WorldId) + " / " + row.Region);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(MarketSearch.Age(row.Reviewed, now));
                if(ImGui.IsItemHovered()) ImGui.SetTooltip("Last recorded listing review · Retainer: " + row.Retainer);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Retainer); CopyTextOnClick($"{itemName} · {row.World} ({MarketDataCenter(row.WorldId)}, {row.Region}) · {row.Price:N0} gil each · {row.Quantity} × {(row.Hq ? "HQ" : "NQ")} · {row.Retainer}");
                ImGui.TableNextColumn();
                var here = Player.IsLoaded && Player.CurrentWorld.RowId == row.WorldId;
                ImGui.BeginDisabled(!Player.IsLoaded || shoppingTravel is not null || DateTimeOffset.UtcNow < marketTravelNext);
                if(ImGui.SmallButton("Travel")) TravelToMarketWorld(row.WorldId);
                ImGui.EndDisabled();
                if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip($"Travel to {row.World}, then your saved shopping city / shard. Data Center travel may log out and back in.");
                ImGui.PopID();
            }
            clipper.End(); clipper.Destroy(); ImGui.EndTable();
        }
    }
    private static void DrawMarketSearchIcon(Vector2 at, Vector2 size, float opacity)
    {
        DrawItemSearchIcon(at, size, opacity);
        var d = ImGui.GetWindowDrawList(); var scale = size.X / 64;
        var gold = ImGui.ColorConvertFloat4ToU32(new Vector4(1, .8f, .3f, opacity));
        d.AddCircleFilled(at + new Vector2(24, 35) * scale, 9 * scale, gold, 24);
        d.AddText(at + new Vector2(20, 27) * scale, ImGui.ColorConvertFloat4ToU32(new Vector4(.1f, .12f, .16f, opacity)), "$" );
    }
    private void TravelToMarketWorld(uint world) => BeginShoppingTravel(world);
    private void DrawMarketSearchShortcut(float size)
    {
        var at = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("Open cheapest prices##header", new Vector2(size));
        DrawMarketSearchIcon(at, new Vector2(size), 1);
        if(ImGui.IsItemHovered()) ImGui.SetTooltip("Where is it cheapest? · /whereischeap");
        if(clicked) OpenMarketSearch("");
    }
    private void DisposeMarketSearch()
    {
        CancelMarketSearch(); marketIndexReader?.Dispose(); marketSourceReader?.Dispose(); marketSearch.Dispose();
        if(marketCommandRegistered) Commands.RemoveHandler("/whereischeap");
    }
}
