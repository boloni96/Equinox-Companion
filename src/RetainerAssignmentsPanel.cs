using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string assignmentItemSearch="";
    private void DrawRetainerAssignments()
    {
        if(!Player.IsLoaded){ImGui.TextDisabled("Log in to edit this character’s assignments.");return;}
        var eligible=NoOrganizerPlan||ScopeOrganizeRules(WholeOrganizerRules()).Length>0;
        if(ImGui.Button(NoOrganizerPlan?"Preview all":"Preview plan"))CheckAllOrganize();ImGui.SameLine();
        ImGui.BeginDisabled(!eligible);if(ImGui.Button(NoOrganizerPlan?"Organise all":"Organise plan"))StartStorageRouting(WholeOrganizerRules(),false);
        ImGui.EndDisabled();
        MarketHelp("With a plan: only its filtered items are eligible. With no plan: all retainer items matching the movement category and quality filters are eligible. Regular: consolidate duplicates from the last retainer toward the first in game order; unique items stay put. With assignments enabled: use the selected category/item homes, choosing available space across them. Your existing bag items stay untouched; only empty space is used for staging. No routine inventory tour when recorded stock is complete.");
        ImGui.SameLine();DrawUseRetainerAssignments();
        DrawAssignmentEditor();
    }
    private void DrawUseRetainerAssignments()
    {
        var character=Player.ContentId;var use=config.UseRetainerAssignments.Contains(character);
        ImGui.BeginDisabled(!Player.IsLoaded);
        if(ImGui.Checkbox("Use assignments",ref use)){
            if(use)config.UseRetainerAssignments.Add(character);else config.UseRetainerAssignments.Remove(character);
            SaveConfiguration();InvalidateOrganizePreview();
        }
        ImGui.EndDisabled();
        MarketHelp("Shared with Organise retainers, saved per character. Entrust to All retainers follows assigned items, plans and categories; unassigned items stay in bags. Choosing a specific retainer overrides assignments. Keep amounts and protection still apply.");
    }
    private void DrawAssignmentEditor()
    {
        if(!Player.IsLoaded)return;
        var character=Player.ContentId;
        if(ImGui.SmallButton("Assignments…"))ImGui.OpenPopup("RetainerAssignmentsPopup");
        // Size comes from the viewport, never the auto-sized popup/child from the previous frame.
        var viewport=ImGui.GetMainViewport().WorkSize;
        ImGui.SetNextWindowSize(new(Math.Min(540,Math.Max(100,viewport.X-24)),Math.Min(400,Math.Max(100,viewport.Y-24))),ImGuiCond.Always);
        if(!ImGui.BeginPopup("RetainerAssignmentsPopup"))return;
        ImGui.BeginChild("AssignmentRows",System.Numerics.Vector2.Zero,false);
        if(!config.RetainerAssignments.TryGetValue(character,out var rows))config.RetainerAssignments[character]=rows=[];
        var owned=ReadSellerRetainers();if(owned.Length==0)owned=(retrievalKnownRetainers.GetValueOrDefault(character)??new Dictionary<ulong,string>()).Select(x=>new SellerRetainerRef(x.Key,x.Value)).ToArray();var names=owned.ToDictionary(x=>x.Id,x=>x.Name);
        foreach(var retainer in owned)if(rows.All(x=>x.Retainer!=retainer.Id))rows.Add(new(retainer.Id,[],[]));
        var dirty=false;
        for(var i=0;i<config.RetrievalPresets.Count;i++)if(string.IsNullOrEmpty(config.RetrievalPresets[i].PlanId)){config.RetrievalPresets[i]=config.RetrievalPresets[i] with{PlanId=Guid.NewGuid().ToString("N")};dirty=true;}
        foreach(var entry in rows.ToArray().Where(x=>names.ContainsKey(x.Retainer))){
            ImGui.PushID(entry.Retainer.ToString());
            var expanded=ImGui.TreeNode(names[entry.Retainer]+$" · {entry.Categories.Length} categories / {entry.Items.Length} items / {entry.Plans?.Length??0} plans###RetainerAssignment");
            if(ImGui.IsItemHovered()){
                var summary=StorageCategoryGroups.Summary(StorageGroups(),entry.Categories.ToHashSet()).ToList();
                foreach(var id in entry.Categories.Where(id=>!StorageGroups().Any(g=>g.Categories.Any(c=>c.Id==id))))summary.Add("Unknown category "+id);
                var selectedPlans=config.RetrievalPresets.Where(p=>(entry.Plans??[]).Contains(p.PlanId)).Select(p=>p.Name).ToArray();
                if(selectedPlans.Length>0)summary.Add("Plans: "+string.Join(", ",selectedPlans));
                var explicitItems=entry.Items.Where(id=>!entry.Categories.Contains(OrganizeCategory(id))).Select(id=>retrievalCatalog?.Names.GetValueOrDefault(id)??id.ToString()).ToArray();
                if(explicitItems.Length>0)summary.Add("Items: "+string.Join(", ",explicitItems));
                if(summary.Count==0)summary.Add("No assigned categories, plans or items.");
                ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*32);ImGui.TextUnformatted(names[entry.Retainer]+"\n"+(config.UseRetainerAssignments.Contains(character)?"Assignments enabled":"Assignments currently disabled")+"\n"+string.Join("\n",summary));ImGui.PopTextWrapPos();ImGui.EndTooltip();
            }
            if(expanded){
                var categories=entry.Categories.ToHashSet();var items=entry.Items.ToHashSet();var plans=(entry.Plans??[]).ToHashSet();var changed=false;
                if(ImGui.SmallButton("Clear selections")){categories.Clear();items.Clear();plans.Clear();changed=true;}
                changed|=DrawStorageCategoryPicker("Categories",categories,"Optional · plan/items only");
                MarketHelp("Saved plans and specific items work without categories. Categories add other eligible item groups; leaving them empty does not disable your plan.");
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
        if(dirty){SaveConfiguration();InvalidateOrganizePreview();}
        ImGui.TextDisabled("Allowed homes · available space first · saved per character");
        ImGui.EndChild();ImGui.EndPopup();
    }
}

