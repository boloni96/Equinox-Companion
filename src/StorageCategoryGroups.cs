namespace EquinoxCompanion;
internal sealed record StorageCategory(uint Id,string Name,int Major,int Minor);
internal sealed record StorageCategoryGroup(string Name,StorageCategory[] Categories);
internal static class StorageCategoryGroups
{
    internal static string Group(StorageCategory category)=>category.Id switch
    {
        57 or >=64 and <=80 or 82 or 95=>"Housing",
        55=>"Dyes",
        81 or 86 or 94=>"Collectibles",
        33=>"Fishing tackle",
        112=>"Outfits",
        58 or 83=>"Materia",
        59=>"Crystals",
        60=>"Catalysts",
        >=90 and <=93 or >=101 and <=104=>"Workshop parts",
        _=>category.Major switch{1=>"Weapons",2=>"Tools",3 or 4=>"Armour & accessories",5=>"Food & medicine",6=>"Crafting materials",_=>"Other items"}
    };
    internal static StorageCategoryGroup[] Build(IEnumerable<StorageCategory> categories)
    {
        var rows=categories.Where(c=>c.Id>0&&!string.IsNullOrWhiteSpace(c.Name)).DistinctBy(c=>c.Id).ToArray();
        return new[]{"Housing","Dyes","Crafting materials","Materia","Crystals","Catalysts","Workshop parts","Weapons","Tools","Armour & accessories","Food & medicine","Fishing tackle","Outfits","Collectibles","Other items"}
            .Select(name=>new StorageCategoryGroup(name,rows.Where(c=>Group(c)==name).OrderBy(c=>c.Minor).ThenBy(c=>c.Id).ToArray())).Where(g=>g.Categories.Length>0).ToArray();
    }
    internal static string[] Summary(IEnumerable<StorageCategoryGroup> groups,IReadOnlySet<uint> selected)=>
        groups.Select(g=>(Group:g,Checked:g.Categories.Where(c=>selected.Contains(c.Id)).ToArray())).Where(x=>x.Checked.Length>0)
            .Select(x=>x.Checked.Length==x.Group.Categories.Length?x.Group.Name:x.Group.Name+": "+string.Join(", ",x.Checked.Select(c=>c.Name))).ToArray();
    internal static void Select(HashSet<uint> selection,StorageCategoryGroup group,bool enabled)
    {
        foreach(var category in group.Categories){if(enabled)selection.Add(category.Id);else selection.Remove(category.Id);}
    }
}
