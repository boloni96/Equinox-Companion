using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string assignmentItemSearch="";
    private void DrawRetainerAssignments()
    {
        if(!Player.IsLoaded){ImGui.TextDisabled("Log in to edit this character’s assignments.");return;}
        if(ImGui.Button("Preview all")){organizeFrom=0;organizeTo=StorageAutoRouting.AllRetainers;organizeRules.Clear();RetrievalTry(CheckOrganize);}ImGui.SameLine();
        if(ImGui.Button("Organise all"))StartStorageRouting([new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true)],false);
        MarketHelp("Regular: consolidate duplicates from the last retainer toward the first in game order; unique items stay put. With assignments enabled: use the selected category/item homes. No routine inventory tour when recorded stock is complete.");
        var character=Player.ContentId;var use=config.UseRetainerAssignments.Contains(character);ImGui.SameLine();
        if(ImGui.Checkbox("Use assignments",ref use)){if(use)config.UseRetainerAssignments.Add(character);else config.UseRetainerAssignments.Remove(character);SaveConfiguration();organizePreview=[];}
        MarketHelp("Assignments apply to All retainers in Organise and Entrust. Explicit destinations still override them. Unassigned items are left alone. Settings belong to this character.");
        if(!ImGui.CollapsingHeader("Retainer assignments"))return;
        if(!config.RetainerAssignments.TryGetValue(character,out var rows))config.RetainerAssignments[character]=rows=[];
        var owned=ReadSellerRetainers();if(owned.Length==0)owned=(retrievalKnownRetainers.GetValueOrDefault(character)??new Dictionary<ulong,string>()).Select(x=>new SellerRetainerRef(x.Key,x.Value)).ToArray();var names=owned.ToDictionary(x=>x.Id,x=>x.Name);
        foreach(var retainer in owned)if(rows.All(x=>x.Retainer!=retainer.Id))rows.Add(new(retainer.Id,[],[]));
        var dirty=false;
        foreach(var entry in rows.ToArray().Where(x=>names.ContainsKey(x.Retainer))){
            var index=rows.IndexOf(entry);ImGui.PushID(entry.Retainer.ToString());
            ImGui.BeginDisabled(index==0);if(ImGui.SmallButton("↑")){(rows[index-1],rows[index])=(rows[index],rows[index-1]);dirty=true;}ImGui.EndDisabled();ImGui.SameLine();
            ImGui.BeginDisabled(index==rows.Count-1);if(ImGui.SmallButton("↓")){(rows[index+1],rows[index])=(rows[index],rows[index+1]);dirty=true;}ImGui.EndDisabled();ImGui.SameLine();
            if(ImGui.TreeNode(names[entry.Retainer]+$" · {entry.Categories.Length} categories / {entry.Items.Length} items###RetainerAssignment")){
                var categories=entry.Categories.ToHashSet();var items=entry.Items.ToHashSet();var changed=false;
                if(ImGui.SmallButton("Clear selections")){categories.Clear();items.Clear();changed=true;}
                changed|=DrawStorageCategoryPicker("Categories",categories);
                if(ImGui.TreeNode("Specific items")){
                    foreach(var id in items.ToArray()){if(ImGui.SmallButton("×##"+id)){items.Remove(id);changed=true;}ImGui.SameLine();DrawRetrievalIcon(id);ImGui.TextUnformatted(retrievalCatalog?.Names.GetValueOrDefault(id)??id.ToString());}
                    ImGui.InputText("Find item",ref assignmentItemSearch,80);
                    if(assignmentItemSearch.Length>=2&&retrievalCatalog!=null)foreach(var item in retrievalCatalog.Names.Where(x=>x.Value.Contains(assignmentItemSearch,StringComparison.OrdinalIgnoreCase)).Take(20))if(ImGui.Selectable(item.Value+"##add"+item.Key,items.Contains(item.Key))){items.Add(item.Key);changed=true;}
                    MarketHelp("Specific-item assignments take precedence over category assignments.");ImGui.TreePop();
                }
                if(changed){rows[rows.IndexOf(entry)]=entry with{Categories=categories.Order().ToArray(),Items=items.Order().ToArray()};dirty=true;}
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        if(dirty){SaveConfiguration();organizePreview=[];}
        ImGui.TextDisabled("Existing stacks first · priority top to bottom · saved per character");
    }
}
