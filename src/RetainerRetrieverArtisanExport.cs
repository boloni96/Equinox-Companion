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
            var outputs=RetainerRetrieval.Requested(retrievalItems,retrievalMultiplier,retrievalIncludesSubcrafts,retrievalCatalog.Recipes);
            outputs=RetainerRetrieval.WorkshopTurnIns(outputs,retrievalCatalog.WorkshopByItem);
            if(retrievalExportMissing)
            {
                RefreshRetrievalStock();if(!retrievalStockComplete)throw new InvalidOperationException("Some retainer stock is unknown. Refresh inventory after visiting your retainers, or choose all requested outputs.");
                outputs=outputs.Select(e=>e with{Quantity=Math.Max(0,e.Quantity-RetrievalHeld(e.ItemId)-retrievalStock.Where(s=>s.Item==e.ItemId).Sum(s=>s.Quantity))}).Where(e=>e.Quantity>0).ToArray();
            }
            var plan=RetainerRetrieval.PlanCrafts(outputs,retrievalCatalog.Recipes);retrievalExportSteps=plan.Steps;retrievalExportMaterials=plan.Materials;
        }
        catch(Exception e){retrievalExportError=e.Message;}
    }
    private void DrawRetrievalArtisanExport()
    {
        ImGui.SetNextWindowSize(new Vector2(600,450),ImGuiCond.FirstUseEver);
        if(!ImGui.BeginPopupModal("Prepare Artisan crafting list"))return;
        if(ImGui.Checkbox("Only missing outputs (bags + recorded retainers)",ref retrievalExportMissing))BuildRetrievalArtisanExport();
        ImGui.TextWrapped("Review the crafting order below. Copy, then use Artisan → Lists → Import List From Clipboard (Artisan Export). No crafting starts automatically.");
        ImGui.BeginChild("CraftExportReview",new Vector2(0,Math.Max(100,ImGui.GetContentRegionAvail().Y-100)),false);
        foreach(var s in retrievalExportSteps){DrawRetrievalIcon(s.ItemId);ImGui.TextUnformatted($"{s.Name} · {s.Crafts} crafts → {s.Crafts*s.Yield} items");}
        if(ImGui.CollapsingHeader("Ingredients to gather / buy / retrieve"))foreach(var e in retrievalExportMaterials){DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted($"{e.Quantity} × {e.Name}");}
        if(retrievalExportSteps.Length==0&&retrievalExportError.Length==0)ImGui.TextWrapped("No craftable quantities remain. Non-craftable items are listed under ingredients.");
        if(retrievalExportError.Length>0)ImGui.TextWrapped(retrievalExportError);
        ImGui.EndChild();
        ImGui.BeginDisabled(retrievalExportError.Length>0||retrievalExportSteps.Length==0);
        if(ImGui.Button("Copy Artisan list")){ImGui.SetClipboardText(RetainerRetrieval.ExportArtisan(retrievalName,retrievalExportSteps));retrievalExportNotice="Copied · import in Artisan's Lists tab, then review its settings.";}
        ImGui.SameLine();ImGui.BeginDisabled(!Pi.InstalledPlugins.Any(p=>p.InternalName=="Artisan"&&p.IsLoaded));if(ImGui.Button("Copy + open Artisan")){ImGui.SetClipboardText(RetainerRetrieval.ExportArtisan(retrievalName,retrievalExportSteps));Commands.ProcessCommand("/artisan lists");retrievalExportNotice="Copied · choose Import List From Clipboard in Artisan.";}
        ImGui.EndDisabled();ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Close"))ImGui.CloseCurrentPopup();
        if(retrievalExportNotice.Length>0)ImGui.TextWrapped(retrievalExportNotice);
        ImGui.EndPopup();
    }
}
