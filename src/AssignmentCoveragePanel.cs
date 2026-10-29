using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private AssignmentCoverageResult? assignmentCoverage;
    private ulong assignmentCoverageCharacter;
    private bool assignmentCoverageDirty=true;
    private string assignmentCoverageDetails="";
    private Dictionary<ulong,ChestStack[]> assignmentCoverageStock=[];
    private int assignmentCoverageRevision;
    private unsafe void DrawAssignmentCoverage(ulong[] owners,Dictionary<ulong,string> names,List<RetainerAssignment> rows)
    {
        if(ImGui.SmallButton("Refresh assignment check"))assignmentCoverageDirty=true;
        if(ImGui.IsWindowAppearing()||assignmentCoverageCharacter!=Player.ContentId)assignmentCoverageDirty=true;
        if(assignmentCoverageDirty){
            var stock=RecordedOrganizerInventories(owners,false);
            assignmentCoverageStock=stock;assignmentCoverageRevision++;assignmentRecommendation=null;
            assignmentCoverage=AssignmentCoverage.Check(owners,stock,rows,config.RetrievalPresets,OrganizeCategory);
            assignmentCoverageCharacter=Player.ContentId;assignmentCoverageDirty=false;
            var categoryNames=StorageGroups().SelectMany(g=>g.Categories).ToDictionary(c=>c.Id,c=>c.Name);
            var lines=new List<string>{"No assigned home · recorded retainer storage only",
                "Counts ignore the current transfer plan and filters. HQ/NQ share one item type; slots count each occupied stack.",
                "Specific items and saved-plan items count as assigned. Missing inventories are unknown, not empty."};
            foreach(var group in assignmentCoverage.Missing.GroupBy(x=>x.Category)){
                lines.Add("\n"+categoryNames.GetValueOrDefault(group.Key,"Category "+group.Key)+$" · {group.Count()} item types · {group.Sum(x=>x.Slots)} slots");
                foreach(var item in group)lines.Add("  "+(retrievalCatalog?.Names.GetValueOrDefault(item.Item)??item.Item.ToString())+$" ×{item.Quantity:N0} · "+string.Join(", ",item.Retainers.Select(id=>names.GetValueOrDefault(id,id.ToString()))));
            }
            if(assignmentCoverage.Missing.Length==0)lines.Add("\nNo unassigned items in the checked records.");
            assignmentCoverageDetails=string.Join("\n",lines);
        }
        var check=assignmentCoverage!;
        ImGui.TextWrapped($"{check.Missing.Select(x=>x.Category).Distinct().Count()} categories with unassigned items · {check.Missing.Length} item types · {check.Missing.Sum(x=>x.Slots)} slots");
        if(ImGui.IsItemHovered()){
            ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*38);
            var groups=check.Missing.GroupBy(x=>x.Category).ToArray();
            ImGui.TextUnformatted("No assigned home:");
            foreach(var group in groups){
                var label=StorageGroups().SelectMany(g=>g.Categories).FirstOrDefault(c=>c.Id==group.Key)?.Name??("Category "+group.Key);
                ImGui.TextUnformatted(label+$" · {group.Count()} item types · {group.Sum(x=>x.Slots)} slots");
                ImGui.TextWrapped(string.Join(", ",group.Take(4).Select(x=>retrievalCatalog?.Names.GetValueOrDefault(x.Item)??x.Item.ToString()))+(group.Count()>4?" …":""));
            }
            ImGui.TextWrapped("Expand Missing assignment details below for every item and its retainer. Based on recorded inventories; Refresh rechecks without moving items.");
            ImGui.PopTextWrapPos();ImGui.EndTooltip();
        }
        ImGui.TextDisabled($"Recorded inventories checked: {check.CheckedRetainers}/{check.TotalRetainers} · {check.MisplacedSlots} slots outside assigned homes");
        if(check.CheckedRetainers<check.TotalRetainers)ImGui.TextWrapped("Incomplete check: open the bell and missing retainer inventories, then Refresh.");
        if(!config.UseRetainerAssignments.Contains(Player.ContentId))ImGui.TextDisabled("Assignments are currently disabled.");
        if(ImGui.TreeNode("Missing assignment details")){
            ImGui.TextWrapped(assignmentCoverageDetails);ImGui.TreePop();
        }
        DrawAssignmentSetups(owners,names,rows);
        ImGui.Separator();
    }
}
