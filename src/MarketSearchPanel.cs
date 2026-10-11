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
    private uint marketSelectedId;
    private int marketRegion, marketQuality;
    private DateTimeOffset marketNextSearch, marketAutoRefreshAt;
    private int marketRefreshFailures, marketRefreshChoice = 1;
    private DateTimeOffset marketTravelNext;
    private string marketTravelStatus = "";
    private static readonly int[] MarketRefreshChoices = [15, 30, 60];
    private IEnumerator<Lumina.Excel.Sheets.Item>? marketIndexReader;
    private readonly List<(uint Id, string Name)> marketItems = [];
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
        public override void Draw() => plugin.DrawMarketSearch();
        public override void PostDraw() => HandleNativeCollapse(this, () => plugin.MinimizeLauncher("Where is it cheapest?"));
    }
    private void InitializeMarketSearch()
    {
        marketRefreshChoice = config.MarketRefreshSeconds == 15 ? 0 : config.MarketRefreshSeconds == 60 ? 2 : 1;
        marketWindow = new(this); windows.AddWindow(marketWindow);
        marketCommandRegistered = Commands.AddHandler("/whereischeap", new CommandInfo((_, args) => OpenMarketSearch(args)) { HelpMessage = "Find cheapest recorded market listings: /whereischeap item name or ID. Opens a separate window." });
        if(!marketCommandRegistered) Log.Warning("/whereischeap is already registered. Use /equinox cheap instead.");
    }
    private void OpenMarketSearch(string query)
    {
        marketWindow.IsOpen = true; minimizedLaunchers.Remove("Where is it cheapest?");
        if(string.IsNullOrWhiteSpace(query)) return;
        marketQuery = query.Trim().Trim('"'); marketPendingLookup = true;
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
            if(item.ItemSearchCategory.RowId != 0 && !item.IsUntradable)
            {
                var name = item.Name.ToString();
                if(name.Length > 0) marketItems.Add((item.RowId, name));
            }
        } while(Stopwatch.GetElapsedTime(start).TotalMilliseconds < 1);
    }
    private void FindMarketItem()
    {
        marketPendingLookup = false; CancelMarketSearch(); marketResult = null; marketMatches = []; marketSelectedId = 0;
        var query = marketQuery.Trim().Trim('"');
        if(query.Length == 0) { marketMessage = "Enter an item name or ID."; return; }
        if(uint.TryParse(query, out var id))
        {
            var item = marketItems.FirstOrDefault(i => i.Id == id);
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
        marketSelectedId = item.Id; marketSelectedName = item.Name; marketMatches = [];
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
        marketTask = Task.Run(() => marketSearch.Search(id, region, quality, token, force), token);
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
        ImGui.TextWrapped("Market board prices across NA, Europe, Japan and Oceania.");
        ImGui.SetNextItemWidth(Math.Max(200, ImGui.GetContentRegionAvail().X - 95));
        var submit = ImGui.InputTextWithHint("##MarketQuery", "Item name or item ID", ref marketQuery, 200, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine(); if(ImGui.Button("Find item") || submit) marketPendingLookup = true;
        ImGui.SetNextItemWidth(180);
        var changed = ImGui.Combo("Region", ref marketRegion, "All regions\0North America\0Europe\0Japan\0Oceania\0");
        ImGui.SameLine(); ImGui.SetNextItemWidth(140);
        changed |= ImGui.Combo("Quality", ref marketQuality, "Any quality\0NQ only\0HQ only\0");
        if(changed && marketSelectedId != 0) StartMarketSearch();
        ImGui.SetNextItemWidth(140);
        if(ImGui.Combo("Auto-refresh while open", ref marketRefreshChoice, "15 seconds\0" + "30 seconds\0" + "60 seconds\0"))
        {
            config.MarketRefreshSeconds = MarketRefreshChoices[marketRefreshChoice]; SaveConfiguration();
            if(marketResult is {} current) marketAutoRefreshAt = MarketRefresh.Next(current, marketRefreshFailures, config.MarketRefreshSeconds);
        }
        if(!marketIndexReady) ImGui.TextDisabled("Preparing item names…");
        if(marketSelectedId != 0)
        {
            ImGui.TextWrapped(marketSelectedName);
            ImGui.BeginDisabled(marketTask is not null || DateTimeOffset.UtcNow < marketNextSearch);
            if(ImGui.Button("Refresh now")) StartMarketSearch(preserveResults: true, force: true);
            ImGui.EndDisabled(); ImGui.SameLine();
            if(ImGui.Button("Copy item name")) ImGui.SetClipboardText(marketSelectedName);
        }
        ImGui.TextWrapped(marketMessage);
        if(marketResult is {} result)
        {
            if(result.FailedRegions.Length > 0)
                ImGui.TextColored(new Vector4(1, .72f, .3f, 1), "Incomplete search — unavailable: " + string.Join(", ", result.FailedRegions));
            ImGui.TextDisabled($"Last check {result.FetchedAt.LocalDateTime:HH:mm:ss} · up to 100 cheapest listings per region");
        }
        if(marketSelectedId != 0)
            ImGui.TextDisabled(marketTask is not null ? "Auto-refresh active · checking Universalis" : $"Auto-refresh while open · next check in {Math.Max(0, (int)Math.Ceiling((marketAutoRefreshAt - DateTimeOffset.UtcNow).TotalSeconds))}s");
        ImGui.TextWrapped("Recorded prices may have changed or sold. Stack totals exclude purchase tax. Check world access for your character; a listed region is not a travel guarantee.");
        if(marketMatches.Length > 0)
        {
            if(ImGui.BeginChild("MarketMatches", new Vector2(0, 0), true))
                foreach(var item in marketMatches) if(ImGui.Selectable(item.Name + "##" + item.Id)) { SelectMarketItem(item); break; }
            ImGui.EndChild(); return;
        }
        if(marketResult is null) return;
        if(marketTravelStatus.Length > 0) ImGui.TextWrapped(marketTravelStatus);
        if(ImGui.BeginTable("MarketListings", 9, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, 0)))
        {
            foreach(var heading in new[] { "Unit gil", "Qty", "Stack gil", "Quality", "World", "DC / Region", "Checked", "Location" }) ImGui.TableSetupColumn(heading);
            ImGui.TableSetupColumn("Travel", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
            var now = DateTimeOffset.UtcNow;
            var clipper = ImGui.ImGuiListClipper(); clipper.Begin(marketResult.Listings.Length);
            while(clipper.Step()) for(var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = marketResult.Listings[i]; ImGui.PushID($"{row.WorldId}:{row.Retainer}:{row.Price}:{row.Quantity}:{row.Hq}:{i}"); ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Price.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Quantity.ToString());
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Total.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Hq ? "HQ" : "NQ");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.World);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(MarketDataCenter(row.WorldId) + " / " + row.Region);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(MarketSearch.Age(row.Reviewed, now));
                if(ImGui.IsItemHovered()) ImGui.SetTooltip("Last recorded listing review · Retainer: " + row.Retainer);
                ImGui.TableNextColumn(); if(ImGui.SmallButton("Copy")) ImGui.SetClipboardText($"{marketSelectedName} · {row.World} ({MarketDataCenter(row.WorldId)}, {row.Region}) · {row.Price:N0} gil each · {row.Quantity} × {(row.Hq ? "HQ" : "NQ")} · {row.Retainer}");
                ImGui.TableNextColumn();
                var here = Player.IsLoaded && Player.CurrentWorld.RowId == row.WorldId;
                ImGui.BeginDisabled(!Player.IsLoaded || here || DateTimeOffset.UtcNow < marketTravelNext);
                if(ImGui.SmallButton(here ? "Here" : "Travel")) TravelToMarketWorld(row.WorldId);
                ImGui.EndDisabled();
                if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(here ? "You are already on this world." : $"Ask Lifestream to travel to {row.World}. Data Center travel may log this character out and back in. Requires a supported destination.");
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
    private void TravelToMarketWorld(uint world)
    {
        if(!Player.IsLoaded || Player.CurrentWorld.RowId == world) return;
        marketTravelNext = DateTimeOffset.UtcNow.AddSeconds(2);
        if(Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ||
            Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
            Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
        { marketTravelStatus = "Travel waits until your character is out of combat and loading. Click Travel again when ready."; return; }
        var destination = DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(world);
        if(destination is not {} target || !target.IsPublic) { marketTravelStatus = "This destination is not available to this game client."; return; }
        try
        {
            if(LifestreamBusy()) { marketTravelStatus = "Lifestream is already busy; your current trip was kept."; return; }
            if(Pi.GetIpcSubscriber<uint, bool>("Lifestream.ChangeWorldById").InvokeFunc(world))
            {
                marketTravelStatus = "Lifestream accepted travel to " + target.Name.ToString() + ". Follow its travel progress.";
                ShareFollowWorldIntent(world);
            }
            else marketTravelStatus = "Lifestream declined this destination. Check its travel settings and your character's world access.";
        }
        catch(Exception) { marketTravelStatus = "Lifestream must be installed and enabled for World travel."; }
    }
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
        CancelMarketSearch(); marketIndexReader?.Dispose(); marketSearch.Dispose();
        if(marketCommandRegistered) Commands.RemoveHandler("/whereischeap");
    }
}
