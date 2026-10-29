namespace EquinoxCompanion;

internal static class CraftingWithdrawal
{
    internal static RetrievalPreset Create(uint itemId, string name, bool rawMaterials)
    {
        if (itemId == 0) throw new ArgumentException("Choose an item first.");
        return new RetrievalPreset("Craft · " + name, [new(itemId, name, 1)],
            Mode: rawMaterials ? RetrievalMode.AllMaterials : RetrievalMode.DirectIngredients);
    }
}
