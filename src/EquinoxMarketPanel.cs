using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private EquinoxMarketWindow equinoxMarketWindow=null!;
    private EquinoxSellerWindow sellerWindow=null!;
    private bool eqMarketCommand,eqSellerCommand;
    private bool openMarketSettings,openMarketSelling,marketUpdatesOnly;
    private ulong marketFocusRetainer;
    private int sellerScope=2,marketPage,marketDate=2,marketOrder,marketSource;
    private bool marketAttentionOnly;
    private string marketFilter="",marketPerson="",marketAccount="",marketCharacter="",marketRetainerFilter="",marketWorldFilter="";
    private uint marketRuleItem;
    private int marketRuleMinimum=1;
    private readonly UiProjectionCache<MarketIncome[]> marketIncomeView=new(5000);
    private readonly UiProjectionCache<MarketExpense[]> marketExpenseView=new(5000);
    private readonly UiProjectionCache<MarketSummaryRow[]> marketSummaryView=new(5000);
    private readonly UiProjectionCache<(long Net,bool Unverified)> marketIncomeTotals=new(0);
    private readonly UiProjectionCache<long> marketExpenseTotal=new(0);
    private sealed record MarketSummaryRow(string Name,long Net,long Gross,long Units,int Count);
    private MarketRetainer[]? marketRetainerIndexSource;
    private Dictionary<ulong,MarketRetainer> marketRetainerIndex=[];
    private MarketRetainer? MarketRetainerById(ulong id){
        if(!ReferenceEquals(marketRetainerIndexSource,marketLedger.Retainers)){marketRetainerIndexSource=marketLedger.Retainers;marketRetainerIndex=marketLedger.Retainers.DistinctBy(x=>x.Id).ToDictionary(x=>x.Id);}
        return marketRetainerIndex.GetValueOrDefault(id);
    }
    private string MarketViewStamp=>string.Join("\u001f",marketDate.ToString(),marketSource.ToString(),marketFocusRetainer.ToString(),marketFilter,marketPerson,marketAccount,marketCharacter,marketRetainerFilter,marketWorldFilter);
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
    }
    private void DisposeMarketWindows(){if(eqMarketCommand)Commands.RemoveHandler("/eqmarket");if(eqSellerCommand)Commands.RemoveHandler("/eqseller");}
    private void OpenEquinoxMarket(){equinoxMarketWindow.IsOpen=true;minimizedLaunchers.Remove("Equinox Market");}
    private void OpenEquinoxSeller(){sellerWindow.IsOpen=true;minimizedLaunchers.Remove("Equinox Seller");}
    private static Vector4 SellerColor(SellerStatus status)=>status switch {SellerStatus.Update=>new(1,.35f,.35f,1),SellerStatus.Competitive=>new(.35f,.85f,.50f,1),SellerStatus.Checking=>new(.4f,.75f,1,1),SellerStatus.History or SellerStatus.Skipped=>new(1,.75f,.35f,1),_=>new(.65f,.7f,.75f,1)};
    private SellerDecision ReviewFor(ulong retainer,int slot)
    {
        var current=MarketRetainerById(retainer)?.Stock.FirstOrDefault(x=>x.Slot==slot);
        if(current!=null&&sellerReviews.TryGetValue((retainer,slot),out var r)&&r.Item.SameItem(current)&&r.Item.Price==current.Price&&DateTimeOffset.UtcNow-r.At<=TimeSpan.FromMinutes(5))return r.Decision;
        if(config.Seller.BackgroundChecks&&current!=null&&marketWatchReviews.TryGetValue((retainer,slot),out var watch)&&watch.Item.SameItem(current)&&watch.Item.Price==current.Price)return watch.Decision;
        return new(SellerStatus.Unknown,null,"Not compared this session.","None");
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
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Visits selected retainers and compares each listing. Does not change any price.");
        ImGui.SameLine();if(ImGui.Button("Update prices"))StartSeller(true);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Checks live prices again, then applies your rules. Existing stack quantities stay unchanged.");
        ImGui.EndDisabled();
        ImGui.SameLine();
        var settingsSize=ImGui.GetFrameHeight();var settingsAt=ImGui.GetCursorScreenPos();
        if(ImGui.InvisibleButton("Seller settings",new Vector2(settingsSize))){openMarketSettings=true;openMarketSelling=false;OpenEquinoxMarket();}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Seller settings · pricing, stacks, toolbar and background checks");
        GardenImage("assets/category-icons/options.png",settingsAt,new Vector2(settingsSize));
        if(sellerRun.Running){
            if(ImGui.Button(sellerRun.Paused?"Resume":"Pause"))sellerRun.Pause(DateTimeOffset.UtcNow);
            ImGui.SameLine();if(ImGui.Button("Stop"))sellerRun.Stop("Stopped. Already confirmed changes are kept.");
        }
        ImGui.TextWrapped(sellerRun.Status);
        if(sellerNotice.Length>0)ImGui.TextWrapped(sellerNotice);
        if(ImGui.CollapsingHeader("Pricing & stack size"))DrawSellerSettings(false);
        windowFooter=()=>{if(ImGui.SmallButton("Market"))OpenEquinoxMarket();};
    }
    private void DrawSellerSettings()=>DrawSellerSettings(true);
    private void DrawSellerSettings(bool full)
    {
        var s=config.Seller;var dirty=false;
        if(full){var enabled=s.Enabled;if(ImGui.Checkbox("Enable Equinox Market & Seller",ref enabled))SetMarketEnabled(enabled);}
        ImGui.BeginDisabled(sellerRun.Running);
        void Toggle(string label,bool value,Action<bool> set){if(ImGui.Checkbox(label,ref value)){set(value);dirty=true;}}
        void Number(string label,int value,int min,int max,Action<int> set){ImGui.SetNextItemWidth(100);if(ImGui.InputInt(label,ref value)){set(Math.Clamp(value,min,max));dirty=true;}}
        void Percent(string label,double value,Action<double> set){var f=(float)value;ImGui.SetNextItemWidth(100);if(ImGui.InputFloat(label,ref f,1,5,"%.1f%%")){set(float.IsFinite(f)?Math.Clamp(f,0,99):0);dirty=true;}}
        Toggle("Open Seller at summoning bells",s.OpenAtBell,v=>s.OpenAtBell=v);
        Toggle("Show market toolbar",s.ShowToolbar,v=>s.ShowToolbar=v);
        if(s.ShowToolbar)Toggle("Toolbar on top of market window",s.ToolbarOnTop,v=>s.ToolbarOnTop=v);
        SettingsHelp("Uncheck to use a movable toolbar. The Seller and Market windows can also be minimized to icons.");
        Toggle("Color retainers and items",s.Highlights,v=>{s.Highlights=v;if(!v)RestoreMarketHighlights();});
        Toggle("Show top-bar price update count",s.ShowMarketBar,v=>{s.ShowMarketBar=v;if(!v)RemoveMarketBar();});
        Toggle("Remind me to revisit selling retainers",s.VisitReminders,v=>s.VisitReminders=v);
        if(s.VisitReminders)Number("Visit reminder after days",s.VisitReminderDays,1,14,v=>s.VisitReminderDays=v);
        SettingsHelp("Default: six days since the last observed visit, while the retainer has known listings. A visit reminder is not proof that the game has stopped selling. Background price checks do not reset visits.");
        SettingsHelp("Click the top-bar count to open Selling filtered to suggested updates across known retainers. Right-click opens Seller settings. A question mark means some listings are still uncertain.");
        Toggle("Check listed items on Universalis in the background",s.BackgroundChecks,v=>{s.BackgroundChecks=v;if(!v)StopMarketWatch();});
        if(s.BackgroundChecks){Number("Background interval (minutes)",s.BackgroundIntervalMinutes,5,120,v=>s.BackgroundIntervalMinutes=v);Number("Maximum data age (minutes)",s.BackgroundMaxAgeMinutes,5,1440,v=>s.BackgroundMaxAgeMinutes=v);}
        SettingsHelp("Checks known listings on each retainer's home world, even with these windows closed. Cached suggestions only: automatic price changes always use a fresh in-game comparison. Old/missing data stays uncertain.");
        Toggle("Limit new stacks",s.LimitNewStacks,v=>s.LimitNewStacks=v);
        if(s.LimitNewStacks)Number("Items per new listing",s.NewStackSize,1,9999,v=>s.NewStackSize=v);
        SettingsHelp("Only when posting new items. Existing listings keep their quantity. Limited by available items and the item's stack maximum.");
        Toggle("Undercut by percentage",s.PercentUndercut,v=>s.PercentUndercut=v);
        if(s.PercentUndercut)Percent("Undercut",s.UndercutPercent,v=>s.UndercutPercent=v);else Number("Gil undercut",s.UndercutGil,0,SellerPricing.MaxPrice,v=>s.UndercutGil=v);
        Number("Round down to multiple",s.RoundTo,1,1000000,v=>s.RoundTo=v);
        Percent("Ignore low price beyond gap",s.LowOutlierPercent,v=>s.LowOutlierPercent=v);
        SettingsHelp("Gap is measured against the next distinct price, per unit and matching HQ/NQ. Example: 100k, 250k, 260k has a 60% gap. At 40%, use 250k before undercutting. Zero disables this rule.");
        Toggle("Allow raising already competitive prices",s.RaiseCompetitivePrices,v=>s.RaiseCompetitivePrices=v);
        Toggle("Use recent sales when market is empty",s.UseHistory,v=>s.UseHistory=v);
        if(s.UseHistory){
            var rule=(int)s.HistoryRule;ImGui.SetNextItemWidth(190);if(ImGui.Combo("History price",ref rule,"Median recent sale\0Highest supported recent sale\0Latest supported sale\0")){s.HistoryRule=(SellerHistoryRule)rule;dirty=true;}
            Number("History age in days",s.HistoryDays,1,90,v=>s.HistoryDays=v);
            Number("Minimum recent sales",s.MinimumHistorySales,1,20,v=>s.MinimumHistorySales=v);
            Percent("History outlier distance",s.HistoryOutlierPercent,v=>s.HistoryOutlierPercent=Math.Max(1,v));
            SettingsHelp("Only a successful empty comparison uses history. Prices outside this percentage of the median are excluded. No undercut is applied without competing listings; stale or insufficient history keeps your price.");
        }
        if(full&&ImGui.CollapsingHeader("Manual pricing conveniences")){
            Toggle("Open compare prices automatically",s.OpenCompare,v=>s.OpenCompare=v);SettingsHelp("Hold Shift while opening an item to bypass automatic comparison.");
            Toggle("Open recent sale history",s.OpenHistory,v=>s.OpenHistory=v);
            Toggle("Click a comparison to set price",s.ClickSetsPrice,v=>s.ClickSetsPrice=v);
            Toggle("Confirm after clicking a price",s.ClickConfirms,v=>s.ClickConfirms=v);
            Toggle("Copy selected adjusted price",s.CopyPrice,v=>s.CopyPrice=v);
            Toggle("Ctrl: paste clipboard price when opening an item",s.CtrlPastePrice,v=>s.CtrlPastePrice=v);SettingsHelp("Accepts a whole gil amount only. Sets the unit price without confirming the listing.");
            Toggle("Alt: reverse automatic history opening",s.AltTogglesHistory,v=>s.AltTogglesHistory=v);
        }
        if(full&&ImGui.CollapsingHeader("Item rules")){
            var id=(int)marketRuleItem;Number("Item ID",id,1,1000000,v=>marketRuleItem=(uint)v);
            Number("Minimum unit price",marketRuleMinimum,1,SellerPricing.MaxPrice,v=>marketRuleMinimum=v);
            if(ImGui.Button("Save minimum")&&marketRuleItem>0){s.MinimumPrices[marketRuleItem]=marketRuleMinimum;dirty=true;}
            ImGui.SameLine();if(ImGui.Button("Exclude item")&&marketRuleItem>0){s.ExcludedItems.Add(marketRuleItem);dirty=true;}
            foreach(var item in s.MinimumPrices.Keys.Concat(s.ExcludedItems).Distinct().ToArray()){
                ImGui.TextUnformatted(SearchItemName(item)+(s.ExcludedItems.Contains(item)?" · excluded":$" · min {s.MinimumPrices[item]:N0}"));ImGui.SameLine();
                if(ImGui.SmallButton("Remove##rule"+item)){s.MinimumPrices.Remove(item);s.ExcludedItems.Remove(item);dirty=true;}
            }
        }
        ImGui.EndDisabled();if(dirty){marketWatchStock=null;SaveConfiguration();}
    }
    private unsafe void DrawSellerToolbar()
    {
        if(!config.Seller.Enabled||!config.Seller.ShowToolbar||!Player.IsLoaded)return;
        var a=SellerAddon("RetainerSellList");if(a==null)return;
        var flags=ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoFocusOnAppearing;
        if(config.Seller.ToolbarOnTop){ImGui.SetNextWindowPos(new(a->X+55*a->Scale,a->Y+8*a->Scale),ImGuiCond.Always);flags|=ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings;}
        if(ImGui.Begin("##EquinoxSellerToolbar",flags)){
            ImGui.BeginDisabled(sellerRun.Running);
            var size=config.Seller.NewStackSize;var limit=config.Seller.LimitNewStacks;
            if(ImGui.Checkbox("Stack",ref limit)){config.Seller.LimitNewStacks=limit;SaveConfiguration();}
            ImGui.SameLine();ImGui.SetNextItemWidth(45);if(ImGui.InputInt("##stack",ref size,0)){config.Seller.NewStackSize=Math.Clamp(size,1,9999);SaveConfiguration();}
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Stack limit for new listings only");
            ImGui.SameLine();var value=config.Seller.PercentUndercut?(float)config.Seller.UndercutPercent:config.Seller.UndercutGil;ImGui.SetNextItemWidth(55);
            if(ImGui.InputFloat("##undercut",ref value,0,0,"%.0f")&&float.IsFinite(value)){if(config.Seller.PercentUndercut)config.Seller.UndercutPercent=Math.Clamp(value,0,99);else config.Seller.UndercutGil=(int)Math.Clamp(value,0,SellerPricing.MaxPrice);SaveConfiguration();}
            ImGui.SameLine();var mode=config.Seller.PercentUndercut?1:0;ImGui.SetNextItemWidth(50);if(ImGui.Combo("##priceunit",ref mode,"gil\0%\0")){config.Seller.PercentUndercut=mode==1;SaveConfiguration();}
            ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton(sellerRun.Running?"Stop":"Seller")){if(sellerRun.Running)sellerRun.Stop("Stopped from market toolbar.");else OpenEquinoxSeller();}
        }ImGui.End();
    }
    private static void MarketFilterCombo(string id,string all,IEnumerable<string> values,ref string selected)
    {
        ImGui.SetNextItemWidth(130);if(!ImGui.BeginCombo(id,selected.Length==0?all:selected))return;
        if(ImGui.Selectable(all,selected.Length==0))selected="";
        foreach(var s in values.Where(x=>x.Length>0).Distinct().Order())if(ImGui.Selectable(s,s==selected))selected=s;
        ImGui.EndCombo();
    }
    private bool MatchesMarket(MarketRetainer r)=>
        (marketFocusRetainer==0||r.Id==marketFocusRetainer)&&(marketPerson.Length==0||r.Person==marketPerson)&&(marketAccount.Length==0||r.Account==marketAccount)&&
        (marketCharacter.Length==0||r.Character==marketCharacter)&&(marketRetainerFilter.Length==0||r.Name==marketRetainerFilter)&&(marketWorldFilter.Length==0||r.World==marketWorldFilter);
    private bool MatchesMarketItem(uint id,string name)=>marketFilter.Length==0||id.ToString()==marketFilter||name.Contains(marketFilter,StringComparison.OrdinalIgnoreCase);
    private void DrawEquinoxMarket()
    {
        if(!MarketEnabledUi())return;
        if(marketDataError.Length>0)ImGui.TextWrapped(marketDataError);
        ImGui.SetNextItemWidth(Math.Min(240,ImGui.GetContentRegionAvail().X));ImGui.InputTextWithHint("##marketfilter","Search items / item ID",ref marketFilter,100);
        if(ImGui.CollapsingHeader("Filters")){
            var all=marketLedger.Retainers;
            MarketFilterCombo("##person","All persons",all.Select(x=>x.Person),ref marketPerson);ImGui.SameLine();
            MarketFilterCombo("##account","All accounts",all.Where(x=>marketPerson.Length==0||x.Person==marketPerson).Select(x=>x.Account),ref marketAccount);
            MarketFilterCombo("##char","All characters",all.Select(x=>x.Character),ref marketCharacter);ImGui.SameLine();
            MarketFilterCombo("##retainer","All retainers",all.Select(x=>x.Name),ref marketRetainerFilter);
            MarketFilterCombo("##world","All worlds",all.Select(x=>x.World),ref marketWorldFilter);
            if(ImGui.SmallButton("Clear filters")){marketPerson=marketAccount=marketCharacter=marketRetainerFilter=marketWorldFilter=marketFilter="";marketFocusRetainer=0;marketAttentionOnly=false;marketUpdatesOnly=false;}
        }
        if(ImGui.BeginTabBar("EquinoxMarketPages",ImGuiTabBarFlags.FittingPolicyScroll)){
            void Tab(string label,Action body){var select=label=="Settings"&&openMarketSettings||label=="Selling"&&openMarketSelling;if(ImGui.BeginTabItem(label,select?ImGuiTabItemFlags.SetSelected:ImGuiTabItemFlags.None)){if(select){openMarketSettings=false;openMarketSelling=false;}if(ImGui.BeginChild("marketbody"+label,new Vector2(0,0),false))body();ImGui.EndChild();ImGui.EndTabItem();}}
            Tab("Selling",DrawMarketSelling);Tab("Retainers",DrawMarketRetainers);Tab("Sales",DrawMarketIncome);Tab("Summary",DrawMarketSummary);Tab("Spending",DrawMarketSpending);Tab("Price changes",DrawMarketChanges);Tab("Settings",DrawSellerSettings);Tab("Files",DrawMarketFiles);ImGui.EndTabBar();
        }
        windowFooter=()=>{if(ImGui.SmallButton("Seller"))OpenEquinoxSeller();};windowStatus=marketDirty||marketWriter.Busy?"Saving market records…":"Saved on this computer";
    }
    private void DrawMarketSelling()
    {
        ImGui.Checkbox("Needs attention only",ref marketAttentionOnly);
        ImGui.SameLine();ImGui.Checkbox("Price updates only",ref marketUpdatesOnly);
        if(marketFocusRetainer!=0){ImGui.TextDisabled("Retainer: "+(MarketRetainerById(marketFocusRetainer)?.Name??"Selected"));ImGui.SameLine();if(ImGui.SmallButton("All retainers"))marketFocusRetainer=0;}
        if(config.Seller.BackgroundChecks){ImGui.SameLine();ImGui.TextDisabled(marketWatchTask!=null?"Checking prices…":"Background prices");if(ImGui.IsItemHovered())ImGui.SetTooltip(marketWatchNotice+"\nUniversalis is cached; live checks take priority.");}
        var retainers=OrderedMarketRetainers();
        var shown=retainers.SelectMany(r=>r.Stock.Where(x=>MatchesMarketItem(x.ItemId,x.Name))).ToArray();
        ImGui.TextDisabled($"{shown.Length} listings · asking value {shown.Sum(x=>x.Total):N0} gil");
        foreach(var r in retainers){
            var rows=r.Stock.Where(x=>MatchesMarketItem(x.ItemId,x.Name)&&(!marketAttentionOnly||ReviewFor(r.Id,x.Slot).Status!=SellerStatus.Competitive)&&(!marketUpdatesOnly||ReviewFor(r.Id,x.Slot).CanApply)).ToArray();if(rows.Length==0)continue;
            var needed=rows.Count(x=>ReviewFor(r.Id,x.Slot).CanApply);var color=needed>0?SellerColor(SellerStatus.Update):MarketVisitDue(r)?SellerColor(SellerStatus.History):rows.All(x=>ReviewFor(r.Id,x.Slot).Status==SellerStatus.Competitive)?SellerColor(SellerStatus.Competitive):SellerColor(SellerStatus.Unknown);
            ImGui.PushStyleColor(ImGuiCol.Text,color);var open=ImGui.CollapsingHeader($"{r.Name} · {r.Character} · {rows.Length} listings"+(needed>0?$" · {needed} updates":"")+(MarketVisitDue(r)?" · Visit due":"")+"###retainer"+r.Id);ImGui.PopStyleColor();
            if(ImGui.IsItemHovered())ImGui.SetTooltip($"{r.Person} / {r.Account} · {r.World}\nLast observed {DisplayTime.Local(r.At)}\nListed value is not earned gil.");
            if(!open)continue;
            DrawMarketCharacterAction(r);
            foreach(var x in rows){var decision=ReviewFor(r.Id,x.Slot);ImGui.PushID($"{r.Id}/{x.Slot}");
                ImGui.PushStyleColor(ImGuiCol.Text,SellerColor(decision.Status));if(ImGui.Selectable(x.Name+(x.Hq?" HQ":"")+"##item",false,ImGuiSelectableFlags.None))ImGui.SetClipboardText(x.Name);ImGui.PopStyleColor();ItemRightClick(x.ItemId);
                if(ImGui.IsItemHovered())ImGui.SetTooltip($"{decision.Status} · {decision.Reason}\nListed {DisplayTime.Local(x.ListedAt)}\nClick to copy · right-click for item actions");
                ImGui.TextDisabled($"{x.Quantity} × {x.Price:N0} = {x.Total:N0} gil"+(decision.CanApply?$" → {decision.Price:N0} each":""));
                if(ImGui.BeginPopupContextItem("rule")){if(ImGui.MenuItem("Exclude from automatic pricing")){config.Seller.ExcludedItems.Add(x.ItemId);SaveConfiguration();}if(ImGui.MenuItem("Use current price as minimum")){config.Seller.MinimumPrices[x.ItemId]=x.Price;SaveConfiguration();}ImGui.EndPopup();}ImGui.PopID();
            }
        }
        if(retainers.Length==0)ImGui.TextWrapped("Open each retainer’s selling list to record it. Check prices visits selected retainers automatically.");
    }
    private void DrawMarketRetainers()
    {
        foreach(var r in OrderedMarketRetainers()){
            var states=r.Stock.Where(x=>MatchesMarketItem(x.ItemId,x.Name)).Select(x=>ReviewFor(r.Id,x.Slot)).ToArray();
            var updates=states.Count(x=>x.CanApply);var uncertain=states.Count(x=>x.Status is SellerStatus.Unknown or SellerStatus.Skipped or SellerStatus.Checking);
            var status=updates>0?SellerStatus.Update:MarketVisitDue(r)?SellerStatus.History:states.Length>0&&uncertain==0?SellerStatus.Competitive:SellerStatus.Unknown;
            ImGui.PushStyleColor(ImGuiCol.Text,SellerColor(status));
            if(ImGui.Selectable($"{r.Name} · {updates} updates"+(MarketVisitDue(r)?" · Visit due":"")+$"###market-retainer-{r.Id}"))OpenMarketSelling(r.Id,updates>0);
            ImGui.PopStyleColor();
            if(ImGui.IsItemHovered())ImGui.SetTooltip($"{r.Person} / {r.Account}\n{r.Stock.Length} known listings · {uncertain} need a fresh check\nLast observed {DisplayTime.Local(r.At)}\nClick to open this retainer's listings.");
            ImGui.TextDisabled($"{r.Character} · {r.World} · {states.Length} listings");
            DrawMarketCharacterAction(r);
        }
        if(marketLedger.Retainers.Length==0)ImGui.TextWrapped("Open retainer selling lists to populate this overview.");
    }
    private bool MarketVisitDue(MarketRetainer r)=>config.Seller.VisitReminders&&MarketAccounting.VisitDue(r,DateTimeOffset.UtcNow,config.Seller.VisitReminderDays);
    private MarketRetainer[] OrderedMarketRetainers()=>marketLedger.Retainers.Where(MatchesMarket).OrderByDescending(MarketVisitDue).ThenByDescending(r=>r.Stock.Count(s=>ReviewFor(r.Id,s.Slot).CanApply)).ThenBy(r=>r.World).ThenBy(r=>r.Character).ThenBy(r=>r.Name).ToArray();
    private void DrawMarketCharacterAction(MarketRetainer r)
    {
        ImGui.PushID("market-travel-"+r.Id);
        var visited=r.LastVisit??r.At;
        ImGui.TextDisabled("Last visit: "+DisplayTime.Local(visited)+(MarketVisitDue(r)?" · Visit due":""));
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Last observed interaction with this retainer. Visit reminders are a precaution, not a confirmed expiry. Summon and dismiss the retainer at a bell; background market checks do not count as visits.");
        if(Player.IsLoaded&&Player.ContentId==r.CharacterId){
            if(ImGui.SmallButton("Open Seller")){sellerScope=1;config.Seller.SelectedRetainers=[r.Id];SaveConfiguration();OpenEquinoxSeller();}
            ImGui.SameLine();ImGui.TextDisabled(Player.CurrentWorld.RowId==r.WorldId?"Use a summoning bell":"Return to "+r.World+" for this retainer");
        }else{
            var character=config.SharedRoster?.People.SelectMany(p=>p.Characters).FirstOrDefault(c=>c.ContentId==r.CharacterId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(character!=null)DrawCharacterTravel(character,false);else ImGui.TextDisabled("Pair this character to enable switching.");
        }
        ImGui.PopID();
    }
    private MarketIncome[] FilteredMarketIncome()=>marketIncomeView.Get(marketLedger.Income,MarketViewStamp,Environment.TickCount64,()=>{
        var cutoff=marketDate switch {0=>DateTimeOffset.Now.Date,1=>DateTimeOffset.UtcNow.AddDays(-7),2=>DateTimeOffset.UtcNow.AddDays(-30),_=>DateTimeOffset.MinValue};
        return marketLedger.Income.Where(x=>(marketSource switch {1=>x.Net.HasValue,2=>x.Id.StartsWith("allagan:"),3=>x.Id.StartsWith("inferred:"),_=>true})&&x.At>=cutoff&&MatchesMarketItem(x.ItemId,x.Item.Length>0?x.Item:SearchItemName(x.ItemId))&&
            (MarketRetainerById(x.RetainerId) is {} r?MatchesMarket(r):marketPerson.Length==0&&marketAccount.Length==0&&marketRetainerFilter.Length==0&&(marketCharacter.Length==0||x.Character==marketCharacter)&&(marketWorldFilter.Length==0||x.World==marketWorldFilter))).OrderByDescending(x=>x.At).ToArray();
    });
    private void MarketDateFilter(bool income=true){ImGui.SetNextItemWidth(130);if(ImGui.Combo("Period",ref marketDate,"Today\0Last 7 days\0Last 30 days\0All recorded\0"))marketPage=0;if(income){ImGui.SetNextItemWidth(150);if(ImGui.Combo("Source",ref marketSource,"All sources\0Confirmed receipts\0Imported history\0Inferred sales\0"))marketPage=0;}}
    private void DrawMarketIncome()
    {
        MarketDateFilter();var rows=FilteredMarketIncome();
        var totals=marketIncomeTotals.Get(rows,"",Environment.TickCount64,()=>(rows.Sum(x=>x.Net??0),rows.Any(x=>x.Net==null)));
        ImGui.TextDisabled($"{rows.Length} records · confirmed net {totals.Net:N0} gil");
        if(totals.Unverified)SettingsHelp("Imported or inferred records have no verified net amount. They are excluded from the confirmed net total. Hover a record to see its evidence.");
        MarketPaging(rows.Length);
        foreach(var x in rows.Skip(marketPage*80).Take(80)){
            var name=x.Item.Length>0?x.Item:SearchItemName(x.ItemId);if(ImGui.Selectable(name+(x.Hq?" HQ":"")+"##sale"+x.Id))ImGui.SetClipboardText(name);ItemRightClick(x.ItemId);
            var r=MarketRetainerById(x.RetainerId);
            ImGui.TextDisabled($"{r?.Character??x.Character} · {r?.Name??x.Retainer} · {(x.Net is {} net?$"{net:N0} net":$"{x.Gross:N0} gross")} gil");
            if(ImGui.IsItemHovered())ImGui.SetTooltip($"{DisplayTime.Local(x.At)} · {r?.World??x.World}\nQuantity: {(x.Quantity==0?"not observed":x.Quantity.ToString())}\n{x.Evidence}");
        }
    }
    private void MarketPaging(int count){marketPage=Math.Clamp(marketPage,0,Math.Max(0,(count-1)/80));if(count<=80)return;if(ImGui.SmallButton("Previous")&&marketPage>0)marketPage--;ImGui.SameLine();ImGui.TextDisabled($"{marketPage+1}/{(count+79)/80}");ImGui.SameLine();if(ImGui.SmallButton("Next")&&(marketPage+1)*80<count)marketPage++;}
    private void DrawMarketSummary()
    {
        MarketDateFilter();var rows=FilteredMarketIncome();
        var totals=marketIncomeTotals.Get(rows,"",Environment.TickCount64,()=>(rows.Sum(x=>x.Net??0),rows.Any(x=>x.Net==null)));
        ImGui.TextUnformatted($"Confirmed net: {totals.Net:N0} gil");
        SettingsHelp("Game sale receipts confirm net gil after fees. Recorded gross includes imported and inferred sales; hover a sale for evidence. This is revenue, not profit.");
        ImGui.SetNextItemWidth(160);ImGui.Combo("Group",ref marketOrder,"Item\0Retainer\0Character\0World\0");
        var groups=marketSummaryView.Get(rows,marketOrder.ToString(),Environment.TickCount64,()=>rows.GroupBy(x=>marketOrder switch{1=>MarketRetainerById(x.RetainerId)?.Name??x.Retainer,2=>MarketRetainerById(x.RetainerId)?.Character??x.Character,3=>MarketRetainerById(x.RetainerId)?.World??x.World,_=>(x.Item.Length>0?x.Item:SearchItemName(x.ItemId))+(x.Hq?" HQ":"")}).Select(g=>new MarketSummaryRow(g.Key,g.Sum(x=>x.Net??0),g.Sum(x=>x.Gross??0),g.Sum(x=>(long)x.Quantity),g.Count())).OrderByDescending(x=>x.Net).ThenByDescending(x=>x.Gross).ToArray());
        MarketPaging(groups.Length);
        if(ImGui.BeginTable("marketSummary",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp)){
            ImGui.TableSetupColumn("Name");ImGui.TableSetupColumn("Units");ImGui.TableSetupColumn("Recorded gross");ImGui.TableSetupColumn("Confirmed net");ImGui.TableHeadersRow();
            foreach(var g in groups.Skip(marketPage*80).Take(80)){ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextWrapped(g.Name);ImGui.TableNextColumn();ImGui.TextUnformatted(g.Units.ToString("N0"));ImGui.TableNextColumn();ImGui.TextUnformatted(g.Gross.ToString("N0"));ImGui.TableNextColumn();ImGui.TextUnformatted(g.Net.ToString("N0"));}ImGui.EndTable();
        }
    }
    private void DrawMarketSpending()
    {
        MarketDateFilter(false);var cutoff=marketDate switch{0=>DateTimeOffset.Now.Date,1=>DateTimeOffset.UtcNow.AddDays(-7),2=>DateTimeOffset.UtcNow.AddDays(-30),_=>DateTimeOffset.MinValue};
        var rows=marketExpenseView.Get(marketLedger.Expenses,MarketViewStamp,Environment.TickCount64,()=>(marketLedger.Expenses??[]).Where(x=>x.At>=cutoff&&MatchesMarketItem(x.ItemId,SearchItemName(x.ItemId))&&(marketCharacter.Length==0||x.Character==marketCharacter)&&(marketWorldFilter.Length==0||x.World==marketWorldFilter)&&(marketPerson.Length==0&&marketAccount.Length==0||marketLedger.Retainers.Any(r=>r.CharacterId==x.CharacterId&&(marketPerson.Length==0||r.Person==marketPerson)&&(marketAccount.Length==0||r.Account==marketAccount)))).OrderByDescending(x=>x.At).ToArray());
        var spent=marketExpenseTotal.Get(rows,"",Environment.TickCount64,()=>rows.Sum(x=>x.Price*x.Quantity+x.Tax));
        ImGui.TextUnformatted($"Market purchases: {spent:N0} gil including recorded tax");
        SettingsHelp("Confirmed purchases recorded while this tool is enabled. Not assigned automatically to crafts or resale stock; historical, vendor and gathered material costs may be missing.");
        if(marketRetainerFilter.Length>0)ImGui.TextDisabled("Retainer filter does not apply to character purchases.");
        MarketPaging(rows.Length);foreach(var x in rows.Skip(marketPage*80).Take(80)){ImGui.TextUnformatted(SearchItemName(x.ItemId)+(x.Hq?" HQ":""));ItemRightClick(x.ItemId);ImGui.TextDisabled($"{x.Character} · {x.World} · {x.Quantity} × {x.Price:N0} + {x.Tax:N0} tax");if(ImGui.IsItemHovered())ImGui.SetTooltip(DisplayTime.Local(x.At));}
    }
    private void DrawMarketChanges()
    {
        foreach(var x in marketLedger.Prices.Reverse().Where(x=>MatchesMarketItem(x.ItemId,x.Item)&&(MarketRetainerById(x.RetainerId) is {} r&&MatchesMarket(r))).Take(200)){
            ImGui.TextUnformatted($"{x.Retainer} · {x.Item} · {x.Result}");ItemRightClick(x.ItemId);
            ImGui.TextDisabled($"{x.Before:N0} → {(x.After is {} p?p.ToString("N0"):"kept")} gil");if(ImGui.IsItemHovered())ImGui.SetTooltip($"{DisplayTime.Local(x.At)}\n{x.Reason}");
        }
        ImGui.TextDisabled("Latest 200 shown; export includes the retained 2,000 price checks.");
    }
    private void DrawMarketFiles()
    {
        ImGui.BeginDisabled(marketExport!=null);
        if(ImGui.Button("Export filtered sales CSV")){var rows=FilteredMarketIncome();var folder=Path.Combine(Pi.GetPluginConfigDirectory(),"exports");marketExport=Task.Run(()=>{Directory.CreateDirectory(folder);var path=Path.Combine(folder,"Equinox-Market-sales-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv");File.WriteAllText(path,MarketAccounting.Export(rows));return path;});}
        if(ImGui.Button("Export full market backup")){var ledger=marketLedger;var folder=Path.Combine(Pi.GetPluginConfigDirectory(),"exports");marketExport=Task.Run(()=>{Directory.CreateDirectory(folder);var path=Path.Combine(folder,"Equinox-Market-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(ledger));return path;});}
        ImGui.EndDisabled();if(ImGui.Button("Open export folder"))OpenDiagnosticFolder(Path.Combine(Pi.GetPluginConfigDirectory(),"exports"));
        if(ImGui.CollapsingHeader("Import Allagan Market sales")){
            ImGui.TextWrapped("Export Sale History as CSV in Allagan Market, then choose its full file path here. Timestamps use this computer’s local timezone. Existing records are kept.");
            ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##allagancsv","CSV file path",ref marketImportPath,1024);
            ImGui.BeginDisabled(marketImport!=null);if(ImGui.Button("Import sales history"))ImportAllaganSales();ImGui.EndDisabled();
        }
        if(marketFileStatus.Length>0)ImGui.TextWrapped(marketFileStatus);
        if(marketSaleReceipts.Count<2)ImGui.TextWrapped("Some game sale message templates could not be verified. Those receipts will not be guessed; sale imports remain available.");
    }
}
