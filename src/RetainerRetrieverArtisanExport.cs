using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private RetainerRetrieval.CraftStep[] retrievalCraftSteps=[],retrievalExportSteps=[];
    private RetrievalEntry[] retrievalExportMaterials=[];
    private bool retrievalExportMissing;
    private string retrievalExportError="",retrievalExportNotice="";
    private void PrepareRetrievalArtisanExport()
    {
        retrievalExportMissing=false;retrievalExportNotice="";BuildRetrievalArtisanExport();
        ImGui.OpenPopup("Prepare Artisan crafting list");
    }
    private void BuildRetrievalArtisanExport()
    {
        retrievalExportSteps=[];retrievalExportMaterials=[];retrievalExportError="";
        try
        {
            if(retrievalCatalog==null)return;
            RefreshRetrievalStock();
            var plan=BuildPreparation(retrievalExportMissing);
            retrievalExportSteps=plan.Steps;retrievalExportMaterials=plan.Missing;
            if(retrievalExportMissing&&!retrievalStockComplete)retrievalExportNotice="Some retainer stock is unrecorded. This list uses known stock; refresh before crafting to avoid making extras.";

        }
        catch(RetrievalRecipeChoiceException e){retrievalRecipeChoice=e.ItemId;retrievalExportError=e.Message+" Close this review, choose the recipe in Withdraw, then prepare again.";}
        catch(Exception e){retrievalExportError=e.Message;}
    }
    private void DrawRetrievalArtisanExport()
    {
        ImGui.SetNextWindowSize(new Vector2(600,450),ImGuiCond.FirstUseEver);
        if(!ImGui.BeginPopupModal("Prepare Artisan crafting list"))return;
        if(ImGui.Checkbox("Missing only (Companion subtracts owned stock)",ref retrievalExportMissing))BuildRetrievalArtisanExport();
        ImGui.TextWrapped(retrievalExportMissing?"Exact missing craft counts: Artisan skip-if-enough is disabled. Do not enable stock subtraction again.":"Full requirements: Artisan skip-if-enough is enabled so Artisan subtracts owned stock once.");
        ImGui.TextWrapped("Review the crafting order below. Copy, then use Artisan → Lists → Import List From Clipboard (Artisan Export). No crafting starts automatically.");
        ImGui.BeginChild("CraftExportReview",new Vector2(0,Math.Max(100,ImGui.GetContentRegionAvail().Y-100)),false);
        foreach(var s in retrievalExportSteps){DrawRetrievalIcon(s.ItemId);ImGui.TextUnformatted($"{s.Name} · {s.Crafts} crafts → {s.Crafts*s.Yield} items");}
        if(ImGui.CollapsingHeader("Ingredients to gather / buy / withdraw"))foreach(var e in retrievalExportMaterials){DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted($"{e.Quantity} × {e.Name}");}
        if(retrievalExportSteps.Length==0&&retrievalExportError.Length==0)ImGui.TextWrapped("No craftable quantities remain. Non-craftable items are listed under ingredients.");
        if(retrievalExportError.Length>0)ImGui.TextWrapped(retrievalExportError);
        ImGui.EndChild();
        ImGui.BeginDisabled(retrievalExportError.Length>0||retrievalExportSteps.Length==0);
        if(ImGui.Button("Copy Artisan list")){ImGui.SetClipboardText(RetainerRetrieval.ExportArtisan(retrievalName,retrievalExportSteps,skipIfEnough:!retrievalExportMissing));retrievalExportNotice="Copied · import in Artisan's Lists tab, then review its settings.";}
        ImGui.SameLine();ImGui.BeginDisabled(!Pi.InstalledPlugins.Any(p=>p.InternalName=="Artisan"&&p.IsLoaded));if(ImGui.Button("Copy + open Artisan")){ImGui.SetClipboardText(RetainerRetrieval.ExportArtisan(retrievalName,retrievalExportSteps,skipIfEnough:!retrievalExportMissing));Commands.ProcessCommand("/artisan lists");retrievalExportNotice="Copied · choose Import List From Clipboard in Artisan.";}
        ImGui.EndDisabled();ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Close"))ImGui.CloseCurrentPopup();
        if(retrievalExportNotice.Length>0)ImGui.TextWrapped(retrievalExportNotice);
        ImGui.EndPopup();
    }
}
