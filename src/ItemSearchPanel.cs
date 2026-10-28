using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using System.Numerics;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    [Dalamud.IoC.PluginService] internal static Dalamud.Plugin.Services.IGameGui GameGui { get; private set; } = null!;
    private ItemSearchWindow itemSearchWindow=null!;
    private string itemSearchQuery="",itemSearchBuiltQuery="",itemAccount="",itemCharacter="";
    private uint? itemExactId;
    private string itemActivePerson="",itemPreviousPerson="",itemRestorePerson="",itemWorld="";
    private SharedRoster? itemSearchRoster;
    private int itemSearchDiscoveryCount=-1,itemSearchLocationCount,itemLocationFilter,itemQualityFilter,itemFreshnessFilter;
    private bool itemSearchCommandRegistered,itemSelectCurrentPerson,itemCurrentOnly;
    private (SharedItemLocation Location,StorageStack Stack)[] itemSearchMatches=[];
    private static readonly string[] ItemLocationNames=["All locations","Inventory","Equipped","Armoury","Saddlebags","Retainers","FC chest","Armoire","Dresser","Housing placed","Housing storeroom"];
    private sealed class ItemSearchWindow:Window
    {
        private readonly Plugin plugin;
        public ItemSearchWindow(Plugin p):base("Do I already have it?###EquinoxItemSearch")
        {
            plugin=p;Size=new(730,520);SizeCondition=ImGuiCond.FirstUseEver;
            SizeConstraints=new(){MinimumSize=new(480,320),MaximumSize=new(float.MaxValue)};
            AllowPinning=true;AllowClickthrough=true;AllowBackgroundBlur=true;RespectCloseHotkey=false;
        }
        public override void Draw()=>plugin.DrawWindowWithLauncher(plugin.DrawItemSearch);
        public override void PostDraw()=>HandleNativeCollapse(this,()=>plugin.MinimizeLauncher("Do I already have it?"));
    }
    private readonly Dictionary<uint,string> searchItemNames=[];
    private string SearchItemName(uint id){if(!searchItemNames.TryGetValue(id,out var name))searchItemNames[id]=name=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(id)?.Name.ToString()??$"Item {id}";return name;}
    private void OpenItemSearch(string query)
    {
        itemExactId=uint.TryParse(query.Trim(),out var id)&&id>0?id:null;
        itemSearchQuery=itemExactId is {} selected?SearchItemName(selected):query.Trim();
        itemSearchDiscoveryCount=-1;itemSelectCurrentPerson=true;itemSearchWindow.IsOpen=true;
        minimizedLaunchers.Remove("Do I already have it?");
    }
    private unsafe uint ContextSearchItem(IMenuOpenedArgs args)
    {
        if(openingPluginItem!=0)return openingPluginItem;
        if(args.Target is MenuTargetInventory inventory)
            return ItemContextPolicy.Normalize(inventory.TargetItem?.ItemId ?? 0);
        // Character menus must never inherit an unrelated hovered item.
        if(args.Target is not MenuTargetDefault target || target.TargetObject is not null || target.TargetContentId != 0)
            return 0;
        ulong raw = 0;
        switch(args.AddonName)
        {
            case "RecipeNote":
                var recipe = AgentRecipeNote.Instance();
                if(recipe != null && recipe->IsAgentActive()) raw = recipe->ContextMenuResultItemId;
                break;
            case "RecipeTree": case "RecipeMaterialList": case "RecipeProductList":
                var context = AgentRecipeItemContext.Instance();
                if(context != null && context->IsAgentActive()) raw = context->ResultItemId;
                break;
            case "GatheringNote":
                var gathering = AgentGatheringNote.Instance();
                if(gathering != null && gathering->IsAgentActive()) raw = gathering->ContextMenuItemId;
                break;
            default:
                raw = GameGui.HoveredItem;
                break;
        }
        return ItemContextPolicy.Normalize(raw);
    }
    private void OnItemSearchMenu(IMenuOpenedArgs args)
    {
        var id = ContextSearchItem(args);
        if(id == 0 || DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id) is not {} row || row.Name.IsEmpty) return;
        args.AddMenuItem(new MenuItem{PrefixChar='E',Name=new SeStringBuilder().AddText("Where is it cheapest?").Build(),OnClicked=_=>OpenMarketItem(id)});
        args.AddMenuItem(new MenuItem{PrefixChar='E',Name=new SeStringBuilder().AddText("Do I already have it?").Build(),OnClicked=_=>OpenItemSearch(id.ToString())});
    }
    private static int ItemLocationKind(string key)
    {
        if(key.StartsWith("house:"))return int.TryParse(key.Split(':').Last(),out var housingType)&&housingType>=27000?10:9;
        if(key.StartsWith("fc:"))return 6;if(key.StartsWith("retainer:"))return 5;
        if(key=="armoire")return 7;if(key=="dresser")return 8;
        if(!int.TryParse(key.Split(':').Last(),out var id))return 0;
        return id<=3||id==2001?1:id==1000?2:id>=4000?4:3;
    }
    private void DrawItemSearch()
    {
        var activeFilters=(itemLocationFilter>0?1:0)+(itemQualityFilter>0?1:0)+(itemFreshnessFilter>0?1:0)+(itemWorld.Length>0?1:0);
        if(ImGui.CollapsingHeader("Filters"+(activeFilters>0?$" · {activeFilters} active":"")+"###item-filters"))
        {
            if(ImGui.BeginTable("item-filter-grid",2,ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextColumn();ImGui.TextDisabled("Location");ImGui.SetNextItemWidth(-1);ImGui.Combo("###location",ref itemLocationFilter,ItemLocationNames,ItemLocationNames.Length);
                ImGui.TableNextColumn();ImGui.TextDisabled("Quality");ImGui.SetNextItemWidth(-1);ImGui.Combo("###quality",ref itemQualityFilter,new[]{"Any quality","NQ","HQ","Not recorded"},4);
                ImGui.TableNextColumn();ImGui.TextDisabled("Last checked");ImGui.SetNextItemWidth(-1);ImGui.Combo("###checked",ref itemFreshnessFilter,new[]{"Any time","Last 24 hours","Last 7 days","Last 30 days"},4);
                ImGui.TableNextColumn();ImGui.TextDisabled("World");ImGui.SetNextItemWidth(-1);
                if(ImGui.BeginCombo("###item-world",itemWorld.Length>0?itemWorld:"All worlds")){if(ImGui.Selectable("All worlds",itemWorld==""))itemWorld="";foreach(var world in (config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Select(c=>c.World).Distinct().Order())if(ImGui.Selectable(world,itemWorld==world))itemWorld=world;ImGui.EndCombo();}
                ImGui.EndTable();
            }
            if(ImGui.Button("Clear filters")){if(itemCurrentOnly)itemRestorePerson=itemPreviousPerson;itemCurrentOnly=false;itemWorld=itemAccount=itemCharacter="";itemLocationFilter=itemQualityFilter=itemFreshnessFilter=0;}
        }
        ImGui.SetNextItemWidth(Math.Max(200,ImGui.GetContentRegionAvail().X-35));
        if(ImGui.InputTextWithHint("###item-search","Item name",ref itemSearchQuery,120)){itemExactId=null;}
        ImGui.SameLine();if(ImGui.Button("X##ClearOwnedSearch")){itemSearchQuery="";itemExactId=null;}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Clear search text and results");
        BuildItemSearch();
        if(ownedItemSearchStatus.Length>0)ImGui.TextDisabled(ownedItemSearchStatus);
        else if(ownedItemIndexTask!=null||ownedItemNamesTask!=null)ImGui.TextDisabled("Updating inventory index…");
        else if(ownedItemQueryTask!=null||itemSearchQuery.Trim()!=itemSearchBuiltQuery)ImGui.TextDisabled("Searching…");
        var loggedCharacter=config.SharedRoster?.People.SelectMany(p=>p.Characters).FirstOrDefault(c=>Player.IsLoaded&&c.ContentId==Player.ContentId.ToString());
        ImGui.BeginDisabled(loggedCharacter is null);if(ImGui.Checkbox("Current character only###current-character-search",ref itemCurrentOnly)){if(itemCurrentOnly)itemPreviousPerson=itemActivePerson;else itemRestorePerson=itemPreviousPerson;}ImGui.EndDisabled();
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(loggedCharacter is null?"Log into a paired character to use this filter.":"Logged in: "+loggedCharacter.Name+" @ "+loggedCharacter.World);
        ImGui.SameLine();ImGui.TextDisabled($"{itemSearchLocationCount} locations");
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Last observed possessions; unscanned locations may be missing.");
        if(config.SharedRoster?.ItemLocationsTruncated==true){ImGui.SameLine();ImGui.TextColored(new Vector4(1,.72f,.3f,1),"Partial shared results");if(ImGui.IsItemHovered())ImGui.SetTooltip("Shared results are partial because of size. The Journal searches its full saved records.");}
        if(loggedCharacter is null&&itemCurrentOnly)ImGui.TextDisabled("Log into a paired character to use this filter.");

        var people=config.SharedRoster?.People??[];
        if(people.Length==0){ImGui.TextWrapped("Refresh shared profiles to load your paired persons and characters.");return;}
        var scopePerson=people.FirstOrDefault(p=>((itemSelectCurrentPerson||itemCurrentOnly)&&p.Characters.Any(c=>c.Id==loggedCharacter?.Id))||itemRestorePerson==p.Id)
            ??people.FirstOrDefault(p=>p.Id==itemActivePerson)??people[0];
        var accounts=scopePerson.Characters.GroupBy(c=>c.AccountId).ToArray();

        var scopeChars=scopePerson.Characters.Where(c=>itemCurrentOnly?c.Id==loggedCharacter?.Id:itemAccount==""||string.Equals(c.Account,itemAccount,StringComparison.OrdinalIgnoreCase)).Where(c=>itemWorld==""||c.World==itemWorld).ToArray();
        if(ImGui.BeginTable("item-scope-grid",2,ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.BeginDisabled(itemCurrentOnly);
            if(ImGui.BeginCombo("###account",itemAccount.Length>0?itemAccount:"All accounts"))
            {if(ImGui.Selectable("All accounts",itemAccount==""))itemAccount="";foreach(var a in accounts)if(ImGui.Selectable(a.First().Account+"###"+a.Key,string.Equals(itemAccount,a.First().Account,StringComparison.OrdinalIgnoreCase)))itemAccount=a.First().Account;ImGui.EndCombo();}
            ImGui.EndDisabled();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.BeginDisabled(itemCurrentOnly);
            if(ImGui.BeginCombo("###character",people.SelectMany(p=>p.Characters).FirstOrDefault(c=>c.Id==itemCharacter)?.Name??"All characters"))
            {if(ImGui.Selectable("All characters",itemCharacter==""))itemCharacter="";foreach(var c in scopeChars)if(ImGui.Selectable(c.Name+" @ "+c.World+"###"+c.Id,itemCharacter==c.Id))itemCharacter=c.Id;ImGui.EndCombo();}
            ImGui.EndDisabled();ImGui.EndTable();
        }
        if(!ImGui.BeginTabBar("item-persons"))return;
        foreach(var person in people)
        {
            var current=person.Characters.FirstOrDefault(c=>Player.IsLoaded&&c.ContentId==Player.ContentId.ToString());
            var flags=((itemSelectCurrentPerson||itemCurrentOnly)&&current is not null||itemRestorePerson==person.Id)?ImGuiTabItemFlags.SetSelected:ImGuiTabItemFlags.None;
            if(!ImGui.BeginTabItem(person.Name.Replace("##","")+"###items-person-"+person.Id,flags))continue;
            if(itemSelectCurrentPerson&&current is not null)itemSelectCurrentPerson=false;
            itemActivePerson=person.Id;if(itemRestorePerson==person.Id)itemRestorePerson="";
            ImGui.PushID(person.Id);
            var chars=person.Characters.Where(c=>itemCurrentOnly?c.Id==loggedCharacter?.Id:(itemAccount==""||string.Equals(c.Account,itemAccount,StringComparison.OrdinalIgnoreCase))&&(itemCharacter==""||c.Id==itemCharacter)).Where(c=>itemWorld==""||c.World==itemWorld).ToArray();
            var scopeOwners=chars.Select(c=>c.Id).Concat(chars.Where(c=>c.FcMember!=false).Select(c=>"fc:"+c.FcId)).ToHashSet();
            var charIds=chars.Select(c=>c.Id).ToHashSet();
            var stamp=$"{person.Id}/{itemAccount}/{itemCharacter}/{itemWorld}/{itemCurrentOnly}/{loggedCharacter?.Id}/{itemLocationFilter}/{itemQualityFilter}/{itemFreshnessFilter}/{config.SharedRoster?.Revision}";
            var allRows=itemSearchFiltered.Get(itemSearchMatches,stamp,Environment.TickCount64,()=>itemSearchMatches.Where(x=>(scopeOwners.Contains(x.Location.OwnerId)||(x.Location.CharacterIds??[]).Any(charIds.Contains))&&(itemLocationFilter==0||ItemLocationKind(x.Location.Key)==itemLocationFilter)&&(itemQualityFilter==0||(x.Location.Stacks is null?3:x.Stack.Hq?2:1)==itemQualityFilter)&&(itemFreshnessFilter==0||DateTimeOffset.UtcNow-x.Location.At<=TimeSpan.FromDays(new[]{0,1,7,30}[itemFreshnessFilter]))).ToArray());
            var matchingOwners=itemSearchOwners.Get(allRows,"",0,()=>allRows.Select(x=>x.Location.OwnerId).ToHashSet());
            var count=allRows.Length;var pageCount=Math.Max(1,(count+59)/60);itemSearchPage=Math.Clamp(itemSearchPage,0,pageCount-1);
            var rows=allRows.Skip(itemSearchPage*60).Take(60).ToArray();
            var housingRows=rows.Where(x=>x.Location.Key.StartsWith("house:")&&(x.Location.CharacterIds??[]).Any(charIds.Contains)).ToArray();
            ImGui.TextDisabled($"{count} matching records · {chars.Count(c=>matchingOwners.Contains(c.Id))} characters");
            ImGui.SameLine();bool? expand=null;if(ImGui.SmallButton("Expand results"))expand=true;ImGui.SameLine();if(ImGui.SmallButton("Collapse results"))expand=false;
            if(pageCount>1){ImGui.BeginDisabled(itemSearchPage==0);if(ImGui.SmallButton("Previous##items"))itemSearchPage--;ImGui.EndDisabled();ImGui.SameLine();ImGui.TextDisabled($"Page {itemSearchPage+1}/{pageCount}");ImGui.SameLine();ImGui.BeginDisabled(itemSearchPage+1>=pageCount);if(ImGui.SmallButton("Next##items"))itemSearchPage++;ImGui.EndDisabled();}
            ImGui.BeginChild("item-results",Vector2.Zero);
            // Update stored child states before closing their parents: hidden children
            // are not drawn, so SetNextItemOpen inside an account cannot reach them.
            if(expand is {} openAll)
            {
                var state=ImGui.GetStateStorage();
                foreach(var c in person.Characters)
                    state.SetBool(ImGui.GetID("###char-"+c.Id),openAll);
                foreach(var account in person.Characters.GroupBy(c=>c.AccountId))
                    state.SetBool(ImGui.GetID("###account-"+account.Key),openAll);
            }
            foreach(var account in chars.GroupBy(c=>c.AccountId).OrderByDescending(a=>a.Any(c=>c.Id==current?.Id)))
            {
                var relevant=account.Where(c=>rows.Any(x=>x.Location.OwnerId==c.Id)).OrderByDescending(c=>c.Id==current?.Id).ToArray();if(relevant.Length==0)continue;
                ImGui.SetNextItemOpen(expand??true,expand.HasValue?ImGuiCond.Always:ImGuiCond.Once);
                if(!ImGui.CollapsingHeader(account.First().Account+"###account-"+account.Key))continue;
                foreach(var c in relevant){var owned=rows.Where(x=>x.Location.OwnerId==c.Id).ToArray();ImGui.SetNextItemOpen(expand??(c.Id==current?.Id||count<=8),expand.HasValue?ImGuiCond.Always:ImGuiCond.Once);if(ImGui.TreeNodeEx(c.Name+" @ "+c.World+(c.Id==current?.Id?" · Logged in":"")+"###char-"+c.Id,ImGuiTreeNodeFlags.SpanAvailWidth)){foreach(var row in owned)DrawItemLocation(row.Location,row.Stack);ImGui.TreePop();}}
            }
            var fcIds=chars.Where(c=>c.FcMember!=false).Select(c=>"fc:"+c.FcId).ToHashSet();
            var chests=rows.Where(x=>fcIds.Contains(x.Location.OwnerId)).ToArray();
            if(chests.Length>0){ImGui.SetNextItemOpen(expand??true,expand.HasValue?ImGuiCond.Always:ImGuiCond.Once);if(ImGui.CollapsingHeader("Shared FC chests"))foreach(var row in chests)DrawItemLocation(row.Location,row.Stack);}
            if(housingRows.Length>0){ImGui.SetNextItemOpen(expand??true,expand.HasValue?ImGuiCond.Always:ImGuiCond.Once);if(ImGui.CollapsingHeader("Housing · placed & stored"))foreach(var row in housingRows)DrawItemLocation(row.Location,row.Stack);}
            if(count==0)ImGui.TextWrapped(itemSearchQuery.Length==0?"Search for an item by name.":"No matching recorded items in this view. Try clearing filters; unscanned locations may be missing.");
            ImGui.EndChild();ImGui.PopID();ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }
    private void DrawItemLocation(SharedItemLocation location,StorageStack stack)
    {
        ImGui.Separator();ImGui.TextWrapped(stack.Name+(location.Stacks is null?" · quality unknown":stack.Hq?" · HQ":" · NQ"));
        ItemRightClick(stack.ItemId);
        ImGui.TextWrapped(location.Name);
        ImGui.TextWrapped(stack.Positions is {Length:>0}?string.Join(" · ",stack.Positions.Select(p=>$"Slot {p.Slot} × {p.Quantity}")):"Slot not recorded");
        ImGui.TextDisabled((location.Stacks is null?"Quantity not recorded":stack.Quantity+" recorded")+$" · Checked {DisplayTime.Local(location.At)}");
    }
    private static void DrawItemSearchIcon(Vector2 at,Vector2 size,float opacity=1)
    {
        var d=ImGui.GetWindowDrawList();var scale=size.X/64;
        var blue=ImGui.ColorConvertFloat4ToU32(new Vector4(.25f,.72f,1,opacity));var white=ImGui.ColorConvertFloat4ToU32(new Vector4(.93f,.97f,1,opacity));
        d.AddCircleFilled(at+size/2,size.X*.46f,ImGui.ColorConvertFloat4ToU32(new Vector4(.06f,.15f,.24f,opacity)),32);
        d.AddRect(at+new Vector2(12,17)*scale,at+new Vector2(39,43)*scale,blue,3*scale,ImDrawFlags.None,2*scale);
        d.AddLine(at+new Vector2(12,25)*scale,at+new Vector2(39,25)*scale,blue,2*scale);
        d.AddCircle(at+new Vector2(39,37)*scale,10*scale,white,24,3*scale);
        d.AddLine(at+new Vector2(46,45)*scale,at+new Vector2(55,54)*scale,white,4*scale);
    }
    private void DrawItemSearchShortcut(float size)
    {
        var at=ImGui.GetCursorScreenPos();var click=ImGui.InvisibleButton("Open item search##header",new Vector2(size));DrawItemSearchIcon(at,new(size));
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Do I already have it? · /doihaveit");
        if(click){itemSearchWindow.IsOpen=true;minimizedLaunchers.Remove("Do I already have it?");}
    }
}
