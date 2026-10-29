using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private int retrievalCategoryWithdraw=1,retrievalCategoryKeep;
    private bool retrievalFilterCategories,retrievalTransferAll;
    private void DrawTransferAmount()
    {
        var mode=retrievalTransferAll?1:0;ImGui.SetNextItemWidth(220);
        if(ImGui.Combo("##TransferAmount",ref mode,"Plan quantities\0All available listed items\0")){retrievalTransferAll=mode==1;retrievalPreviewDirty=true;retrievalPreview=[];}
        MarketHelp("All available transfers only the listed items matching plan categories and quality. No recipe expansion. Entrust keeps imported Keep amounts and protected items. This temporary option resets when loading a plan.");
    }
    private RetrievalEntry[] FilteredPlanItems()=>PlanItemScope.Filter(retrievalItems,retrievalFilterCategories,retrievalSendCategories,OrganizeCategory);
    private RetrievalEntry[] PlanEntrustEntries()=>retrievalTransferAll
        ?FilteredPlanItems().Select(e=>e with{Quantity=Math.Max(0,RetrievalHeld(e.ItemId,retrievalQuality)-chestKeep)}).Where(e=>e.Quantity>0).ToArray()
        :RetainerRetrieval.Combine(FilteredPlanItems().Select(e=>e with{Quantity=checked(e.Quantity*retrievalMultiplier)}));
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

