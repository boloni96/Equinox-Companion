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
            .Where(item=>armoire?armoireRows!.ContainsKey(item.RowId):GearStorageItemAllowed(item.RowId))
            .Select(item=>item.ItemUICategory.RowId).Where(id=>id>0).ToHashSet();
        LoadCabinetSources();
        if(gearOutfitItems!.Any(id=>armoire?armoireRows!.ContainsKey(id):GearStorageItemAllowed(id)))categories.Add(112);
        var groups=StorageCategoryGroups.Build(DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>()
            .Where(c=>categories.Contains(c.RowId))
            .Select(c=>new StorageCategory(c.RowId,c.Name.ToString(),c.OrderMajor,c.OrderMinor)));
        if(armoire)armoireCategoryGroups=groups;else glamourCategoryGroups=groups;
        return groups;
    }
    private bool GearCategoryMatches(uint item,IReadOnlySet<uint> selected,bool emptyMeansAll=true)
    {
        LoadCabinetSources();
        return GearCategorySelection.Matches(OrganizeCategory(item),gearOutfitItems!.Contains(item),selected,emptyMeansAll);
    }
    private bool DrawGearCategoryPicker(string label,HashSet<uint> selected,bool armoire)
    {
        var groups=GearCategoryGroups(armoire);
        var valid=groups.SelectMany(g=>g.Categories).Select(c=>c.Id).ToHashSet();
        selected.RemoveWhere(id=>!valid.Contains(id));
        var changed=DrawStorageCategoryPicker(label,selected,"All eligible gear categories",groups);
        MarketHelp(armoire
            ?"Only categories present in the game's armoire eligibility list. Empty selection allows all eligible gear. Outfits matches individual pieces from the game's outfit-set catalogue; the source-format checkboxes remain separate."
            :"Equipment categories: weapons, tools, armour, accessories and outfit-set pieces. Empty selection allows all gear; each copy must still pass the game's storage rules and your dye/exclusion filters.");
        return changed;
    }
}
