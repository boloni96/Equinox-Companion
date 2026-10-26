using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    [Dalamud.IoC.PluginService] internal static Dalamud.Plugin.Services.IGameConfig MarketGameConfig { get; private set; }=null!;
    private EquinoxMarketWindow equinoxMarketWindow=null!;
    private EquinoxSellerWindow sellerWindow=null!;
    private bool eqMarketCommand,eqSellerCommand;
    private bool openMarketSettings,openMarketSelling,marketUpdatesOnly,sellerToolbarFocused;
    private ulong marketFocusRetainer;
    private int sellerScope=2,marketPage,marketDate=2,marketOrder,marketSource;
    private bool marketAttentionOnly;
    private string marketActiveTab="",marketFilter="",marketPerson="",marketAccount="",marketCharacter="",marketRetainerFilter="",marketWorldFilter="";
    private uint marketRuleItem;
    private int marketRuleMinimum=1;
    private readonly UiProjectionCache<MarketIncome[]> marketIncomeView=new(5000);
    private readonly UiProjectionCache<MarketExpense[]> marketExpenseView=new(5000);
    private readonly UiProjectionCache<MarketPriceLog[]> marketChangesView=new(5000);
    private readonly UiProjectionCache<MarketSummaryRow[]> marketSummaryView=new(5000);
    private readonly UiProjectionCache<(long Net,bool Unverified)> marketIncomeTotals=new(0);
    private readonly UiProjectionCache<long> marketExpenseTotal=new(0);
    private sealed record MarketSummaryRow(string Name,long Net,long Gross,long Units,int Count);
    private MarketRetainer[]? marketRetainerIndexSource;
    private Dictionary<ulong,MarketRetainer> marketRetainerIndex=[];
    private MarketRetainer? MarketRetainerById(ulong id){
        if(!ReferenceEquals(marketRetainerIndexSource,MarketRetainers)){marketRetainerIndexSource=MarketRetainers;marketRetainerIndex=MarketRetainers.DistinctBy(x=>x.Id).ToDictionary(x=>x.Id);}
        return marketRetainerIndex.GetValueOrDefault(id);
    }
    private string MarketViewStamp=>string.Join("\u001f",marketDate.ToString(),marketSource.ToString(),marketFocusRetainer.ToString(),marketFilter,marketPerson,marketAccount,marketCharacter,marketRetainerFilter,marketWorldFilter,(config.SharedRoster?.Revision??0).ToString());
    private sealed class EquinoxMarketWindow(Plugin p):Window("Equinox Market###EquinoxMarket")
    {
        public override void OnOpen(){Size??=new(750,480);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(450,300),MaximumSize=new(float.MaxValue)};RespectCloseHotkey=false;AllowPinning=true;AllowClickthrough=true;}
        public override void Draw()=>p.DrawWindowWithLauncher(p.DrawEquinoxMarket);
        public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Equinox Market"));
    }
    private sealed class EquinoxSellerWindow(Plugin p):Window("Equinox Seller###EquinoxSeller")
    {
        public override void OnOpen(){Size??=new(440,230);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(330,180),MaximumSize=new(float.MaxValue)};RespectCloseHotkey=false;AllowPinning=true;AllowClickthrough=true;}
        public override void Draw()=>p.DrawWindowWithLauncher(p.DrawEquinoxSeller);
        public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Equinox Seller"));
    }
    private void InitializeMarketWindows()
    {
        equinoxMarketWindow=new(this);sellerWindow=new(this);windows.AddWindow(equinoxMarketWindow);windows.AddWindow(sellerWindow);
        eqMarketCommand=Commands.AddHandler("/eqmarket",new CommandInfo((_,_)=>OpenEquinoxMarket()){HelpMessage="Equinox Market: retainers, listings, sales and spending."});
        eqSellerCommand=Commands.AddHandler("/eqseller",new CommandInfo((_,args)=>{if(args.Trim().Equals("stop",StringComparison.OrdinalIgnoreCase))sellerRun.Stop("Stopped by command.");else OpenEquinoxSeller();}){HelpMessage="Equinox Seller price updater. /eqseller stop stops immediately."});
        InitializeMarketReceipts();
        InitializeMarketHistorySync();
    }
    private void DisposeMarketWindows(){DisposeMarketHistorySync();if(eqMarketCommand)Commands.RemoveHandler("/eqmarket");if(eqSellerCommand)Commands.RemoveHandler("/eqseller");}
    private void OpenEquinoxMarket(){equinoxMarketWindow.IsOpen=true;minimizedLaunchers.Remove("Equinox Market");}
    private void OpenEquinoxSeller(){sellerWindow.IsOpen=true;minimizedLaunchers.Remove("Equinox Seller");}
    private static Vector4 SellerColor(SellerStatus status)=>status switch {SellerStatus.Update=>new(1,.35f,.35f,1),SellerStatus.Competitive=>new(.35f,.85f,.50f,1),SellerStatus.Checking=>new(.4f,.75f,1,1),SellerStatus.History or SellerStatus.Skipped=>new(1,.75f,.35f,1),_=>new(.85f,.7f,.4f,1)};
    private SellerDecision ReviewFor(ulong retainer,int slot)
    {
        var current=MarketRetainerById(retainer)?.Stock.FirstOrDefault(x=>x.Slot==slot);
        if(current!=null&&config.Seller.ExcludedItems.Contains(current.ItemId))return new(SellerStatus.Skipped,null,"Excluded from automatic price updates by your item rule.","Item rule");
        var hasLive=sellerReviews.TryGetValue((retainer,slot),out var live);
        if(current!=null&&hasLive&&live.Item.SamePricingItem(current)&&live.Item.Price==current.Price&&DateTimeOffset.UtcNow-live.At<=TimeSpan.FromMinutes(5))return live.Decision;
        if(config.Seller.BackgroundChecks&&current!=null&&marketWatchReviews.TryGetValue((retainer,slot),out var watch)&&watch.Item.SamePricingItem(current)&&watch.Item.Price==current.Price)return watch.Decision;
        var why=hasLive?(current!=null&&live.Item.SamePricingItem(current)&&live.Item.Price==current.Price?"The live comparison is over five minutes old.":"This listing changed after its last live comparison."):"No live comparison is recorded for this listing in this session.";
        why+=config.Seller.BackgroundChecks?" Waiting for usable background data, or run Check prices at the bell.":" Background checks are off; run Check prices at the bell.";
        return new(SellerStatus.Unknown,null,why,"None");
    }

    private bool MarketEnabledUi()
    {
        if(config.Seller.Enabled)return true;
        ImGui.TextWrapped("Equinox Market & Seller are optional tools on this computer.");
        if(ImGui.Button("Enable Market & Seller"))SetMarketEnabled(true);
        return false;
    }
    private void DrawEquinoxSeller()
    {
        if(!MarketEnabledUi())return;
        if(marketLoad!=null)ImGui.TextDisabled("Loading saved market records…");
        if(marketDataError.Length>0)ImGui.TextWrapped(marketDataError);
        ImGui.BeginDisabled(sellerRun.Running);
        ImGui.SetNextItemWidth(Math.Min(180,ImGui.GetContentRegionAvail().X));ImGui.Combo("##sellerScope",ref sellerScope,"Current retainer\0Selected retainers\0All retainers\0");
        if(sellerScope==1 && ImGui.CollapsingHeader("Choose retainers"))foreach(var r in ReadSellerRetainers()){
            var selected=config.Seller.SelectedRetainers.Contains(r.Id);
            if(ImGui.Checkbox(r.Name+"###seller-retainer-"+r.Id,ref selected)){if(selected)config.Seller.SelectedRetainers.Add(r.Id);else config.Seller.SelectedRetainers.Remove(r.Id);SaveConfiguration();}
        }
        if(ImGui.Button("Check prices"))StartSeller(false);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Pauses AutoRetainer and waits for its current action to finish, then compares each selected listing. Does not change prices. AutoRetainer's previous pause state is restored afterward.");
        ImGui.SameLine();if(ImGui.Button("Update prices"))StartSeller(true);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Pauses AutoRetainer, waits for its current action, then checks live prices and applies your rules. Existing stack quantities stay unchanged. Restores AutoRetainer's previous pause state afterward.");
        ImGui.EndDisabled();
        ImGui.SameLine();
        var settingsSize=ImGui.GetFrameHeight();var settingsAt=ImGui.GetCursorScreenPos();
        if(ImGui.InvisibleButton("Market & Seller settings",new Vector2(settingsSize)))OpenSellerSettings();
        MarketHelp("All Market & Seller settings · manual selling, pricing, checks and display");
        GardenImage("assets/category-icons/options.png",settingsAt,new Vector2(settingsSize));
        if(sellerRun.Running){
            if(ImGui.Button(sellerRun.Paused?"Resume":"Pause"))sellerRun.Pause(DateTimeOffset.UtcNow);
            ImGui.SameLine();if(ImGui.Button("Stop"))sellerRun.Stop("Stopped. Already confirmed changes are kept.");
        }
        if(sellerNotice.Length>0)ImGui.TextColored(SellerColor(SellerStatus.Skipped),sellerNotice);
        else ImGui.TextWrapped(sellerRun.Status);
        if(!sellerRun.Running&&manualEditor!=null&&manualDecision is {} d){
            ImGui.TextColored(SellerColor(d.VisualStatus),d.CanApply?$"Suggested: {d.Price:N0} gil":d.Status==SellerStatus.Competitive?"Price kept":"Skipped");
            MarketHelp((d.Source=="None"?"":d.Source+"\n")+d.Reason+"\nOpening an item only calculates a suggestion. Update prices applies permitted changes; Check prices does not.");
        }
        if(sellerAutoRetainerPause.Held||sellerAutoRetainerPause.RestorePending){ImGui.TextDisabled(sellerAutoRetainerPause.RestorePending?"AutoRetainer: restoring…":"AutoRetainer: paused");MarketHelp(sellerAutoRetainerPause.Status+"\nStop or finish Seller to restore the previous pause state. Existing AutoRetainer settings are kept.");}
        if(ImGui.CollapsingHeader("Diagnostics##seller"))DrawSellerDiagnostics();

        windowFooter=()=>{if(ImGui.SmallButton("Market"))OpenEquinoxMarket();};
    }
    private static void MarketHelp(string text)
    {
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)){
            ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);ImGui.TextUnformatted(text);ImGui.PopTextWrapPos();ImGui.EndTooltip();
        }
    }
    private void OpenSellerSettings(){openMarketSettings=true;openMarketSelling=false;OpenEquinoxMarket();}
    private void DrawSellerSettings()
    {
        var s=config.Seller;var dirty=false;
        var enabled=s.Enabled;if(ImGui.Checkbox("Enable Market & Seller",ref enabled))SetMarketEnabled(enabled);
        MarketHelp("Enables the manual selling helpers, price updater and local market records. Turning this off keeps your settings and records.");
        ImGui.BeginDisabled(sellerRun.Running);
        void Toggle(string label,bool value,Action<bool> set,string help){if(ImGui.Checkbox(label,ref value)){set(value);dirty=true;}MarketHelp(help);}
        void Number(string label,int value,int min,int max,Action<int> set,string help){ImGui.SetNextItemWidth(100);if(ImGui.InputInt(label,ref value)){set(Math.Clamp(value,min,max));dirty=true;}MarketHelp(help);}
        void Percent(string label,double value,Action<double> set,string help,float maximum=99){var f=(float)value;ImGui.SetNextItemWidth(100);if(ImGui.InputFloat(label,ref f,1,5,"%.1f%%")){set(float.IsFinite(f)?Math.Clamp(f,0,maximum):0);dirty=true;}MarketHelp(help);}
        if(ImGui.CollapsingHeader("Manual selling",ImGuiTreeNodeFlags.DefaultOpen)){
            Toggle("Open Compare Prices",s.OpenCompare,v=>s.OpenCompare=v,"Opens the game's comparison when you open an item's price editor. Hold Shift to skip it for that item.");
            Toggle("Also open recent sale history",s.OpenHistory,v=>s.OpenHistory=v,"Opens this item's market transaction history beside the comparison. These are market sales, not your own retainer's earnings.");
            Toggle("Use clicked comparison price",s.ClickSetsPrice,v=>s.ClickSetsPrice=v,"Click a verified listing to put its adjusted unit price into the open editor. Quality must match unless HQ/NQ crossover below is enabled. A known own retainer at that price is matched exactly, without undercut or rounding; your item minimum still applies.");
            Toggle("Allow HQ/NQ crossover on manual clicks",s.ManualAllowOtherQuality,v=>s.ManualAllowOtherQuality=v,"Manual clicks only: use an NQ listing's price for your HQ item, or an HQ price for NQ. Your item and its quality stay unchanged. Applies to both filling and copying the clicked price. Automatic checks still match quality and materia. Off by default.");
            Toggle("Copy clicked price",s.CopyPrice,v=>s.CopyPrice=v,"Copies the adjusted whole-gil unit price to your clipboard. Works independently of filling the price editor.");
            Toggle("Confirm price after clicking",s.ClickConfirms,v=>s.ClickConfirms=v,"When Use clicked comparison price is on, clicking a price also confirms the listing. Leave off to review and confirm yourself.");
            Toggle("Ctrl: paste price when opening an item",s.CtrlPastePrice,v=>s.CtrlPastePrice=v,"Hold Ctrl while opening the price editor to paste a whole-gil clipboard price instead of opening comparison. Does not confirm the listing.");
            Toggle("Alt: reverse history opening",s.AltTogglesHistory,v=>s.AltTogglesHistory=v,"Hold Alt when opening an item to reverse Also open recent sale history for this item only.");
            Toggle("Limit new stacks",s.LimitNewStacks,v=>s.LimitNewStacks=v,"Limits the quantity when posting new items. Adjusting an existing listing never changes its quantity.");
            if(s.LimitNewStacks)Number("Items per new listing",s.NewStackSize,1,9999,v=>s.NewStackSize=v,"Uses the smaller of this amount, your available quantity and the item's stack limit.");
        }
        if(ImGui.CollapsingHeader("Price calculation")){
            Toggle("Undercut by percentage",s.PercentUndercut,v=>s.PercentUndercut=v,"Off: subtract a fixed gil amount. On: subtract this percentage of the reference unit price.");
            if(s.PercentUndercut)Percent("Undercut",s.UndercutPercent,v=>s.UndercutPercent=v,"Example: 1% below 10,000 gil becomes 9,900 gil.");
            else Number("Gil undercut",s.UndercutGil,0,SellerPricing.MaxPrice,v=>s.UndercutGil=v,"Example: 1 gil below 10,000 becomes 9,999. Zero matches the reference price.");
            Number("Round down to multiple",s.RoundTo,1,1000000,v=>s.RoundTo=v,"Applied after undercutting, and to history-based targets. A multiple of 100 rounds 9,999 down to 9,900. Use 1 for no extra rounding. Matching your own retainer skips both rounding and undercutting.");
        }
        if(ImGui.CollapsingHeader("Automatic pricing rules")){
            MarketHelp("For Check prices, Update prices and background suggestions. Quality and materia always match; manual HQ/NQ crossover does not change these rules.");
            Percent("Ignore lowest price beyond gap",s.LowOutlierPercent,v=>s.LowOutlierPercent=v,"Automatic checks only: compare the lowest and next distinct HQ/NQ prices with the same materia types and grades. Plain and melded items never share a price group. At 40%, 100k versus 250k is ignored, unless your minimum number of recent sales supports the plain low price within 10%. Zero disables this rule. Your manual price click remains your choice.");
            Toggle("Allow raising competitive prices",s.RaiseCompetitivePrices,v=>s.RaiseCompetitivePrices=v,"Allows automatic updates to increase your price to the calculated target. Off keeps an already lower price, subject to your item minimum.");
            Percent("Maximum automatic increase",s.MaximumIncreasePercent,v=>s.MaximumIncreasePercent=v,"Caps any automatic price increase, including own-retainer matches and empty-market history. Default 100% allows at most doubling; 0 blocks increases. Larger suggestions keep your existing price with a hover explanation. Your explicit item minimum can still raise it to that minimum. Manual price clicks remain your choice.",10000);
            Toggle("Use recent sales when comparison is empty",s.UseHistory,v=>s.UseHistory=v,"Update prices can use recent matching HQ/NQ transactions only after a successful comparison has no matching listings at all. Known own listings are matched instead. History is fetched even when Also open recent sale history is off. Errors and timeouts keep the existing price; hover a skipped result for the reason.");
            if(s.UseHistory){
                var rule=(int)s.HistoryRule;ImGui.SetNextItemWidth(220);if(ImGui.Combo("History reference",ref rule,"Median recent sale\0Highest supported recent sale\0Latest supported sale\0")){s.HistoryRule=(SellerHistoryRule)rule;dirty=true;}
                MarketHelp("Choose the middle, highest or most recent price among sales that pass the age and outlier checks. No undercut is applied to history alone.");
                Number("History age (days)",s.HistoryDays,1,90,v=>s.HistoryDays=v,"Ignore transactions older than this many days.");
                Number("Minimum recent sales",s.MinimumHistorySales,1,20,v=>s.MinimumHistorySales=v,"Require at least this many usable transactions before suggesting a history-based price or supporting an unusually low plain listing. Sales do not reveal materia; unmatched melds need manual review.");
                Percent("History outlier distance",s.HistoryOutlierPercent,v=>s.HistoryOutlierPercent=Math.Max(1,v),"Exclude sale prices too far above or below the median. This reduces the effect of unusually large or small transactions.");
            }
        }
        if(ImGui.CollapsingHeader("Background checks & visits")){
            Toggle("Sync Market with paired people",s.SyncToJournal,v=>{s.SyncToJournal=v;marketJournalSource=null;WakeObservationSync();},"Shares observed retainers, jobs, levels, selling lists, visits, sales, spending and price changes through your existing pairing. All clients receive the same records with the Journal closed. Gameplay observations upload first; history uses a separate paced queue. Automation controls remain on this computer.");
            Toggle("Check Universalis in the background",s.BackgroundChecks,v=>{s.BackgroundChecks=v;if(!v)StopMarketWatch();},"Checks known listings on each retainer's home world while the plugin runs, even with windows closed. Suggestions are cached; repricing always uses live game comparisons.");
            if(s.BackgroundChecks){
                Number("Check interval (minutes)",s.BackgroundIntervalMinutes,5,120,v=>s.BackgroundIntervalMinutes=v,"Minimum interval between background checks of each known item. Requests are paced and yield to gameplay syncing.");
                Number("Maximum data age (minutes)",s.BackgroundMaxAgeMinutes,5,1440,v=>s.BackgroundMaxAgeMinutes=v,"Older Universalis information stays uncertain instead of being shown as a reliable update suggestion.");
            }
            Toggle("Remind me to revisit selling retainers",s.VisitReminders,v=>s.VisitReminders=v,"Adds Visit due for known nonempty retainers after the selected time since an observed visit. Price downloads do not reset this timer.");
            if(s.VisitReminders)Number("Visit reminder after days",s.VisitReminderDays,1,14,v=>s.VisitReminderDays=v,"A precautionary reminder, not proof that the game stopped selling. Summon the retainer to refresh its observed visit.");
        }
        if(ImGui.CollapsingHeader("Windows & highlighting")){
            Toggle("Open Seller at summoning bells",s.OpenAtBell,v=>s.OpenAtBell=v,"Opens the compact Check prices / Update prices panel when the bell's retainer list appears.");
            Toggle("Close Seller when leaving the bell",s.CloseAtBell,v=>s.CloseAtBell=v,"Closes Seller after you finish using the summoning bell. It stays open while switching retainers. Turn off to keep Seller open.");
            Toggle("Show selling toolbar",s.ShowToolbar,v=>s.ShowToolbar=v,"Shows stack size and undercut beside the game's selling list.");
            if(s.ShowToolbar)Toggle("Attach toolbar to selling window",s.ToolbarOnTop,v=>s.ToolbarOnTop=v,"On: align with the upper-right area of the game window. Off: drag the toolbar to your preferred location.");
            Toggle("Color retainers and items",s.Highlights,v=>{s.Highlights=v;if(!v)RestoreMarketHighlights();},"Red: suggested update. Green: competitive. Amber: history or uncertain. Blue: checking. Hover a listing for its evidence.");
            Toggle("Show top-bar market count",s.ShowMarketBar,v=>{s.ShowMarketBar=v;if(!v)RemoveMarketBar();},"Click the game top-bar count to open Selling. Right-click opens these settings. A question mark means some listings need a fresh check.");
        }
        if(ImGui.CollapsingHeader("Per-item minimums & exclusions")){
            var id=(int)marketRuleItem;Number("Item ID",id,1,1000000,v=>marketRuleItem=(uint)v,"The numeric game item ID. You can also choose a listing's current price as its minimum from the Selling table.");
            Number("Minimum unit price",marketRuleMinimum,1,SellerPricing.MaxPrice,v=>marketRuleMinimum=v,"Neither automatic suggestions nor manual adjusted price clicks go below this item-specific minimum.");
            if(ImGui.Button("Save minimum")&&marketRuleItem>0){s.MinimumPrices[marketRuleItem]=marketRuleMinimum;dirty=true;}
            ImGui.SameLine();if(ImGui.Button("Exclude item")&&marketRuleItem>0){s.ExcludedItems.Add(marketRuleItem);dirty=true;}
            MarketHelp("Excluded items are skipped by automatic checks and updates. You can still price them manually.");
            foreach(var item in s.MinimumPrices.Keys.Concat(s.ExcludedItems).Distinct().ToArray()){
                ImGui.TextUnformatted(SearchItemName(item)+(s.ExcludedItems.Contains(item)?" · excluded":$" · min {s.MinimumPrices[item]:N0}"));ImGui.SameLine();
                if(ImGui.SmallButton("Remove##rule"+item)){s.MinimumPrices.Remove(item);s.ExcludedItems.Remove(item);dirty=true;}
            }
        }
        ImGui.EndDisabled();if(dirty){marketWatchStock=null;SaveConfiguration();}
    }
    private unsafe void DrawSellerToolbar()
    {
        sellerToolbarFocused=false;
        if(!config.Seller.Enabled||!config.Seller.ShowToolbar||!Player.IsLoaded)return;
        var a=SellerAddon("RetainerSellList");if(a==null)return;
        var flags=ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoFocusOnAppearing;
        if(config.Seller.ToolbarOnTop){
            var width=a->RootNode==null?480*a->Scale:a->RootNode->Width*a->Scale;
            ImGui.SetNextWindowPos(new(a->X+width-40*a->Scale,a->Y+6*a->Scale),ImGuiCond.Always,new Vector2(1,0));flags|=ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings;
        }
        MarketGameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.ColorThemeType,out uint theme);
        var light=theme==1;var blue=theme>=2;
        ImGui.PushStyleColor(ImGuiCol.WindowBg,light?new Vector4(.78f,.77f,.73f,.98f):blue?new Vector4(.08f,.14f,.25f,.98f):new Vector4(.15f,.15f,.15f,.98f));
        ImGui.PushStyleColor(ImGuiCol.Text,light?new Vector4(.12f,.12f,.12f,1):new Vector4(.92f,.90f,.84f,1));
        ImGui.PushStyleColor(ImGuiCol.Border,new Vector4(.55f,.51f,.39f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,light?new Vector4(.88f,.87f,.83f,1):new Vector4(.08f,.08f,.08f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,light?new Vector4(.67f,.66f,.62f,1):new Vector4(.24f,.24f,.24f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.4f,.39f,.34f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.5f,.46f,.34f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(5,3));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(5,2));
        if(ImGui.Begin("##EquinoxSellerToolbar",flags)){
            sellerToolbarFocused=ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            ImGui.BeginDisabled(sellerRun.Running);
            var size=config.Seller.NewStackSize;var limit=config.Seller.LimitNewStacks;
            if(ImGui.Checkbox("Stack",ref limit)){config.Seller.LimitNewStacks=limit;SaveConfiguration();}MarketHelp("Limit the quantity of new listings. Existing stacks keep their quantity.");
            ImGui.SameLine();ImGui.SetNextItemWidth(45);if(ImGui.InputInt("##stack",ref size,0)){config.Seller.NewStackSize=Math.Clamp(size,1,9999);SaveConfiguration();}
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Stack limit for new listings only");
            ImGui.SameLine();ImGui.TextUnformatted("−");ImGui.SameLine();var value=config.Seller.PercentUndercut?(float)config.Seller.UndercutPercent:config.Seller.UndercutGil;ImGui.SetNextItemWidth(55);
            if(ImGui.InputFloat("##undercut",ref value,0,0,"%.0f")&&float.IsFinite(value)){if(config.Seller.PercentUndercut)config.Seller.UndercutPercent=Math.Clamp(value,0,99);else config.Seller.UndercutGil=(int)Math.Clamp(value,0,SellerPricing.MaxPrice);SaveConfiguration();}
            ImGui.SameLine();var mode=config.Seller.PercentUndercut?1:0;ImGui.SetNextItemWidth(50);if(ImGui.Combo("##priceunit",ref mode,"gil\0%\0")){config.Seller.PercentUndercut=mode==1;SaveConfiguration();}
            ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton(sellerRun.Running?"Stop":"Seller")){if(sellerRun.Running)sellerRun.Stop("Stopped from market toolbar.");else OpenEquinoxSeller();}
        }ImGui.End();ImGui.PopStyleVar(2);ImGui.PopStyleColor(7);
    }
    private static void MarketSameLine(float width)
    {
        var right=ImGui.GetCursorScreenPos().X+ImGui.GetContentRegionAvail().X;
        if(ImGui.GetItemRectMax().X+ImGui.GetStyle().ItemSpacing.X+width<=right)ImGui.SameLine();
    }
    private static void MarketFilterCombo(string id,string all,IEnumerable<string> values,ref string selected)
    {
        ImGui.SetNextItemWidth(Math.Min(150,ImGui.GetContentRegionAvail().X));
        if(ImGui.BeginCombo(id,selected.Length==0?all:selected)){
            if(ImGui.Selectable(all,selected.Length==0))selected="";
            foreach(var value in values.Where(x=>x.Length>0).Distinct().Order())if(ImGui.Selectable(value,value==selected))selected=value;
            ImGui.EndCombo();
        }
        MarketHelp(all+" · "+(selected.Length==0?"no restriction":selected));
    }
    private static bool BeginMarketRecords(string id,int columns)
    {
        if(!ImGui.BeginTable(id,columns,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.Resizable|ImGuiTableFlags.ScrollY,new Vector2(0,Math.Max(1,ImGui.GetContentRegionAvail().Y))))return false;
        ImGui.TableSetupScrollFreeze(0,1);return true;
    }
    private void MarketItemName(uint id,string name,bool hq,string help)
    {
        if(ImGui.Selectable(name+(hq?" HQ":"")+"##item"))ImGui.SetClipboardText(name);
        MarketHelp(name+(hq?" · HQ":"")+"\n"+help+"\nClick to copy the item name. Right-click for item actions.");
        ItemRightClick(id);
    }
    private static void MarketNumber(string id,long? amount,string help)
    {
        if(amount is {} value){if(ImGui.Selectable(value.ToString("N0")+"##"+id))ImGui.SetClipboardText(value.ToString());}
        else ImGui.TextDisabled("—");
        MarketHelp(help+(amount.HasValue?"\nClick to copy.":""));
    }
    private string MarketIdentity(MarketRetainer? r,string character,string world)=>
        (r==null?character+" · "+world:$"{r.Character} · {r.World}\n{r.Person} / {r.Account}");
    private void MarketRetainerCell(ulong id,string fallback,string character,string world)
    {
        var r=MarketRetainerById(id);var name=r?.Name??fallback;
        if(r!=null){if(ImGui.Selectable(name+"##retainer"))OpenMarketSelling(id,false);}
        else ImGui.TextUnformatted(name.Length>0?name:"Unassigned");
        MarketHelp(MarketIdentity(r,character,world)+(r!=null?"\nClick to open this retainer's listings.":"\nRetainer was not observed."));
    }
    private bool MatchesMarket(MarketRetainer r)=>
        (marketFocusRetainer==0||r.Id==marketFocusRetainer)&&(marketPerson.Length==0||PairedMarketView.PersonKey(r)==marketPerson)&&(marketAccount.Length==0||PairedMarketView.AccountKey(r)==marketAccount)&&
        (marketCharacter.Length==0||r.CharacterId.ToString()==marketCharacter)&&(marketRetainerFilter.Length==0||r.Id.ToString()==marketRetainerFilter)&&(marketWorldFilter.Length==0||r.World==marketWorldFilter);
    private bool MatchesMarketItem(uint id,string name)=>marketFilter.Length==0||id.ToString()==marketFilter||name.Contains(marketFilter,StringComparison.OrdinalIgnoreCase);
    private void DrawLedgerFilters(bool purchases=false)
    {
        var beforePerson=marketPerson;var beforeAccount=marketAccount;
        MarketScopeCombo("##market-person","All persons",MarketRetainers.Select(x=>(PairedMarketView.PersonKey(x),x.Person.Length>0?x.Person:"Unassigned")),ref marketPerson);
        if(beforePerson!=marketPerson){marketAccount=marketCharacter=marketRetainerFilter="";marketFocusRetainer=0;}
        MarketSameLine(155);
        MarketScopeCombo("##market-account","All accounts",MarketRetainers.Where(x=>marketPerson.Length==0||PairedMarketView.PersonKey(x)==marketPerson).Select(x=>(PairedMarketView.AccountKey(x),x.Person+" / "+(x.Account.Length>0?x.Account:"Unassigned"))),ref marketAccount);
        if(beforeAccount!=marketAccount){marketCharacter=marketRetainerFilter="";marketFocusRetainer=0;}
        ImGui.SetNextItemWidth(Math.Min(240,ImGui.GetContentRegionAvail().X));ImGui.InputTextWithHint("##marketfilter","Search items / item ID",ref marketFilter,100);
        var activeFilters=(marketPerson.Length>0?1:0)+(marketAccount.Length>0?1:0)+(marketCharacter.Length>0?1:0)+(marketWorldFilter.Length>0?1:0)+(!purchases&&marketRetainerFilter.Length>0?1:0);
        MarketSameLine(100);if(ImGui.Button((activeFilters>0?$"Filters · {activeFilters}":"Filters")+"###market-filter-button"))ImGui.OpenPopup("market-filters");
        if(ImGui.IsPopupOpen("market-filters"))ImGui.SetNextWindowSize(new Vector2(ImGui.GetFontSize()*20,0),ImGuiCond.Always);
        if(ImGui.BeginPopup("market-filters")){
            var all=MarketRetainers;
            MarketScopeCombo("##char","All characters",all.Where(MatchesMarketScope).Select(x=>(x.CharacterId.ToString(),x.Character+" · "+x.World)),ref marketCharacter);
            if(!purchases)MarketScopeCombo("##retainer","All retainers",all.Where(MatchesMarketScope).Where(x=>marketCharacter.Length==0||x.CharacterId.ToString()==marketCharacter).Select(x=>(x.Id.ToString(),x.Name+" · "+x.Character)),ref marketRetainerFilter);
            MarketFilterCombo("##world","All worlds",all.Select(x=>x.World),ref marketWorldFilter);
            if(ImGui.SmallButton("Clear filters")){marketPerson=marketAccount=marketCharacter=marketRetainerFilter=marketWorldFilter=marketFilter="";marketFocusRetainer=0;marketAttentionOnly=false;marketUpdatesOnly=false;}
            ImGui.EndPopup();
        }
        if(!purchases&&marketFocusRetainer!=0){
            MarketSameLine(150);if(ImGui.SmallButton((MarketRetainerById(marketFocusRetainer)?.Name??"Selected retainer")+" ×##focus")){marketFocusRetainer=0;marketPage=0;}
            MarketHelp("This retainer is selected. Click to show all retainers again.");
        }
    }
    private void DrawEquinoxMarket()
    {
        if(!MarketEnabledUi())return;
        if(config.SyncEnabled&&config.SharedRoster is {MarketProjectionVersion:0}){ImGui.TextDisabled("Paired Market needs Journal / Worker 7.11.243.");MarketHelp("Update both Cloudflare deployments. Existing local records stay available; no re-pairing is needed.");}
        if(marketDataError.Length>0)ImGui.TextWrapped(marketDataError);
        if(ImGui.BeginTabBar("EquinoxMarketPages",ImGuiTabBarFlags.FittingPolicyScroll)){
            void Tab(string label,Action body){
                var select=label=="Settings"&&openMarketSettings||label=="Selling"&&openMarketSelling;
                if(!ImGui.BeginTabItem(label,select?ImGuiTabItemFlags.SetSelected:ImGuiTabItemFlags.None))return;
                if(select){openMarketSettings=false;openMarketSelling=false;}
                if(marketActiveTab!=label){marketActiveTab=label;marketPage=0;}
                var before=MarketViewStamp;
                // Controls stay above the scrolling records in every tab.
                if(label is not ("Settings" or "Files"))DrawLedgerFilters(label=="Spending");
                if(label is "Sales" or "Summary" or "Spending" or "Price changes")MarketDateFilter(label is "Sales" or "Summary");
                if(before!=MarketViewStamp)marketPage=0;
                if(ImGui.BeginChild("marketbody"+label,new Vector2(0,0),false))body();
                ImGui.EndChild();ImGui.EndTabItem();
            }
            Tab("Selling",DrawMarketSelling);Tab("Retainers",DrawMarketRetainers);Tab("Sales",DrawMarketIncome);Tab("Summary",DrawMarketSummary);Tab("Spending",DrawMarketSpending);Tab("Price changes",DrawMarketChanges);Tab("Files",DrawMarketFiles);Tab("Settings",DrawSellerSettings);ImGui.EndTabBar();
        }
        windowFooter=()=>{if(ImGui.SmallButton("Seller"))OpenEquinoxSeller();MarketHelp(marketSharedStatus+"\nCurrent listings: "+syncStatus);};windowStatus=marketDirty||marketWriter.Busy?"Saving market records…":config.SyncEnabled&&config.SharedRoster is {MarketProjectionVersion:>=1}?"Paired Market":"Local Market";
    }
    private void DrawMarketSelling()
    {
        ImGui.Checkbox("Needs attention only",ref marketAttentionOnly);
        MarketSameLine(160);ImGui.Checkbox("Price updates only",ref marketUpdatesOnly);
        if(config.Seller.BackgroundChecks){if(ImGui.SmallButton("Refresh market data")){marketWatchNext.Clear();marketWatchStock=null;}MarketHelp("Queue a fresh Universalis check for known listings on their own worlds. Requests stay paced; use Seller for live in-game comparisons.");ImGui.SameLine();ImGui.TextDisabled(marketWatchTask!=null?"Checking prices…":"Background prices");if(ImGui.IsItemHovered())ImGui.SetTooltip(marketWatchNotice+"\nUniversalis is cached; live checks take priority.");}
        var visible=OrderedMarketRetainers().Select(r=>(Retainer:r,Rows:r.Stock.OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Slot)
            .Where(x=>MatchesMarketItem(x.ItemId,x.Name)&&(!marketAttentionOnly||ReviewFor(r.Id,x.Slot).NeedsAttention||ReviewFor(r.Id,x.Slot).Status!=SellerStatus.Competitive)&&(!marketUpdatesOnly||ReviewFor(r.Id,x.Slot).NeedsAttention)).ToArray())).Where(x=>x.Rows.Length>0).ToArray();
        var shown=visible.SelectMany(x=>x.Rows).ToArray();
        ImGui.TextDisabled($"{shown.Length} listings shown · asking value {shown.Sum(x=>x.Total):N0} gil");MarketHelp("These totals follow the current item and status filters. Listed value is not earned gil.");
        foreach(var group in visible.GroupBy(x=>x.Retainer.CharacterId)){
            var character=group.First().Retainer;ImGui.Separator();
            MarketCharacterHeading(character);
            DrawMarketCharacterAction(character,group.Select(x=>x.Retainer).ToArray());
            foreach(var entry in group){
            var r=entry.Retainer;var rows=entry.Rows;
            var states=r.Stock.Select(x=>ReviewFor(r.Id,x.Slot)).ToArray();var needed=states.Count(x=>x.NeedsAttention);
            var color=SellerColor(needed>0?SellerStatus.Update:AggregateMarketStatus(states.Select(x=>x.VisualStatus).ToArray()));
            ImGui.PushStyleColor(ImGuiCol.Text,color);
            var open=ImGui.CollapsingHeader(MarketRetainerLabel(r)+" · "+SellerLabels.Selling(r.Stock.Length,needed,states.Any(x=>x.Status==SellerStatus.Unknown),states.Count(x=>x.Status==SellerStatus.Skipped),states.Any(x=>x.Status==SellerStatus.Checking))+"###retainer"+r.Id);
            ImGui.PopStyleColor();
            MarketRetainerHover(r);
            if(!open)continue;
            var wide=ImGui.GetContentRegionAvail().X>=ImGui.GetFontSize()*38;
            if(ImGui.BeginTable("selling-"+r.Id+(wide?"Wide":"Compact"),wide?5:4,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.Resizable)){
                ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,3);
                ImGui.TableSetupColumn("Qty",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*2.5f);
                ImGui.TableSetupColumn("Each",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*6);
                ImGui.TableSetupColumn("Suggested",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*6);
                if(wide)ImGui.TableSetupColumn("Status",ImGuiTableColumnFlags.WidthStretch,1);
                ImGui.TableHeadersRow();
                foreach(var x in rows){var decision=ReviewFor(r.Id,x.Slot);ImGui.PushID($"{r.Id}/{x.Slot}");
                    ImGui.TableNextRow();ImGui.TableNextColumn();
                    ImGui.PushStyleColor(ImGuiCol.Text,SellerColor(decision.VisualStatus));
                    if(ImGui.Selectable(x.Name+(x.Hq?" HQ":"")+"##item",false))OpenMarketItem(r,x);
                    ImGui.PopStyleColor();MarketHelp($"{SellerMateria.Describe(x.Materia)}\n{decision.Reason}\nClick: open this listing's price editor while its retainer is summoned. Right-click: item actions.\nListed {DisplayTime.Local(x.ListedAt)}");ItemRightClick(x.ItemId);
                    ImGui.TableNextColumn();ImGui.TextUnformatted(x.Quantity.ToString());
                    ImGui.TableNextColumn();if(ImGui.Selectable(x.Price.ToString("N0"),false))ImGui.SetClipboardText(x.Price.ToString());
                    MarketHelp($"Click to copy unit price. Stack value: {x.Total:N0} gil. Right-click for item pricing rules.");
                    if(ImGui.BeginPopupContextItem("rule")){
                        if(ImGui.MenuItem("Use this price as minimum")){config.Seller.MinimumPrices[x.ItemId]=x.Price;SaveConfiguration();}
                        if(ImGui.MenuItem("Exclude from automatic pricing")){config.Seller.ExcludedItems.Add(x.ItemId);SaveConfiguration();}
                        ImGui.EndPopup();
                    }
                    ImGui.TableNextColumn();ImGui.TextUnformatted(decision.CanApply?decision.Price!.Value.ToString("N0"):"—");MarketHelp(decision.Source+" · "+decision.Reason);
                    if(wide){ImGui.TableNextColumn();ImGui.TextColored(SellerColor(decision.VisualStatus),decision.Undercut&&!decision.CanApply?"Lower listing":MarketDecisionLabel(r,x,decision));MarketHelp(decision.Reason);}
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }

        }
        }
        if(visible.Length==0)ImGui.TextWrapped(MarketRetainers.Length==0?"Open each retainer’s selling list to record it. Check prices visits selected retainers automatically.":"No listings match these filters.");
    }
    private void DrawMarketRetainers()
    {
        foreach(var group in OrderedMarketRetainers().GroupBy(r=>r.CharacterId)){
            var character=group.First();ImGui.Separator();
            MarketCharacterHeading(character);
            DrawMarketCharacterAction(character,group.ToArray());
            if(!ImGui.BeginTable("retainers-"+group.Key,4,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.SizingStretchProp|ImGuiTableFlags.Resizable))continue;
            ImGui.TableSetupColumn("Retainer");ImGui.TableSetupColumn("Selling",ImGuiTableColumnFlags.WidthStretch,1.6f);ImGui.TableSetupColumn("Asking value");ImGui.TableSetupColumn("Visit");ImGui.TableHeadersRow();
            foreach(var r in group){
                var states=r.Stock.Select(x=>ReviewFor(r.Id,x.Slot)).ToArray();
                var updates=states.Count(x=>x.NeedsAttention);var unknown=states.Count(x=>x.Status==SellerStatus.Unknown);
                var color=SellerColor(updates>0?SellerStatus.Update:AggregateMarketStatus(states.Select(x=>x.VisualStatus).ToArray()));
                ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.PushStyleColor(ImGuiCol.Text,color);if(ImGui.Selectable(MarketRetainerLabel(r)+"##"+r.Id,false))OpenMarketSelling(r.Id,false);ImGui.PopStyleColor();MarketRetainerHover(r);
                ImGui.TableNextColumn();ImGui.TextColored(color,SellerLabels.Selling(r.Stock.Length,updates,unknown>0,states.Count(x=>x.Status==SellerStatus.Skipped),states.Any(x=>x.Status==SellerStatus.Checking)));MarketRetainerHover(r);
                ImGui.TableNextColumn();ImGui.TextUnformatted(r.Stock.Sum(x=>x.Total).ToString("N0"));MarketHelp("Total listed value. This is not earned gil.");
                ImGui.TableNextColumn();ImGui.TextUnformatted(MarketVisitDue(r)?"Due":"—");MarketHelp("Last observed visit: "+DisplayTime.Local(r.LastVisit??r.At));
            }
            ImGui.EndTable();
        }
        if(MarketRetainers.Length==0)ImGui.TextWrapped("Open each retainer's selling list to record its listings.");
    }
    private bool MarketVisitDue(MarketRetainer r)=>config.Seller.VisitReminders&&MarketAccounting.VisitDue(r,DateTimeOffset.UtcNow,config.Seller.VisitReminderDays);
    private MarketRetainer[] OrderedMarketRetainers()=>MarketRetainers.Where(MatchesMarket).OrderBy(r=>r.Person).ThenBy(r=>r.Account).ThenBy(r=>r.World).ThenBy(r=>r.Character).ThenBy(r=>r.DisplayOrder).ThenBy(r=>r.Name).ToArray();
    private void DrawMarketCharacterAction(MarketRetainer r,MarketRetainer[] group)
    {
        ImGui.PushID("market-travel-"+r.Id);
        if(Player.IsLoaded&&Player.ContentId==r.CharacterId){
            if(ImGui.SmallButton("Open Seller")){sellerScope=1;config.Seller.SelectedRetainers=group.Select(x=>x.Id).ToHashSet();SaveConfiguration();OpenEquinoxSeller();}
            MarketHelp($"Selects the {group.Length} displayed retainers for this character. Use a summoning bell on {r.World}.");
            MarketSameLine(190);ImGui.TextDisabled(Player.CurrentWorld.RowId==r.WorldId?"Use a summoning bell":"Return to "+r.World);
        }else{
            var character=config.SharedRoster?.People.SelectMany(p=>p.Characters).FirstOrDefault(c=>c.Id==r.SharedCharacterId||c.ContentId==r.CharacterId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(character!=null)DrawCharacterTravel(character,false);else ImGui.TextDisabled("Pair this character to enable switching.");
        }
        ImGui.PopID();
    }
    private MarketIncome[] FilteredMarketIncome()=>marketIncomeView.Get(MarketHistoryLedger.Income,MarketViewStamp,Environment.TickCount64,()=>{
        var cutoff=marketDate switch {0=>DateTimeOffset.Now.Date,1=>DateTimeOffset.UtcNow.AddDays(-7),2=>DateTimeOffset.UtcNow.AddDays(-30),_=>DateTimeOffset.MinValue};
        return MarketHistoryLedger.Income.Where(x=>(marketSource switch {1=>x.Net.HasValue,2=>x.Id.StartsWith("allagan:"),3=>x.Id.StartsWith("inferred:"),_=>true})&&x.At>=cutoff&&MatchesMarketItem(x.ItemId,x.Item.Length>0?x.Item:SearchItemName(x.ItemId))&&
            (MarketRetainerById(x.RetainerId) is {} r?MatchesMarket(r):MatchesMarketOwner(x.CharacterId)&&marketFocusRetainer==0&&marketRetainerFilter.Length==0&&(marketCharacter.Length==0||x.CharacterId.ToString()==marketCharacter)&&(marketWorldFilter.Length==0||x.World==marketWorldFilter))).OrderByDescending(x=>x.At).ToArray();
    });
    private DateTimeOffset MarketCutoff=>marketDate switch {0=>DateTimeOffset.Now.Date,1=>DateTimeOffset.UtcNow.AddDays(-7),2=>DateTimeOffset.UtcNow.AddDays(-30),_=>DateTimeOffset.MinValue};
    private void MarketDateFilter(bool income=true)
    {
        ImGui.SetNextItemWidth(Math.Min(150,ImGui.GetContentRegionAvail().X));
        if(ImGui.Combo("##market-period",ref marketDate,"Today\0Last 7 days\0Last 30 days\0All recorded\0"))marketPage=0;
        MarketHelp("Time period for the records and totals below. Today uses this computer's local date.");
        if(income){
            MarketSameLine(160);ImGui.SetNextItemWidth(Math.Min(160,ImGui.GetContentRegionAvail().X));
            if(ImGui.Combo("##market-source",ref marketSource,"All sources\0Confirmed sales\0Imported history\0Inferred sales\0"))marketPage=0;
            MarketHelp("Confirmed sales have a game-observed net amount. Imported and inferred records remain labelled by their evidence.");
        }
    }
    private void MarketPaging(int count){marketPage=Math.Clamp(marketPage,0,Math.Max(0,(count-1)/80));if(count<=80)return;if(ImGui.SmallButton("Previous")&&marketPage>0)marketPage--;ImGui.SameLine();ImGui.TextDisabled($"{marketPage+1}/{(count+79)/80}");ImGui.SameLine();if(ImGui.SmallButton("Next")&&(marketPage+1)*80<count)marketPage++;}
    private void DrawMarketSummary()
    {
        var rows=FilteredMarketIncome();
        var totals=marketIncomeTotals.Get(rows,"",Environment.TickCount64,()=>(rows.Sum(x=>x.Net??0),rows.Any(x=>x.Net==null)));
        ImGui.TextUnformatted($"Confirmed net: {totals.Net:N0} gil");
        MarketHelp("Game sale receipts confirm net gil after fees. Recorded gross includes imported and inferred sales; hover a sale for evidence. This is revenue, not profit.\n"+marketHistoryStatus);
        ImGui.SetNextItemWidth(160);if(ImGui.Combo("Group",ref marketOrder,"Item\0Retainer\0Character\0World\0"))marketPage=0;
        var groups=marketSummaryView.Get(rows,marketOrder.ToString(),Environment.TickCount64,()=>rows.GroupBy(x=>marketOrder switch{1=>MarketRetainerById(x.RetainerId)?.Name??x.Retainer,2=>MarketRetainerById(x.RetainerId)?.Character??x.Character,3=>MarketRetainerById(x.RetainerId)?.World??x.World,_=>(x.Item.Length>0?x.Item:SearchItemName(x.ItemId))+(x.Hq?" HQ":"")}).Select(g=>new MarketSummaryRow(g.Key,g.Sum(x=>x.Net??0),g.Sum(x=>x.Gross??0),g.Sum(x=>(long)x.Quantity),g.Count())).OrderByDescending(x=>x.Net).ThenByDescending(x=>x.Gross).ToArray());
        MarketPaging(groups.Length);
        if(BeginMarketRecords("marketSummary",4)){
            ImGui.TableSetupColumn("Name");ImGui.TableSetupColumn("Units");ImGui.TableSetupColumn("Recorded gross");ImGui.TableSetupColumn("Confirmed net");ImGui.TableHeadersRow();
            foreach(var g in groups.Skip(marketPage*80).Take(80)){ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextUnformatted(g.Name);MarketHelp(g.Name+$"\n{g.Count} sale records");ImGui.TableNextColumn();ImGui.TextUnformatted(g.Units.ToString("N0"));ImGui.TableNextColumn();ImGui.TextUnformatted(g.Gross.ToString("N0"));ImGui.TableNextColumn();ImGui.TextUnformatted(g.Net.ToString("N0"));}ImGui.EndTable();
        }
    }
    private void DrawMarketFiles()
    {
        if(ImGui.CollapsingHeader("Export & backup",ImGuiTreeNodeFlags.DefaultOpen)){
        ImGui.BeginDisabled(marketExport!=null);
        if(ImGui.Button("Sales CSV")){var rows=FilteredMarketIncome();var folder=Path.Combine(Pi.GetPluginConfigDirectory(),"exports");marketExport=Task.Run(()=>{Directory.CreateDirectory(folder);var path=Path.Combine(folder,"Equinox-Market-sales-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv");File.WriteAllText(path,MarketAccounting.Export(rows));return path;});}
        MarketHelp("Export the Sales tab’s current filters and time period as CSV.");MarketSameLine(140);
        if(ImGui.Button("Full backup")){var ledger=marketLedger;var folder=Path.Combine(Pi.GetPluginConfigDirectory(),"exports");marketExport=Task.Run(()=>{Directory.CreateDirectory(folder);var path=Path.Combine(folder,"Equinox-Market-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(ledger));return path;});}
        MarketHelp("Export all locally recorded retainers, listings, sales, purchases and price checks as JSON.");
        ImGui.EndDisabled();MarketSameLine(150);if(ImGui.Button("Open export folder"))OpenDiagnosticFolder(Path.Combine(Pi.GetPluginConfigDirectory(),"exports"));
        }
        if(ImGui.CollapsingHeader("Import Allagan Market sales")){
            ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##allagancsv","CSV file path",ref marketImportPath,1024);MarketHelp("Export Sale History as CSV in Allagan Market, then paste the full file path here. Timestamps use this computer’s local timezone. Existing records are kept.");
            ImGui.BeginDisabled(marketImport!=null);if(ImGui.Button("Import sales history"))ImportAllaganSales();ImGui.EndDisabled();
        }
        if(marketFileStatus.Length>0)ImGui.TextWrapped(marketFileStatus);
        if(marketSaleReceipts.Count<2)ImGui.TextWrapped("Some game sale message templates could not be verified. Those receipts will not be guessed; sale imports remain available.");
    }
}
