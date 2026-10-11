using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using System.Numerics;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private ItemSearchWindow itemSearchWindow=null!;
    private string itemSearchQuery="",itemSearchBuiltQuery="",itemAccount="",itemCharacter="";
    private uint? itemExactId;
    private string itemActivePerson="",itemPreviousPerson="",itemRestorePerson="",itemWorld="";
    private SharedRoster? itemSearchRoster;
    private int itemSearchDiscoveryCount=-1,itemSearchLocationCount,itemLocationFilter,itemQualityFilter,itemFreshnessFilter;
    private bool itemSearchCommandRegistered,itemSelectCurrentPerson,itemCurrentOnly;
    private readonly List<(SharedItemLocation Location,StorageStack Stack)> itemSearchMatches=[];
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
        public override void Draw()=>plugin.DrawItemSearch();
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
    private void OnItemSearchMenu(IMenuOpenedArgs args)
    {
        if(args.Target is not MenuTargetInventory target||target.TargetItem is not {} item)return;
        var id=item.ItemId%1000000;if(id==0||item.ItemId>=2000000)return;
        args.AddMenuItem(new MenuItem{Name=new SeStringBuilder().AddText("Do I already have it?").Build(),OnClicked=_=>OpenItemSearch(id.ToString())});
    }
    private static int ItemLocationKind(string key)
    {
        if(key.StartsWith("house:"))return int.TryParse(key.Split(':').Last(),out var housingType)&&housingType>=27000?10:9;
        if(key.StartsWith("fc:"))return 6;if(key.StartsWith("retainer:"))return 5;
        if(key=="armoire")return 7;if(key=="dresser")return 8;
        if(!int.TryParse(key.Split(':').Last(),out var id))return 0;
        return id<=3||id==2001?1:id==1000?2:id>=4000?4:3;
    }
    private void BuildItemSearch()
    {
        var query=itemSearchQuery.Trim();
        if(itemSearchBuiltQuery==query&&itemSearchRoster==config.SharedRoster&&itemSearchDiscoveryCount==config.Discoveries.Count)return;
        itemSearchBuiltQuery=query;itemSearchRoster=config.SharedRoster;itemSearchDiscoveryCount=config.Discoveries.Count;itemSearchMatches.Clear();
        var chars=config.SharedRoster?.People.SelectMany(p=>p.Characters).ToArray()??[];
        var locations=(config.SharedRoster?.ItemLocations??[]).ToDictionary(x=>(x.OwnerId,x.Key));
        foreach(var e in config.Discoveries.Where(e=>e.Kind=="storage.observed"&&e.Storage is not null))
        {
            var c=chars.FirstOrDefault(c=>c.ContentId==e.Actor.ContentId);if(c is null)continue;
            var s=e.Storage!;var fc=s.Key.StartsWith("fc:")?s.Key.Split(':')[1]:null;
            if(fc is not null&&(c.FcMember==false||c.FcId!=fc))continue;
            var housing=s.Key.StartsWith("house:");var owner=housing?"house:"+s.Key.Split(':')[1]:fc is null?c.Id:"fc:"+fc;var key=(owner,s.Key);
            if(locations.TryGetValue(key,out var old)&&old.At>=e.At)continue;
            locations[key]=new(owner,housing?"Housing · shared observation":fc is null?c.Name+" @ "+c.World:"Free Company",s.Key,s.Name,e.At,s.Items,s.Stacks,housing?(old?.CharacterIds??[]).Append(c.Id).Concat(chars.Where(x=>x.Houses.Any(h=>h.GameHouseId==s.Key.Split(':')[1])).Select(x=>x.Id)).Distinct().ToArray():null);
        }
        itemSearchLocationCount=locations.Count;if(query.Length==0)return;
        foreach(var location in locations.Values)
        foreach(var stack in location.Stacks??location.Items.Select(id=>new StorageStack(id,0,false,SearchItemName(id))))
        {
            var name=SearchItemName(stack.ItemId);
            if(itemExactId is {} exact?stack.ItemId!=exact:stack.ItemId.ToString()!=query&&!name.Contains(query,StringComparison.OrdinalIgnoreCase)&&!stack.Name.Contains(query,StringComparison.OrdinalIgnoreCase))continue;
            itemSearchMatches.Add((location,stack with{Name=name}));
        }
    }
    private void DrawItemSearch()
    {
        ImGui.SetNextItemWidth(Math.Max(200,ImGui.GetContentRegionAvail().X));
        if(ImGui.InputTextWithHint("###item-search","Item name",ref itemSearchQuery,120)){itemExactId=null;itemSearchDiscoveryCount=-1;}
        BuildItemSearch();
        ImGui.TextDisabled($"{itemSearchLocationCount} recorded locations · last observed possessions");
        if(config.SharedRoster?.ItemLocationsTruncated==true)ImGui.TextWrapped("Shared results are partial because of size. The Journal searches its full saved records.");
        var loggedCharacter=config.SharedRoster?.People.SelectMany(p=>p.Characters).FirstOrDefault(c=>Player.IsLoaded&&c.ContentId==Player.ContentId.ToString());
        ImGui.BeginDisabled(loggedCharacter is null);if(ImGui.Checkbox("Current character only"+(loggedCharacter is null?"":" · "+loggedCharacter.Name)+"###current-character-search",ref itemCurrentOnly)){if(itemCurrentOnly)itemPreviousPerson=itemActivePerson;else itemRestorePerson=itemPreviousPerson;}ImGui.EndDisabled();
        if(loggedCharacter is null&&itemCurrentOnly)ImGui.TextDisabled("Log into a paired character to use this filter.");
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
        var people=config.SharedRoster?.People??[];
        if(people.Length==0){ImGui.TextWrapped("Refresh shared profiles to load your paired persons and characters.");return;}
        var logged=people.SelectMany(p=>p.Characters).FirstOrDefault(c=>Player.IsLoaded&&c.ContentId==Player.ContentId.ToString());
        if(logged is not null)ImGui.TextColored(new Vector4(.5f,.85f,1,1),"Logged in: "+logged.Name+" @ "+logged.World);
        if(!ImGui.BeginTabBar("item-persons"))return;
        foreach(var person in people)
        {
            var current=person.Characters.FirstOrDefault(c=>Player.IsLoaded&&c.ContentId==Player.ContentId.ToString());
            var flags=((itemSelectCurrentPerson||itemCurrentOnly)&&current is not null||itemRestorePerson==person.Id)?ImGuiTabItemFlags.SetSelected:ImGuiTabItemFlags.None;
            if(!ImGui.BeginTabItem(person.Name.Replace("##","")+"###items-person-"+person.Id,flags))continue;
            if(itemSelectCurrentPerson&&current is not null)itemSelectCurrentPerson=false;
            itemActivePerson=person.Id;if(itemRestorePerson==person.Id)itemRestorePerson="";
            ImGui.PushID(person.Id);
            var accounts=person.Characters.GroupBy(c=>c.AccountId).ToArray();

            var chars=person.Characters.Where(c=>itemCurrentOnly?c.Id==loggedCharacter?.Id:itemAccount==""||string.Equals(c.Account,itemAccount,StringComparison.OrdinalIgnoreCase)).Where(c=>itemWorld==""||c.World==itemWorld).ToArray();
            if(ImGui.BeginTable("item-scope-grid",2,ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextColumn();ImGui.TextDisabled("Account");ImGui.SetNextItemWidth(-1);ImGui.BeginDisabled(itemCurrentOnly);
                if(ImGui.BeginCombo("###account",itemAccount.Length>0?itemAccount:"All accounts"))
                {if(ImGui.Selectable("All accounts",itemAccount==""))itemAccount="";foreach(var a in accounts)if(ImGui.Selectable(a.First().Account+"###"+a.Key,string.Equals(itemAccount,a.First().Account,StringComparison.OrdinalIgnoreCase)))itemAccount=a.First().Account;ImGui.EndCombo();}
                ImGui.EndDisabled();ImGui.TableNextColumn();ImGui.TextDisabled("Character");ImGui.SetNextItemWidth(-1);ImGui.BeginDisabled(itemCurrentOnly);
                if(ImGui.BeginCombo("###character",people.SelectMany(p=>p.Characters).FirstOrDefault(c=>c.Id==itemCharacter)?.Name??"All characters"))
                {if(ImGui.Selectable("All characters",itemCharacter==""))itemCharacter="";foreach(var c in chars)if(ImGui.Selectable(c.Name+" @ "+c.World+"###"+c.Id,itemCharacter==c.Id))itemCharacter=c.Id;ImGui.EndCombo();}
                ImGui.EndDisabled();ImGui.EndTable();
            }
            chars=person.Characters.Where(c=>itemCurrentOnly?c.Id==loggedCharacter?.Id:(itemAccount==""||string.Equals(c.Account,itemAccount,StringComparison.OrdinalIgnoreCase))&&(itemCharacter==""||c.Id==itemCharacter)).Where(c=>itemWorld==""||c.World==itemWorld).ToArray();
            var rows=itemSearchMatches.Where(x=>(itemLocationFilter==0||ItemLocationKind(x.Location.Key)==itemLocationFilter)&&(itemQualityFilter==0||(x.Location.Stacks is null?3:x.Stack.Hq?2:1)==itemQualityFilter)&&(itemFreshnessFilter==0||DateTimeOffset.UtcNow-x.Location.At<=TimeSpan.FromDays(new[]{0,1,7,30}[itemFreshnessFilter]))).ToArray();
            var scopeOwners=chars.Select(c=>c.Id).Concat(chars.Where(c=>c.FcMember!=false).Select(c=>"fc:"+c.FcId)).ToHashSet();
            var housingRows=rows.Where(x=>x.Location.Key.StartsWith("house:")&&(x.Location.CharacterIds??[]).Intersect(chars.Select(c=>c.Id)).Any()).ToArray();
            var count=rows.Count(x=>scopeOwners.Contains(x.Location.OwnerId))+housingRows.Length;
            ImGui.TextDisabled($"{count} matching records · {chars.Count(c=>rows.Any(x=>x.Location.OwnerId==c.Id))} characters");
            bool? expand=null;if(ImGui.SmallButton("Expand results"))expand=true;ImGui.SameLine();if(ImGui.SmallButton("Collapse results"))expand=false;
            ImGui.BeginChild("item-results",Vector2.Zero);
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
    private static void DrawItemLocation(SharedItemLocation location,StorageStack stack)
    {
        ImGui.Separator();ImGui.TextWrapped(stack.Name+(location.Stacks is null?" · quality unknown":stack.Hq?" · HQ":" · NQ"));
        ImGui.TextWrapped(location.Name);
        ImGui.TextWrapped(stack.Positions is {Length:>0}?string.Join(" · ",stack.Positions.Select(p=>$"Slot {p.Slot} × {p.Quantity}")):"Slot not recorded");
        ImGui.TextDisabled((location.Stacks is null?"Quantity not recorded":stack.Quantity+" recorded")+$" · Checked {location.At.LocalDateTime:g}");
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
