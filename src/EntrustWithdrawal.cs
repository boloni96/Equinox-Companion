namespace EquinoxCompanion;
internal static class EntrustWithdrawal
{
    // Only explicit item quantities. Entrust keep counts, categories, protection and
    // recipe expansion do not define what the user asks to withdraw.
    internal static RetainerRetrieval.CraftPlan Build(IEnumerable<RetrievalEntry> entries)=>
        RetainerRetrieval.BuildPlan(entries,1,false,RetrievalMode.CompletedItems,
            new Dictionary<uint,RetrievalRecipe[]>(),new Dictionary<uint,RetrievalEntry[]>());
}
