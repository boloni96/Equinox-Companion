using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static void PushRetainerToolbarStyle()
    {
        MarketGameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.ColorThemeType,out uint theme);
        var light=theme==1;var blue=theme>=2;
        ImGui.PushStyleColor(ImGuiCol.WindowBg,light?new Vector4(.78f,.77f,.73f,.98f):blue?new Vector4(.08f,.14f,.25f,.98f):new Vector4(.15f,.15f,.15f,.98f));
        ImGui.PushStyleColor(ImGuiCol.Text,light?new Vector4(.12f,.12f,.12f,1):new Vector4(.92f,.90f,.84f,1));
        ImGui.PushStyleColor(ImGuiCol.Border,new Vector4(.55f,.51f,.39f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,light?new Vector4(.88f,.87f,.83f,1):new Vector4(.08f,.08f,.08f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,light?new Vector4(.67f,.66f,.62f,1):new Vector4(.24f,.24f,.24f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.4f,.39f,.34f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.5f,.46f,.34f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(5,4));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(4,3));
    }
    private void DrawChestToolbar()
    {
        var busy=chestRunning||organizeRunning||retrievalRun.Running||retainerGilRunning;
        ImGui.BeginDisabled(busy);
        // Fixed width keeps the docked toolbar compact.
        ImGui.PushItemWidth(160);
        if(ImGui.BeginCombo("##ChestPlan",chestCrystals?"Crystals · no plan":retrievalName)){
            if(ImGui.Selectable("Crystals · no plan",chestCrystals))chestCrystals=true;
            if(ImGui.Selectable("Current list · "+retrievalName,!chestCrystals))chestCrystals=false;
            foreach(var plan in config.RetrievalPresets.ToArray())if(ImGui.Selectable(plan.Name,!chestCrystals&&retrievalEditingPreset==plan.Name)){SelectRetrievalPreset(plan);chestCrystals=false;}
            ImGui.EndCombo();
        }ImGui.PopItemWidth();
        MarketHelp(chestCrystals?$"No plan needed. Withdraw {chestCrystalAmount:N0} more of each selected crystal type; Entrust all above Keep {chestKeep}. Choose types in Options.":"Current list: "+retrievalName);
        ImGui.SameLine();if(ImGui.Button("Withdraw"))StartChest(0);MarketHelp(chestCrystals?"Withdraw selected crystals directly, limited by carrying capacity.":"Withdraw the current plan from the selected chest tabs into your bags.");
        ImGui.SameLine();if(ImGui.Button("Entrust"))StartChest(4);MarketHelp(chestCrystals?"Entrust selected crystals, leaving Keep in bags.":"Entrust the current plan from your bags into the selected chest tabs.");
        ImGui.SameLine();if(ImGui.Button("Duplicates"))StartChest(1);MarketHelp($"Deposit items already stored in the selected tabs. Keep {chestKeep} of each item/quality in bags. Crystals use their own storage.");
        ImGui.EndDisabled();ImGui.SameLine();
        if(chestRunning){if(ImGui.Button("Stop"))StopChest("Stopped by you. An already-sent transfer may still complete.");ImGui.SameLine();}
        if(ImGui.Button("...##ChestOptions"))ImGui.OpenPopup("Chest options");MarketHelp("Chest tabs, stack merging, move between tabs, and Organizer.");
        var status=retrievalError.Length>0?retrievalError:chestStatus;
        if(status.Length>0){ImGui.SameLine();ImGui.TextDisabled(chestRunning?"…":"i");MarketHelp(status);}
        ImGui.SetNextWindowSize(new(420,0),ImGuiCond.Always);
        if(ImGui.BeginPopup("Chest options")){
            ImGui.TextUnformatted("FC chest");ImGui.Separator();ImGui.BeginDisabled(busy);
            if(chestCrystals)DrawChestCrystalOptions();
            ImGui.BeginDisabled(chestCrystals);
            ImGui.SetNextItemWidth(180);ImGui.Combo("Source / target",ref chestTab,"All accessible tabs\0Tab 1\0Tab 2\0Tab 3\0Tab 4\0Tab 5\0Crystals\0");
            MarketHelp("Withdraw reads these tabs; Entrust and Duplicates deposit into them. Your FC permissions still apply.");
            if(ImGui.Button("Withdraw selected plan items")){StartChest(0,true);ImGui.CloseCurrentPopup();}
            MarketHelp("Withdraw only entries checked in the current plan.");
            ImGui.Separator();
            ImGui.SetNextItemWidth(90);if(ImGui.InputInt("Keep in bags",ref chestKeep)){chestKeep=Math.Clamp(chestKeep,0,RetainerRetrieval.MaxQuantity);config.StorageKeepInBags=chestKeep;SaveConfiguration();}
            MarketHelp("Duplicate deposits leave this amount of each item and quality in your bags.");
            if(ImGui.Button("Merge partial stacks")){StartChest(2);ImGui.CloseCurrentPopup();}MarketHelp("Combine partial stacks within each selected chest tab. HQ and NQ stay separate.");
            ImGui.Separator();
            ImGui.SetNextItemWidth(100);ImGui.SliderInt("Destination tab",ref chestDestination,1,5);
            if(ImGui.Button("Move plan items to this tab")){StartChest(3);ImGui.CloseCurrentPopup();}
            ImGui.EndDisabled();
            MarketHelp("Move matching plan items from selected source tabs to the destination tab. Crystals stay in dedicated storage.");
            ImGui.EndDisabled();ImGui.Separator();
            if(ImGui.Button("Edit plans / open Organizer")){OpenRetriever();retrievalShowList=true;ImGui.CloseCurrentPopup();}
            ImGui.Checkbox("Attach above chest",ref chestDock);
            if(ImGui.Button("Hide toolbar")){chestToolsWindow.IsOpen=false;ImGui.CloseCurrentPopup();}
            MarketHelp("Appears again next time you open the FC chest.");
            ImGui.EndPopup();
        }
    }
}
