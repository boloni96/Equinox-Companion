using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool duplicateExclusionsOpen;
    private string duplicateExclusionSearch="",duplicateExclusionLastSearch="";
    private KeyValuePair<uint,string>[] duplicateExclusionResults=[];
    private void DrawDuplicateExclusionsButton(string scope)
    {
        ImGui.PushID(scope);
        if(ImGui.Button("...##DuplicateExclusions")){duplicateExclusionsOpen=true;retrievalPickLast=0;retrievalPickAction=0;LoadRetrievalCatalog();duplicateExclusionLastSearch="\0";}
        MarketHelp($"Duplicate exclusions · {config.DuplicateDepositExclusions.Count} items. Shared by saddlebag and FC chest duplicate deposits.");
        ImGui.PopID();
    }
    private void DrawDuplicateExclusionsWindow()
    {
        if(!duplicateExclusionsOpen)return;
        ImGui.SetNextWindowSize(new(410,420),ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new System.Numerics.Vector2(10,8));
        if(ImGui.Begin("Duplicate exclusions",ref duplicateExclusionsOpen)){
            ImGui.TextUnformatted("Duplicate exclusions");
            ImGui.TextWrapped("Shared by all Add duplicates / Deposit duplicates buttons. Excludes both HQ and NQ copies. Manual Entrust and Withdraw are unchanged.");
            ImGui.TextDisabled("Hover an item: Shift adds · Alt removes");
            ImGui.Separator();
            if(retrievalCatalogTask?.IsCompleted==true){try{retrievalCatalog=retrievalCatalogTask.GetAwaiter().GetResult();}catch(Exception e){retrievalError=e.Message;}retrievalCatalogTask=null;}
            if(duplicateExclusionsOpen)TickRetrievalPicker();
            var busy=chestRunning||organizeRunning||retrievalRun.Running;
            if(busy)ImGui.TextDisabled("Stop the current transfer to edit exclusions.");
            ImGui.BeginDisabled(busy);
            ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##Search","Search item name or ID to exclude…",ref duplicateExclusionSearch,100);
            if(duplicateExclusionSearch.Trim().Length>0){
                if(retrievalCatalog==null){ImGui.TextDisabled("Loading item catalogue…");if(retrievalCatalogTask==null&&ImGui.SmallButton("Retry"))LoadRetrievalCatalog();}
                else{
                    if(duplicateExclusionLastSearch!=duplicateExclusionSearch){duplicateExclusionLastSearch=duplicateExclusionSearch;var query=duplicateExclusionSearch.Trim();duplicateExclusionResults=retrievalCatalog.Names.Where(x=>x.Key>0&&(x.Value.Contains(query,StringComparison.OrdinalIgnoreCase)||x.Key.ToString()==query)).OrderBy(x=>x.Value).Take(61).ToArray();}
                    ImGui.TextDisabled("Check an item to exclude it.");
                    ImGui.BeginChild("Matches",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-26)),false);
                    foreach(var item in duplicateExclusionResults.Take(60))DrawDuplicateExclusionItem(item.Key,item.Value);
                    if(duplicateExclusionResults.Length==0)ImGui.TextDisabled("No matching items.");
                    if(duplicateExclusionResults.Length>60)ImGui.TextDisabled("Narrow the search for more results.");
                    ImGui.EndChild();
                }
            }else{
                ImGui.TextUnformatted($"Excluded items · {config.DuplicateDepositExclusions.Count}");
                ImGui.BeginChild("Excluded",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-26)),false);
                foreach(var id in config.DuplicateDepositExclusions.OrderBy(SearchItemName).ToArray())DrawDuplicateExclusionItem(id,SearchItemName(id));
                if(config.DuplicateDepositExclusions.Count==0)ImGui.TextDisabled("No exclusions. Search above to add items.");
                ImGui.EndChild();
            }
            ImGui.EndDisabled();
            if(ImGui.SmallButton("Close"))duplicateExclusionsOpen=false;
        }
        ImGui.End();ImGui.PopStyleVar();
    }
    private void DrawDuplicateExclusionItem(uint id,string name)
    {
        var excluded=config.DuplicateDepositExclusions.Contains(id);
        if(ImGui.Checkbox(name+"##exclude-"+id,ref excluded)){
            if(excluded)config.DuplicateDepositExclusions.Add(id);else config.DuplicateDepositExclusions.Remove(id);
            SaveConfiguration();
        }
    }
}
