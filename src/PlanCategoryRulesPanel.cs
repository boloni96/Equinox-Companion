using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private int retrievalCategoryWithdraw=1,retrievalCategoryKeep;
    private readonly HashSet<uint> retrievalCategoryPlanItems=[];
    private IEnumerable<RetrievalEntry> CategoryRuleEntries()=>retrievalSendCategories.SelectMany(c=>retrievalCatalog!.Categories.GetValueOrDefault(c)??[]);
    private Dictionary<uint,int> CategoryEntrustAmounts(IEnumerable<ChestStack> bags,IEnumerable<uint> listed,RetrievalQuality quality)=>PlanCategoryRules.Entrust(bags,listed,retrievalSendCategories,OrganizeCategory,quality,retrievalCategoryKeep);
    private void DrawPlanCategoryRules()
    {
        ImGui.SetNextItemWidth(Math.Max(170,ImGui.GetContentRegionAvail().X-135));
        var changed=DrawStorageCategoryPicker("##PlanCategoryRules",retrievalSendCategories,"Categories · listed items only");
        MarketHelp("Saved rules for both Withdraw and Entrust. Matching category items are included even when they are not listed below. Explicit item quantities and Keep amounts take priority. Withdraw selected uses only checked explicit items.");
        if(retrievalSendCategories.Count>0){
            ImGui.SameLine();if(ImGui.SmallButton("Rule amounts…"))ImGui.OpenPopup("CategoryRuleAmounts");
            ImGui.SetNextWindowSize(new(360,0),ImGuiCond.Always);
            if(ImGui.BeginPopup("CategoryRuleAmounts")){
                ImGui.SetNextItemWidth(110);if(ImGui.InputInt("Withdraw each",ref retrievalCategoryWithdraw)){retrievalCategoryWithdraw=Math.Clamp(retrievalCategoryWithdraw,1,RetainerRetrieval.MaxQuantity);changed=true;}
                MarketHelp("Amount per matching item type. Crystals are capped at 9,999. Subtract matching items in bags applies as usual; unavailable category items are skipped.");
                ImGui.SetNextItemWidth(110);if(ImGui.InputInt("Keep each",ref retrievalCategoryKeep)){
                    retrievalCategoryKeep=Math.Clamp(retrievalCategoryKeep,0,RetainerRetrieval.MaxQuantity);
                    if(retrievalEntrust!=null)foreach(var category in retrievalSendCategories)retrievalEntrust.CategoryKeep[category]=retrievalCategoryKeep;
                    changed=true;
                }
                MarketHelp("Entrust matching bag items above Keep. This changes Keep for the selected category rules; explicit item Keep and protection still take priority. Imported per-category Keep values stay unchanged until edited here.");
                if(retrievalEntrust!=null)foreach(var category in retrievalSendCategories){var name=StorageGroups().SelectMany(g=>g.Categories).FirstOrDefault(c=>c.Id==category)?.Name??category.ToString();ImGui.TextUnformatted(name+" · Keep "+retrievalEntrust.CategoryKeep.GetValueOrDefault(category));}
                ImGui.EndPopup();
            }
        }
        if(changed){if(retrievalEntrust!=null)foreach(var category in retrievalSendCategories)retrievalEntrust.CategoryKeep.TryAdd(category,retrievalCategoryKeep);retrievalPreview=[];retrievalPlan=[];retrievalPreviewDirty=true;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
    }
}
