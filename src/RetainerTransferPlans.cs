using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private void DrawRetrievalSendControls()
    {
        if(!ImGui.CollapsingHeader("Entrust this plan"))return;
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        if(ImGui.BeginCombo("To retainer",retrievalSendTo==StorageAutoRouting.AllRetainers?"All retainers":names.GetValueOrDefault(retrievalSendTo,"Choose destination"))){if(ImGui.Selectable("All retainers",retrievalSendTo==StorageAutoRouting.AllRetainers))retrievalSendTo=StorageAutoRouting.AllRetainers;foreach(var row in names)if(ImGui.Selectable(row.Value+"##send"+row.Key,retrievalSendTo==row.Key))retrievalSendTo=row.Key;ImGui.EndCombo();}
        if(ImGui.BeginCombo("Entrust categories",retrievalSendCategories.Count==0?"None · listed items only":retrievalSendCategories.Count+" categories")){
            foreach(var cat in DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>().Where(c=>c.RowId>0&&!string.IsNullOrWhiteSpace(c.Name.ToString()))){var selected=retrievalSendCategories.Contains(cat.RowId);if(ImGui.Checkbox(cat.Name.ToString()+"##send"+cat.RowId,ref selected)){if(selected)retrievalSendCategories.Add(cat.RowId);else retrievalSendCategories.Remove(cat.RowId);}}ImGui.EndCombo();}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Entrust all matching items in your bags, plus the listed quantities. Categories affect entrusting only; withdraw uses the explicit item list. Save preset remembers the destination and categories.");
        if(ImGui.Button("Entrust plan"))SendRetrievalList(false);if(retrievalEntrust==null){ImGui.SameLine();if(ImGui.Button("Entrust duplicates"))SendRetrievalList(true);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Entrust bag items already stored on this destination retainer. Quality selection applies. No items are discarded or sold.");}
    }
}
