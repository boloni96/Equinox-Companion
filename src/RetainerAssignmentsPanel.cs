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
        MarketHelp("Regular: consolidate duplicates from the last retainer toward the first in game order; unique items stay put. With assignments enabled: use the selected category/item homes, choosing available space across them. No routine inventory tour when recorded stock is complete.");
        var character=Player.ContentId;var use=config.UseRetainerAssignments.Contains(character);ImGui.SameLine();
        if(ImGui.Checkbox("Use assignments",ref use)){if(use)config.UseRetainerAssignments.Add(character);else config.UseRetainerAssignments.Remove(character);SaveConfiguration();organizePreview=[];}
        MarketHelp("Assignments apply to All retainers in Organise and Entrust. Explicit destinations still override them. Unassigned items are left alone. Settings belong to this character.");
        if(use){
            if(ImGui.SmallButton("Entrust matching bag items"))StartStorageRouting([new(StorageRouting.Bags,StorageAutoRouting.AllRetainers,[],[],AllItems:true)],true);
            MarketHelp("Entrust ALL bag items matching your assignments, including leftovers from an older organiser run. Unassigned items stay in your bags. This is an explicit action, not an automatic part of Organise all.");
        }
        if(!ImGui.CollapsingHeader("Retainer assignments"))return;
        if(!config.RetainerAssignments.TryGetValue(character,out var rows))config.RetainerAssignments[character]=rows=[];
        var owned=ReadSellerRetainers();if(owned.Length==0)owned=(retrievalKnownRetainers.GetValueOrDefault(character)??new Dictionary<ulong,string>()).Select(x=>new SellerRetainerRef(x.Key,x.Value)).ToArray();var names=owned.ToDictionary(x=>x.Id,x=>x.Name);
        foreach(var retainer in owned)if(rows.All(x=>x.Retainer!=retainer.Id))rows.Add(new(retainer.Id,[],[]));
        var dirty=false;
        for(var i=0;i<config.RetrievalPresets.Count;i++)if(string.IsNullOrEmpty(config.RetrievalPresets[i].PlanId)){config.RetrievalPresets[i]=config.RetrievalPresets[i] with{PlanId=Guid.NewGuid().ToString("N")};dirty=true;}
        foreach(var entry in rows.ToArray().Where(x=>names.ContainsKey(x.Retainer))){
            ImGui.PushID(entry.Retainer.ToString());
            if(ImGui.TreeNode(names[entry.Retainer]+$" · {entry.Categories.Length} categories / {entry.Items.Length} items###RetainerAssignment")){
                var categories=entry.Categories.ToHashSet();var items=entry.Items.ToHashSet();var plans=(entry.Plans??[]).ToHashSet();var changed=false;
                if(ImGui.SmallButton("Clear selections")){categories.Clear();items.Clear();plans.Clear();changed=true;}
                changed|=DrawStorageCategoryPicker("Categories",categories);
                if(ImGui.BeginCombo("Saved plans",$"{plans.Count} selected")){
                    foreach(var plan in config.RetrievalPresets){var selected=plans.Contains(plan.PlanId);if(ImGui.Checkbox(plan.Name+"##"+plan.PlanId,ref selected)){if(selected)plans.Add(plan.PlanId);else plans.Remove(plan.PlanId);changed=true;}}
                    foreach(var missing in plans.Where(id=>!config.RetrievalPresets.Any(p=>p.PlanId==id)).ToArray())if(ImGui.SmallButton("Remove deleted plan##"+missing)){plans.Remove(missing);changed=true;}
                    ImGui.EndCombo();
                }
                MarketHelp("Use the saved plan's listed item IDs as a custom group, for both HQ and NQ. Edits follow automatically; rename keeps the link. Quantities, keep amounts, categories and recipe expansion are not applied. Specific items and plan items take precedence over category homes. Deleted plans stop contributing items.");
                if(ImGui.TreeNode("Specific items")){
                    foreach(var id in items.ToArray()){if(ImGui.SmallButton("×##"+id)){items.Remove(id);changed=true;}ImGui.SameLine();DrawRetrievalIcon(id);ImGui.TextUnformatted(retrievalCatalog?.Names.GetValueOrDefault(id)??id.ToString());}
                    ImGui.InputText("Find item",ref assignmentItemSearch,80);
                    if(assignmentItemSearch.Length>=2&&retrievalCatalog!=null)foreach(var item in retrievalCatalog.Names.Where(x=>x.Value.Contains(assignmentItemSearch,StringComparison.OrdinalIgnoreCase)).Take(20))if(ImGui.Selectable(item.Value+"##add"+item.Key,items.Contains(item.Key))){items.Add(item.Key);changed=true;}
                    MarketHelp("Specific-item assignments take precedence over category assignments.");ImGui.TreePop();
                }
                if(changed){rows[rows.IndexOf(entry)]=entry with{Categories=categories.Order().ToArray(),Items=items.Order().ToArray(),Plans=plans.Order().ToArray()};dirty=true;}
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        if(dirty){SaveConfiguration();organizePreview=[];}
        ImGui.TextDisabled("Allowed homes · available space first · saved per character");
    }
}
