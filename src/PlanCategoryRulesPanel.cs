using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private int retrievalCategoryWithdraw=1,retrievalCategoryKeep;
    private bool retrievalFilterCategories;
    private RetrievalEntry[] FilteredPlanItems()=>PlanItemScope.Filter(retrievalItems,retrievalFilterCategories,retrievalSendCategories,OrganizeCategory);
    private RetrievalEntry[] PlanEntrustEntries()=>RetainerRetrieval.Combine(FilteredPlanItems().Select(e=>e with{Quantity=checked(e.Quantity*retrievalMultiplier)}));
    private StorageRoute[] ScopeOrganizeRules(IEnumerable<StorageRoute> rules)=>PlanItemScope.Restrict(rules,FilteredPlanItems().Where(e=>retrievalEntrust==null||!retrievalEntrust.Protected.Contains(e.ItemId)).Select(e=>e.ItemId),OrganizeCategory);
    private void DrawPlanCategoryRules()
    {
        var changed=ImGui.Checkbox("Filter plan categories",ref retrievalFilterCategories);
        if(changed&&retrievalFilterCategories&&retrievalSendCategories.Count==0)retrievalSendCategories.UnionWith(retrievalItems.Select(e=>OrganizeCategory(e.ItemId)));
        MarketHelp("Only items saved in this plan can be moved. Selected categories are allowed; uncheck Housing to leave those listed items untouched. Assignments choose destinations only.");
        if(retrievalFilterCategories){ImGui.SetNextItemWidth(-1);changed|=DrawStorageCategoryPicker("##PlanCategoryRules",retrievalSendCategories,"None · no plan items");}
        if(changed){retrievalPreview=[];retrievalPlan=[];retrievalPreviewDirty=true;InvalidateOrganizePreview();if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
    }
}
