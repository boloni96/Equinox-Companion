using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private StorageCategoryGroup[]? armoireCategoryGroups,glamourCategoryGroups;
    private StorageCategoryGroup[] GearCategoryGroups(bool armoire)
    {
        var cached=armoire?armoireCategoryGroups:glamourCategoryGroups;
        if(cached!=null)return cached;
        LoadArmoireCatalogue();
        // Build from item eligibility, not general inventory groups such as food,
        // furniture or crafting materials. Cabinet rows are authoritative for armoire.
        var categories=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()
            .Where(item=>armoire?armoireRows!.ContainsKey(item.RowId):item.IsGlamorous&&item.EquipSlotCategory.RowId>0)
            .Select(item=>item.ItemUICategory.RowId).Where(id=>id>0).ToHashSet();
        var groups=StorageCategoryGroups.Build(DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>()
            .Where(c=>categories.Contains(c.RowId))
            .Select(c=>new StorageCategory(c.RowId,c.Name.ToString(),c.OrderMajor,c.OrderMinor)));
        if(armoire)armoireCategoryGroups=groups;else glamourCategoryGroups=groups;
        return groups;
    }
    private bool DrawGearCategoryPicker(string label,HashSet<uint> selected,bool armoire)
    {
        var groups=GearCategoryGroups(armoire);
        var valid=groups.SelectMany(g=>g.Categories).Select(c=>c.Id).ToHashSet();
        selected.RemoveWhere(id=>!valid.Contains(id));
        var changed=DrawStorageCategoryPicker(label,selected,"All eligible gear categories",groups);
        MarketHelp(armoire
            ?"Only categories present in the game's armoire eligibility list. Empty selection allows all eligible gear. Outfit set pieces is a separate storage-format filter."
            :"Equipment categories only: weapons, tools, armour and accessories. Empty selection allows all gear; each copy must still pass the game's storage rules and your dye/exclusion filters.");
        return changed;
    }
}
