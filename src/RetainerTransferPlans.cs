using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private void DrawRetrievalSendControls()
    {
        if(!ImGui.CollapsingHeader("Send / entrust this plan"))return;
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        if(ImGui.BeginCombo("To retainer",names.GetValueOrDefault(retrievalSendTo,"Choose destination"))){foreach(var row in names)if(ImGui.Selectable(row.Value+"##send"+row.Key,retrievalSendTo==row.Key))retrievalSendTo=row.Key;ImGui.EndCombo();}
        if(ImGui.BeginCombo("Send categories",retrievalSendCategories.Count==0?"None · listed items only":retrievalSendCategories.Count+" categories")){
            foreach(var cat in DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>().Where(c=>c.RowId>0&&!string.IsNullOrWhiteSpace(c.Name.ToString()))){var selected=retrievalSendCategories.Contains(cat.RowId);if(ImGui.Checkbox(cat.Name.ToString()+"##send"+cat.RowId,ref selected)){if(selected)retrievalSendCategories.Add(cat.RowId);else retrievalSendCategories.Remove(cat.RowId);}}ImGui.EndCombo();}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Send all matching items in your bags, plus the listed quantities. Categories affect sending only; retrieve uses the explicit item list. Save preset remembers the destination and categories.");
        if(ImGui.Button("Send plan"))SendRetrievalList(false);ImGui.SameLine();if(ImGui.Button("Entrust duplicates"))SendRetrievalList(true);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Send bag items already stored on this destination retainer. Quality selection applies. No items are discarded or sold.");
    }
}
