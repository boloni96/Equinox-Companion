using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static Vector4 OrganizerMaterialColor(PreparationCoverage coverage)=>coverage switch {
        PreparationCoverage.Bags=>new(.08f,.5f,.16f,.65f),
        PreparationCoverage.Retainers=>new(.55f,.4f,.04f,.65f),
        PreparationCoverage.Craftable=>new(.05f,.3f,.6f,.65f),
        PreparationCoverage.Missing=>new(.6f,.12f,.1f,.65f),_=>new(.3f,.3f,.3f,.65f)};
    private void OrganizerMaterialRow(PreparationCoverage coverage)
    {
        if(config.CraftingColors)ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,ImGui.ColorConvertFloat4ToU32(OrganizerMaterialColor(coverage)));
    }
    private void DrawOrganizerInfo()
    {
        ImGui.SmallButton("Info##OrganizerLegend");
        if(!ImGui.IsItemHovered())return;
        ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*31);
        ImGui.TextUnformatted("MATERIAL AVAILABILITY");
        foreach(var entry in new[]{(PreparationCoverage.Bags,"Green: required materials are in bags."),(PreparationCoverage.Retainers,"Gold: recorded storage covers the rest; withdraw first."),(PreparationCoverage.Craftable,"Blue: remaining materials can be crafted using recorded ingredients."),(PreparationCoverage.Missing,"Red: materials or ingredients are missing."),(PreparationCoverage.Incomplete,"Grey: stock records are incomplete; verify the shortage.")}){
            ImGui.TextColored(OrganizerMaterialColor(entry.Item1) with {W=1},"■");ImGui.SameLine();ImGui.TextWrapped(entry.Item2);
        }
        ImGui.Separator();
        ImGui.TextWrapped("These colors apply to material requirement tables. Bags and storage used are allocated quantities, not every item you own. Hover an item for sources and quality details. Change material colors in Options.");
        ImGui.TextWrapped("FC workshop: every delivery needs a complete NQ or HQ set. Mixed remnants cannot be combined into one set. Missing counts show raw shortages; Craft shows finished materials still to make. Refresh materials after stock changes.");
        ImGui.PopTextWrapPos();ImGui.EndTooltip();
    }
}
