using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool gilItemsMode;
    private readonly Dictionary<TradeItemKey,int> itemTradeSelection=[];
    private TradeBagSlot[] itemTradeBags=[];
    private DateTimeOffset itemTradeBagRefresh;
    private ulong itemTradeBagOwner;
    private string itemTradeSearch="";
    private readonly HashSet<uint> itemTradeCategories=[];
    private readonly Dictionary<uint,string> itemTradeNames=[];
    private bool itemTradeSelectedOnly;
    private void RefreshItemTradePicker()
    {
        itemTradeBags=ReadItemTradeBags();itemTradeNames.Clear();
        foreach(var id in itemTradeBags.Where(x=>x.Item>0).Select(x=>x.Item).Distinct())itemTradeNames[id]=SearchItemName(id);
        itemTradeBagRefresh=DateTimeOffset.UtcNow.AddSeconds(2);
    }
    private void LoadOrganizerTradeItems()
    {
        RefreshItemTradePicker();itemTradeSelection.Clear();var missing=0;
        foreach(var entry in RetainerRetrieval.Combine(FilteredPlanItems())){
            var wanted=checked(entry.Quantity*retrievalMultiplier);
            foreach(var group in itemTradeBags.Where(x=>x.Item==entry.ItemId&&x.Tradable&&RetainerRetrieval.AllowsQuality(x.Hq,retrievalQuality)).GroupBy(x=>x.Key).OrderBy(g=>g.Key.Hq)){
                var count=Math.Min(wanted,group.Sum(x=>x.Quantity));if(count>0)itemTradeSelection[group.Key]=count;wanted-=count;
            }
            missing+=wanted;
        }
        gilTradeNotice=$"Loaded {itemTradeSelection.Count} item/quality selections"+(missing>0?$" · {missing:N0} requested items unavailable or untradable":"")+". Review quantities before sending.";
    }
    private void DrawItemTradePicker()
    {
        var owner=Player.IsLoaded?Player.ContentId:0;
        if(owner!=itemTradeBagOwner){itemTradeBagOwner=owner;itemTradeSelection.Clear();itemTradeBags=[];itemTradeBagRefresh=default;}
        try{if(!TraderRunning&&DateTimeOffset.UtcNow>=itemTradeBagRefresh)RefreshItemTradePicker();}catch(Exception e){gilTradeNotice=e.Message;itemTradeBagRefresh=DateTimeOffset.UtcNow.AddSeconds(2);}
        if(ImGui.BeginTable("TradeItemFilters",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##TradeItemSearch","Search bag items…",ref itemTradeSearch,80);
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);DrawStorageCategoryPicker("##TradeItemCategories",itemTradeCategories,"All categories");
            ImGui.EndTable();
        }
        if(ImGui.SmallButton("Organizer list")){try{LoadOrganizerTradeItems();}catch(Exception e){gilTradeNotice=e.Message;}}
        MarketHelp("Load the current Organizer's listed items and quantities from your bags. No crafting expansion or retainer withdrawal. HQ/NQ choice applies; unavailable or untradable copies are reported. Review before sending.");
        ImGui.SameLine();ImGui.Checkbox("Selected only",ref itemTradeSelectedOnly);
        var rows=itemTradeBags.Where(x=>x.Item>1&&x.Quantity>0&&x.Tradable).GroupBy(x=>x.Key).Select(g=>(Key:g.Key,Count:g.Sum(x=>x.Quantity)))
            .Where(x=>(!itemTradeSelectedOnly||itemTradeSelection.ContainsKey(x.Key))&&(itemTradeCategories.Count==0||itemTradeCategories.Contains(OrganizeCategory(x.Key.Item)))&&itemTradeNames.GetValueOrDefault(x.Key.Item,"").Contains(itemTradeSearch,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>itemTradeNames.GetValueOrDefault(x.Key.Item)).ThenBy(x=>x.Key.Hq).ToArray();
        if(ImGui.SmallButton("Select all shown"))foreach(var row in rows)itemTradeSelection.TryAdd(row.Key,row.Count);
        MarketHelp("Select every item matching the current search, categories and Selected only filter, including rows below the scroll area. Existing selected quantities are preserved.");
        ImGui.SameLine();if(ImGui.SmallButton("Clear selection"))itemTradeSelection.Clear();
        ImGui.TextDisabled($"{itemTradeSelection.Count} selected · {itemTradeSelection.Values.Sum():N0} items");
        MarketHelp("Only tradable copies in your bags and crystals. HQ and NQ are separate. Shift-hover an item to select it; Alt-hover to deselect. Each trade contains at most five stacks. Review quantities; nothing moves until Send items.");
        var height=Math.Max(90,Math.Min(260,ImGui.GetContentRegionAvail().Y-100));
        if(ImGui.BeginTable("TradeBagItems",3,ImGuiTableFlags.ScrollY|ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp,new(0,height))){
            ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("Have",ImGuiTableColumnFlags.WidthFixed,45);ImGui.TableSetupColumn("Send",ImGuiTableColumnFlags.WidthFixed,80);ImGui.TableSetupScrollFreeze(0,1);ImGui.TableHeadersRow();
            foreach(var row in rows){ImGui.PushID(row.Key.Item+":"+row.Key.Hq);ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.BeginGroup();var selected=itemTradeSelection.ContainsKey(row.Key);
                if(ImGui.Checkbox("##Selected",ref selected)){if(selected)itemTradeSelection[row.Key]=row.Count;else itemTradeSelection.Remove(row.Key);}ImGui.SameLine();
                ImGui.TextUnformatted(itemTradeNames.GetValueOrDefault(row.Key.Item,row.Key.Item.ToString())+(row.Key.Hq?" HQ":""));ImGui.EndGroup();
                if(!TraderRunning&&ImGui.IsItemHovered()&&!ImGui.GetIO().WantTextInput){if(ImGui.GetIO().KeyShift&&!ImGui.GetIO().KeyAlt)itemTradeSelection.TryAdd(row.Key,row.Count);else if(ImGui.GetIO().KeyAlt&&!ImGui.GetIO().KeyShift)itemTradeSelection.Remove(row.Key);}
                selected=itemTradeSelection.ContainsKey(row.Key);
                ImGui.TableNextColumn();ImGui.TextUnformatted(row.Count.ToString());ImGui.TableNextColumn();ImGui.BeginDisabled(!selected);
                var qty=itemTradeSelection.GetValueOrDefault(row.Key);ImGui.SetNextItemWidth(-1);if(ImGui.InputInt("##Quantity",ref qty,0,0))itemTradeSelection[row.Key]=Math.Clamp(qty,1,row.Count);ImGui.EndDisabled();ImGui.PopID();
            }ImGui.EndTable();
        }
    }
}

