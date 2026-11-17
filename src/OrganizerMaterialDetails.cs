using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static void DrawOrganizerItemName(string name)
    {
        ImGui.TextUnformatted(name);
        if(ImGui.IsItemClicked(ImGuiMouseButton.Left))ImGui.SetClipboardText(name);
    }
    private static void DrawOrganizerMissing(PreparationRow row)
    {
        if(row.Missing==0 && row.MissingIngredients.Length>0){
            ImGui.TextWrapped(string.Join(", ",row.MissingIngredients));
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Shared ingredient shortage across the queue. Where Is Cheap counts each missing ingredient once.");
            return;
        }
        ImGui.TextUnformatted(row.Missing>0&&!row.Complete?$"≤ {row.Missing:N0} · verify":row.Missing.ToString("N0"));
    }
    private static string OrganizerMaterialDetails(PreparationRow row)
    {
        var use=string.Join("\n",row.DemandSources.Select(s=>$"{s.Quantity:N0} for {s.Name}"));
        var state=row.Coverage switch{
            PreparationCoverage.Bags=>"Green · covered by bags",
            PreparationCoverage.Retainers=>"Gold · covered after withdrawing recorded stock",
            PreparationCoverage.Craftable=>"Blue · enough recorded ingredients for all remaining crafts",
            PreparationCoverage.Missing=>row.MissingIngredients.Length>0?"Red · missing ingredients for crafting":"Red · missing material",
            _=>"Grey · incomplete stock records; verify shortage"};
        var missing=row.MissingIngredients.Length>0?"\nMissing ingredients: "+string.Join(", ",row.MissingIngredients):"";
        return state+missing+$"\nRequested directly: {row.RequestedQuantity:N0} · used by other crafts: {row.Required-row.RequestedQuantity:N0}\n"+$"Click to copy {row.Name}\nRequired: {row.Required:N0} · bags used: {row.Bags:N0} · stored used: {row.Retainers:N0}\nTo craft: {row.CraftQuantity:N0} · still missing: {row.Missing:N0}\n"+use+
            (CrystalStorage.IsItem(row.ItemId)?"\nCrystals are consumed across all planned crafts. Queue totals can exceed the 9,999 carrying limit.":"")+
            (!row.Complete?"\nStorage records are incomplete; verify before buying.":"");
    }
}
