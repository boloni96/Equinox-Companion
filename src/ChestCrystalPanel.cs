using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool chestCrystals;
    private int chestCrystalFamily,chestCrystalElement,chestCrystalAmount=9999;
    private void DrawChestSelection()
    {
        ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("##ChestSelection",chestCrystals?"Crystals · no plan":retrievalName)){
            if(ImGui.Selectable("Crystals · no plan",chestCrystals))chestCrystals=true;
            if(ImGui.Selectable("Current list · "+retrievalName,!chestCrystals))chestCrystals=false;
            foreach(var plan in config.RetrievalPresets.ToArray())if(ImGui.Selectable(plan.Name,!chestCrystals&&retrievalEditingPreset==plan.Name)){SelectRetrievalPreset(plan);chestCrystals=false;}
            ImGui.EndCombo();
        }
        MarketHelp("Crystals works directly without a saved plan. Other lists use the current Organizer quantities and settings.");
    }
    private void DrawChestCrystalOptions()
    {
        if(ImGui.BeginTable("CrystalControls",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.Combo("##CrystalFamily",ref chestCrystalFamily,"All crystals\0Shards\0Crystals\0Clusters\0");
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.Combo("##CrystalElement",ref chestCrystalElement,"All elements\0Fire\0Ice\0Wind\0Earth\0Lightning\0Water\0");
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.InputInt("##CrystalQuantity",ref chestCrystalAmount,0,0);chestCrystalAmount=Math.Clamp(chestCrystalAmount,1,9999);MarketHelp("Withdraw this many more of each selected type, limited by stock and the 9,999 carrying cap.");
            ImGui.TableNextColumn();DrawChestKeepControl();ImGui.EndTable();
        }
        ImGui.TextDisabled("Withdraw: quantity each · Entrust: all above Keep");
    }
    private void DrawChestKeepControl()
    {
        ImGui.SetNextItemWidth(-1);if(ImGui.InputInt("##ChestKeep",ref chestKeep,0,0)){chestKeep=Math.Clamp(chestKeep,0,RetainerRetrieval.MaxQuantity);config.StorageKeepInBags=chestKeep;SaveConfiguration();}
        MarketHelp("Keep in bags: crystal entrust and duplicate deposits leave this amount of each type. Saved until you change it.");
    }
}
