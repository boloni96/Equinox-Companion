using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static void DrawOrganizerItemName(string name)
    {
        ImGui.TextUnformatted(name);
        if(ImGui.IsItemClicked(ImGuiMouseButton.Left))ImGui.SetClipboardText(name);
    }
    private static string OrganizerMaterialDetails(PreparationRow row)
    {
        var use=string.Join("\n",row.DemandSources.Select(s=>$"{s.Quantity:N0} for {s.Name}"));
        return $"Click to copy {row.Name}\nRequired: {row.Required:N0} · bags used: {row.Bags:N0} · stored used: {row.Retainers:N0}\nTo craft: {row.CraftQuantity:N0} · still missing: {row.Missing:N0}\n"+use+
            (CrystalStorage.IsItem(row.ItemId)?"\nCrystals are consumed across all planned crafts. Queue totals can exceed the 9,999 carrying limit.":"")+
            (!row.Complete?"\nStorage records are incomplete; verify before buying.":"");
    }
}
