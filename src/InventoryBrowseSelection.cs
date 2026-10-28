namespace EquinoxCompanion;
internal static class InventoryBrowseSelection
{
    internal static bool Matches(string name,uint category,string search,IReadOnlySet<uint> categories,bool allCategories)=>
        name.Contains(search,StringComparison.OrdinalIgnoreCase)&&(allCategories||categories.Contains(category));
    internal static RetrievalEntry[] Entries(IEnumerable<RetrievalEntry> available,IReadOnlySet<uint> selected,int quantity)=>
        RetainerRetrieval.Combine(available.Where(e=>selected.Contains(e.ItemId)).DistinctBy(e=>e.ItemId).Select(e=>e with{Quantity=quantity,RecipeId=0,ReadyMaterial=false}));
    internal static RetrievalPreset Plan(RetrievalEntry[] entries,bool entrust,RetrievalQuality quality)
    {
        if(entries.Length==0)throw new ArgumentException("Select at least one inventory item first.");
        return new(entrust?"Entrust selection":"Withdrawal selection",entries,Mode:RetrievalMode.CompletedItems,Quality:quality,
            DestinationRetainer:StorageAutoRouting.AllRetainers,Entrust:entrust?new([],[],[]):null);
    }
}
