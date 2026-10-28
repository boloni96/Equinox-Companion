using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private StorageCategoryGroup[]? storageCategoryGroups;
    private StorageCategoryGroup[] StorageGroups()=>storageCategoryGroups??=StorageCategoryGroups.Build(DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>().Select(c=>new StorageCategory(c.RowId,c.Name.ToString(),c.OrderMajor,c.OrderMinor)));
    private bool DrawStorageCategoryPicker(string label,HashSet<uint> selected,string empty="None selected")
    {
        var changed=false;
        if(ImGui.BeginCombo(label,selected.Count==0?empty:selected.Count+" subcategories",ImGuiComboFlags.HeightLarge)){
            foreach(var group in StorageGroups()){
                ImGui.PushID(group.Name);
                var count=group.Categories.Count(c=>selected.Contains(c.Id));var all=count==group.Categories.Length;
                if(ImGui.Checkbox("##All",ref all)){StorageCategoryGroups.Select(selected,group,all);changed=true;count=all?group.Categories.Length:0;}
                MarketHelp("Select or clear every subcategory in "+group.Name+". A partial selection becomes all selected when checked.");ImGui.SameLine();
                if(ImGui.TreeNode($"{group.Name} · {count}/{group.Categories.Length}###Children")){
                    foreach(var category in group.Categories){var enabled=selected.Contains(category.Id);if(ImGui.Checkbox(category.Name+"##"+category.Id,ref enabled)){if(enabled)selected.Add(category.Id);else selected.Remove(category.Id);changed=true;}}
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        return changed;
    }
    private void AddStorageGroup(HashSet<uint> selected,string name)
    {
        var group=StorageGroups().FirstOrDefault(g=>g.Name==name);if(group!=null)StorageCategoryGroups.Select(selected,group,true);
    }
}
