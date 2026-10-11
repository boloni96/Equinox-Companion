using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private string itemSearchQuery="";
    private string itemSearchBuiltQuery="";
    private SharedRoster? itemSearchRoster;
    private int itemSearchDiscoveryCount=-1,itemSearchLocationCount;
    private readonly List<(SharedItemLocation Location,StorageStack Stack)> itemSearchMatches=[];
    private bool itemSearchSelectTab, itemSearchCommandRegistered;
    private void OpenItemSearch(string query){itemSearchQuery=query.Trim();itemSearchSelectTab=true;visible=true;}
    private void OnItemSearchMenu(IMenuOpenedArgs args)
    {
        if(args.Target is not MenuTargetInventory target || target.TargetItem is not {} item)return;
        var id=item.ItemId%1000000;if(id==0||item.ItemId>=2000000)return;
        args.AddMenuItem(new MenuItem{Name=new SeStringBuilder().AddText("Do I already have it?").Build(),OnClicked=_=>OpenItemSearch(id.ToString())});
    }
    private void DrawItemSearch()
    {
        ImGui.TextWrapped("Do I already have it?");
        ImGui.SetNextItemWidth(Math.Min(460,ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint("###item-search","Item name or ID",ref itemSearchQuery,120);
        ImGui.TextWrapped("Last recorded possessions across your paired Journal. Unscanned locations may be missing. Collectible unlocks are separate.");
        var query=itemSearchQuery.Trim();
        if(itemSearchBuiltQuery!=query||itemSearchRoster!=config.SharedRoster||itemSearchDiscoveryCount!=config.Discoveries.Count)
        {
        itemSearchBuiltQuery=query;itemSearchRoster=config.SharedRoster;itemSearchDiscoveryCount=config.Discoveries.Count;itemSearchMatches.Clear();
        var chars=config.SharedRoster?.People.SelectMany(p=>p.Characters).ToArray()??[];
        var locations=(config.SharedRoster?.ItemLocations??[]).ToList();
        // Latest local observations can be newer than the shared website copy.
        foreach(var e in config.Discoveries.Where(e=>e.Kind=="storage.observed"&&e.Storage is not null).OrderBy(e=>e.At))
        {
            var c=chars.FirstOrDefault(c=>c.ContentId==e.Actor.ContentId);if(c is null)continue;
            var s=e.Storage!;var fc=s.Key.StartsWith("fc:")?s.Key.Split(':')[1]:null;
            if(fc is not null&&(c.FcMember==false||c.FcId!=fc))continue;
            var owner=fc is null?c.Id:"fc:"+fc;var old=locations.FirstOrDefault(x=>x.OwnerId==owner&&x.Key==s.Key);
            if(old is not null&&old.At>=e.At)continue;
            if(old is not null)locations.Remove(old);
            locations.Add(new(owner,fc is null?c.Name+" @ "+c.World:"Free Company",s.Key,s.Name,e.At,s.Items,s.Stacks));
        }
        itemSearchLocationCount=locations.Count;
        foreach(var location in locations.OrderBy(x=>x.OwnerName).ThenBy(x=>x.Name))
        {
            var stacks=location.Stacks??location.Items.Select(id=>new StorageStack(id,0,false,DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id)?.Name.ToString()??$"Item {id}")).ToArray();
            foreach(var stack in stacks)
            {
                if(stack.ItemId.ToString()!=query&&!stack.Name.Contains(query,StringComparison.OrdinalIgnoreCase))continue;
                if(query.Length>0)itemSearchMatches.Add((location,stack));
            }
        }
        }
        ImGui.TextDisabled($"{itemSearchLocationCount} recorded locations · use /doihaveit <name or ID>");
        if(config.SharedRoster?.ItemLocationsTruncated==true)ImGui.TextWrapped("The shared search is partial because of its size. Search the Journal for the full saved records.");
        if(query.Length==0)return;
        ImGui.BeginChild("item-results",System.Numerics.Vector2.Zero);
        foreach(var (location,stack) in itemSearchMatches)
        {
            ImGui.Separator();ImGui.TextWrapped(stack.Name+(location.Stacks is null?" · quality not recorded":stack.Hq?" · HQ":" · NQ"));
            ImGui.TextWrapped(location.OwnerName+" · "+location.Name);
            ImGui.TextDisabled((location.Stacks is null?"Quantity not recorded":stack.Quantity+" recorded")+$" · Item {stack.ItemId} · Checked {location.At.LocalDateTime:g}");
        }
        if(itemSearchMatches.Count==0)ImGui.TextWrapped("No match in recorded locations. This does not confirm you do not own it. Open inventories, retainers or the FC chest with collection sync enabled to refresh.");
        ImGui.EndChild();
    }
}
