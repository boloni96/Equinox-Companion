using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private RetainerToolsWindow retainerToolsWindow=null!;
    private bool retainerToolsWasOpen;private string? retrievalEditingPreset;
    private sealed class RetainerToolsWindow(Plugin p):Window("Retainer tools · Equinox###EquinoxRetainerTools",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoSavedSettings)
    {
        public override void Draw()=>p.DrawRetainerTools();
        public override void OnClose(){p.retrievalPickItems=false;}
        public override unsafe void PreDraw(){var a=SellerAddon("RetainerList");if(a!=null){Position=new Vector2(Math.Max(0,(int)a->X),Math.Max(0,a->Y+a->GetScaledHeight(true)+4));PositionCondition=ImGuiCond.Always;}else Position=null;RespectCloseHotkey=false;}
    }
    private static unsafe bool RetainerToolsContext(){var rm=RetainerManager.Instance();return SellerAddon("RetainerList")!=null||(rm!=null&&RetrieverActive(rm->LastSelectedRetainerId));}
    private void SaveRetrievalPreset()
    {
        if(string.IsNullOrWhiteSpace(retrievalName))throw new ArgumentException("Name this plan first.");
        var at=config.RetrievalPresets.FindIndex(x=>x.Name==(retrievalEditingPreset??retrievalName));
        var p=new RetrievalPreset(retrievalName,RetainerRetrieval.Combine(retrievalItems),retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality,DestinationRetainer:retrievalSendTo,SendCategories:retrievalSendCategories.ToArray());
        if(config.RetrievalPresets.Any(x=>x.Name==p.Name)&&at!=config.RetrievalPresets.FindIndex(x=>x.Name==p.Name))throw new ArgumentException("Another plan already has this name.");
        if(at>=0)config.RetrievalPresets[at]=p;else if(config.RetrievalPresets.Count<25)config.RetrievalPresets.Add(p);else throw new ArgumentException("Maximum 25 presets.");
        retrievalEditingPreset=p.Name;SaveConfiguration();
    }
    private void DrawRetrievalPlanSelector()
    {
        ImGui.SetNextItemWidth(220);
        if(ImGui.BeginCombo("Plan",retrievalEditingPreset??retrievalName)){
            foreach(var plan in config.RetrievalPresets.ToArray())if(ImGui.Selectable(plan.Name,retrievalEditingPreset==plan.Name)){if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);SelectRetrievalPreset(config.RetrievalPresets.First(x=>x.Name==plan.Name));}
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Select the saved plan you want to edit or run. Shift/Alt changes are saved to that selected plan. HQ/NQ and crafting modes remain part of the plan.");
    }
    private unsafe void TickRetainerTools(DateTimeOffset now)
    {
        var open=config.EnableRetriever&&Player.IsLoaded&&Conditions[ConditionFlag.OccupiedSummoningBell];
        if(open&&!retainerToolsWasOpen){retainerToolsWindow.IsOpen=true;retrievalRefreshPending=true;LoadRetrievalCatalog();}
        if(!open){retainerToolsWindow.IsOpen=false;if(!retrieverWindow.IsOpen)retrievalPickItems=false;}
        retainerToolsWasOpen=open;
        if(retrievalCatalogTask?.IsCompleted==true){try{retrievalCatalog=retrievalCatalogTask.GetAwaiter().GetResult();}catch(Exception e){retrievalError=e.Message;}retrievalCatalogTask=null;}
        if(open&&retrievalRefreshPending&&!organizeRunning&&!retrievalRun.Running&&!retainerGilRunning){retrievalRefreshPending=false;RetrievalTry(RefreshRetrievalStock);}
    }
    private unsafe void DrawRetainerTools()
    {
        var busy=organizeRunning||retrievalRun.Running||chestRunning||retainerGilRunning;
        ImGui.BeginDisabled(busy);DrawRetrievalPlanSelector();ImGui.SameLine();
        if(ImGui.SmallButton("Edit / new")){OpenRetriever();retrievalShowList=true;}
        DrawRetrievalPicker();
        if(ImGui.SmallButton("Withdraw plan"))StartRetrieval(false);ImGui.SameLine();
        if(ImGui.SmallButton("Entrust plan"))SendRetrievalList(false);ImGui.SameLine();
        if(ImGui.SmallButton("Withdraw gil"))StartRetainerGil();
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Withdraw available gil from the currently open retainer, or all retainers when the retainer list is open. Never deposits gil; verifies receipt and respects your gil cap.");
        ImGui.EndDisabled();
        if(busy&&ImGui.SmallButton("Stop")){StopRetainerGil("Stopped by you.");if(organizeRunning)StopOrganize("Stopped by you.");if(retrievalRun.Running)retrievalRun.Stop("Stopped by you.");}
        var status=retainerGilRunning?retainerGilStatus:organizeRunning?organizeStatus:retrievalRun.Running?retrievalRun.Status:retrievalError.Length>0?retrievalError:retainerGilStatus;
        if(status.Length>0){ImGui.TextUnformatted(status.Length>90?status[..87]+"…":status);if(ImGui.IsItemHovered())ImGui.SetTooltip(status);}
        if(ImGui.CollapsingHeader($"Plan items · {retrievalItems.Length}")){
            ImGui.BeginDisabled(busy);
            ImGui.BeginChild("QuickPlanItems",new Vector2(380,150),true);
            foreach(var e in retrievalItems.ToArray()){
                ImGui.PushID((int)e.ItemId);if(ImGui.SmallButton("×")){retrievalItems=retrievalItems.Where(x=>x.ItemId!=e.ItemId).ToArray();retrievalSelected.Remove(e.ItemId);retrievalPreviewDirty=true;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}ImGui.SameLine();DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted(e.Name+" ×"+e.Quantity);ImGui.PopID();
            }ImGui.EndChild();ImGui.EndDisabled();
        }
    }
}
