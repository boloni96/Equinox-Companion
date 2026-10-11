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
    private DateTimeOffset marketNextSearch;
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
    private void StartMarketSearch()
    {
        CancelMarketSearch(); marketResult = null;
        if(marketSelectedId == 0) return;
        if(DateTimeOffset.UtcNow < marketNextSearch)
        { marketMessage = "Please wait a moment, then press Search prices."; return; }
        marketNextSearch = DateTimeOffset.UtcNow.AddSeconds(2);
        marketCancellation = new();
        var token = marketCancellation.Token; var id = marketSelectedId; var region = marketRegion; var quality = marketQuality;
        marketTask = Task.Run(() => marketSearch.Search(id, region, quality, token), token);
        marketMessage = "Searching Universalis…";
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
                marketResult = marketTask.Result;
                marketMessage = marketResult.Listings.Length == 0 ? "No listings returned for the selected filters." : $"{marketResult.Listings.Length} listings · cheapest unit price first";
            }
            else { _ = marketTask.Exception; marketMessage = "Search unavailable. Please try again."; }
            marketTask = null;
        }
        ImGui.TextWrapped("Market board prices across NA, Europe, Japan and Oceania.");
        ImGui.SetNextItemWidth(Math.Max(200, ImGui.GetContentRegionAvail().X - 95));
        var submit = ImGui.InputTextWithHint("##MarketQuery", "Item name or item ID", ref marketQuery, 200, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine(); if(ImGui.Button("Find item") || submit) marketPendingLookup = true;
        ImGui.SetNextItemWidth(180);
        var changed = ImGui.Combo("Region", ref marketRegion, "All regions\0North America\0Europe\0Japan\0Oceania\0");
        ImGui.SameLine(); ImGui.SetNextItemWidth(140);
        changed |= ImGui.Combo("Quality", ref marketQuality, "Any quality\0NQ only\0HQ only\0");
        if(changed && marketSelectedId != 0) StartMarketSearch();
        if(!marketIndexReady) ImGui.TextDisabled("Preparing item names…");
        if(marketSelectedId != 0)
        {
            ImGui.TextWrapped(marketSelectedName);
            ImGui.BeginDisabled(marketTask is not null || DateTimeOffset.UtcNow < marketNextSearch);
            if(ImGui.Button("Search prices")) StartMarketSearch();
            ImGui.EndDisabled(); ImGui.SameLine();
            if(ImGui.Button("Copy item name")) ImGui.SetClipboardText(marketSelectedName);
        }
        ImGui.TextWrapped(marketMessage);
        if(marketResult is {} result)
        {
            if(result.FailedRegions.Length > 0)
                ImGui.TextColored(new Vector4(1, .72f, .3f, 1), "Incomplete search — unavailable: " + string.Join(", ", result.FailedRegions));
            ImGui.TextDisabled($"Fetched {result.FetchedAt.LocalDateTime:HH:mm:ss} · cached for 60s · up to 100 cheapest listings per region");
        }
        ImGui.TextWrapped("Recorded prices may have changed or sold. Stack totals exclude purchase tax. Check world access for your character; a listed region is not a travel guarantee.");
        if(marketMatches.Length > 0)
        {
            if(ImGui.BeginChild("MarketMatches", new Vector2(0, 0), true))
                foreach(var item in marketMatches) if(ImGui.Selectable(item.Name + "##" + item.Id)) { SelectMarketItem(item); break; }
            ImGui.EndChild(); return;
        }
        if(marketResult is null) return;
        if(ImGui.BeginTable("MarketListings", 8, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, 0)))
        {
            foreach(var heading in new[] { "Unit gil", "Qty", "Stack gil", "Quality", "World", "DC / Region", "Checked", "Location" }) ImGui.TableSetupColumn(heading);
            ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
            var now = DateTimeOffset.UtcNow;
            var clipper = ImGui.ImGuiListClipper(); clipper.Begin(marketResult.Listings.Length);
            while(clipper.Step()) for(var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = marketResult.Listings[i]; ImGui.PushID(i); ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Price.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Quantity.ToString());
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Total.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Hq ? "HQ" : "NQ");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(row.World);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(MarketDataCenter(row.WorldId) + " / " + row.Region);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(MarketSearch.Age(row.Reviewed, now));
                if(ImGui.IsItemHovered()) ImGui.SetTooltip("Last recorded listing review · Retainer: " + row.Retainer);
                ImGui.TableNextColumn(); if(ImGui.SmallButton("Copy")) ImGui.SetClipboardText($"{marketSelectedName} · {row.World} ({MarketDataCenter(row.WorldId)}, {row.Region}) · {row.Price:N0} gil each · {row.Quantity} × {(row.Hq ? "HQ" : "NQ")} · {row.Retainer}");
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
    private void DisposeMarketSearch()
    {
        CancelMarketSearch(); marketIndexReader?.Dispose(); marketSearch.Dispose();
        if(marketCommandRegistered) Commands.RemoveHandler("/whereischeap");
    }
}
