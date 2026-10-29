using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string assignmentSetupName="",assignmentSetupNotice="";
    private Task<AssignmentRecommendation[]>? assignmentRecommendationTask;
    private AssignmentRecommendation? assignmentRecommendation;
    private int assignmentRecommendationRevision;
    private AssignmentRecommendation[] assignmentRecommendationOptions=[];
    private int assignmentRecommendationChoice;
    private void BackupAssignmentSetup(ulong character,string name)
    {
        if(!config.RetainerAssignmentSetups.TryGetValue(character,out var saved))config.RetainerAssignmentSetups[character]=saved=[];
        saved.RemoveAll(x=>x.Name==name);
        saved.Add(new(name,AssignmentRecommendations.Copy(config.RetainerAssignments.GetValueOrDefault(character)??[]),config.UseRetainerAssignments.Contains(character)));
    }
    private void DrawAssignmentSetups(ulong[] owners,Dictionary<ulong,string> names,List<RetainerAssignment> rows)
    {
        var character=Player.ContentId;
        if(assignmentRecommendationTask?.IsCompleted==true){
            try{var result=assignmentRecommendationTask.GetAwaiter().GetResult();if(assignmentRecommendationRevision==assignmentCoverageRevision){assignmentRecommendationOptions=result;assignmentRecommendationChoice=0;assignmentRecommendation=result[0];}}
            catch(Exception e){assignmentSetupNotice="Recommendation failed: "+e.Message;}
            assignmentRecommendationTask=null;
        }
        var busy=organizeRunning||retrievalRun.Running||chestRunning;
        ImGui.BeginDisabled(busy||assignmentRecommendationTask!=null||assignmentCoverage?.CheckedRetainers!=owners.Length||owners.Length==0);
        if(ImGui.SmallButton("Recommended assignments")){
            var inventory=assignmentCoverageStock.ToDictionary(x=>x.Key,x=>x.Value.ToArray());
            var snapshot=AssignmentRecommendations.Copy(rows);var plans=config.RetrievalPresets.ToArray();
            var categories=inventory.Values.SelectMany(x=>x).Where(x=>x.Item>0).Select(x=>x.Item).Distinct().ToDictionary(x=>x,OrganizeCategory);
            assignmentRecommendationRevision=assignmentCoverageRevision;assignmentRecommendation=null;
            assignmentRecommendationTask=Task.Run(()=>Enum.GetValues<AssignmentRecommendationStyle>().Select(style=>AssignmentRecommendations.Build(owners,inventory,snapshot,plans,id=>categories.GetValueOrDefault(id),style)).ToArray());
        }
        ImGui.EndDisabled();
        MarketHelp("Suggest homes for currently unassigned categories, preserving existing assignments. Uses recorded quantities, stack limits, HQ/NQ and capacity; reserves room for existing incoming assignments and aims to leave five spare slots. Review before applying. Does not decide item value or move items.");
        if(assignmentRecommendationTask!=null)ImGui.TextDisabled("Calculating suggested homes…");
        if(assignmentRecommendation!=null&&assignmentRecommendationOptions.Length>0){
            ImGui.SetNextItemWidth(220);
            if(ImGui.Combo("Priority",ref assignmentRecommendationChoice,"Fewer item moves\0More spare space\0Fewer destination retainers\0"))assignmentRecommendation=assignmentRecommendationOptions[assignmentRecommendationChoice];
        }
        if(assignmentRecommendation is {} proposal){
            string Category(uint id)=>StorageGroups().SelectMany(g=>g.Categories).FirstOrDefault(c=>c.Id==id)?.Name??("Category "+id);
            ImGui.TextWrapped("Suggested additions · current assignments stay in place");
            foreach(var suggestion in proposal.Suggestions)ImGui.TextWrapped(Category(suggestion.Category)+" → "+names.GetValueOrDefault(suggestion.Retainer,suggestion.Retainer.ToString())+$" · +{suggestion.AdditionalSlots} slots · {suggestion.FreeAfter} free after this addition");
            if(proposal.ExistingUnplacedSlots>0)ImGui.TextWrapped($"{proposal.ExistingUnplacedSlots} existing assigned stacks could not be reserved in the conservative capacity estimate. Review space before organizing.");
            if(proposal.NeedsSpace.Length>0)ImGui.TextWrapped("Needs manual review / split homes: "+string.Join(", ",proposal.NeedsSpace.Select(Category)));
            ImGui.TextDisabled("Estimates use recorded stock; actual transfers recheck space.");
            ImGui.BeginDisabled(busy||proposal.Suggestions.Length==0);
            if(ImGui.SmallButton("Apply suggested additions")){
                BackupAssignmentSetup(character,"Before last recommended assignments");
                foreach(var suggestion in proposal.Suggestions){
                    var index=rows.FindIndex(x=>x.Retainer==suggestion.Retainer);
                    if(index<0)rows.Add(new(suggestion.Retainer,[suggestion.Category],[]));
                    else rows[index]=rows[index] with{Categories=rows[index].Categories.Append(suggestion.Category).Distinct().Order().ToArray()};
                }
                SaveConfiguration();InvalidateOrganizePreview();assignmentCoverageDirty=true;assignmentRecommendation=null;
                assignmentSetupNotice="Applied additions. Previous assignments saved; no items moved.";
            }
            ImGui.EndDisabled();
        }
        void Restore(AssignmentSetup setup,string backup,string message){
            var copy=AssignmentRecommendations.Copy(setup.Assignments);
            BackupAssignmentSetup(character,backup);
            rows.Clear();rows.AddRange(copy);
            if(setup.Enabled)config.UseRetainerAssignments.Add(character);else config.UseRetainerAssignments.Remove(character);
            SaveConfiguration();InvalidateOrganizePreview();assignmentCoverageDirty=true;assignmentRecommendation=null;
            assignmentSetupNotice=message;
        }
        var undo=config.RetainerAssignmentSetups.GetValueOrDefault(character)?.FirstOrDefault(s=>s.Name=="Before last recommended assignments");
        ImGui.BeginDisabled(busy);
        if(undo!=null&&ImGui.SmallButton("Undo recommendation"))Restore(undo,"Before undo recommendation","Restored previous assignments. Already-moved items stay where they are.");
        ImGui.BeginDisabled(!rows.Any(a=>a.Categories.Length>0||a.Items.Length>0||(a.Plans?.Length??0)>0));
        if(ImGui.SmallButton("Clear assignments")){
            BackupAssignmentSetup(character,"Before last clear assignments");
            rows.Clear();foreach(var owner in owners)rows.Add(new(owner,[],[]));
            SaveConfiguration();InvalidateOrganizePreview();assignmentCoverageDirty=true;assignmentRecommendation=null;
            assignmentSetupNotice="Cleared this character's assignment rules; backup saved. No items moved.";
        }
        ImGui.EndDisabled();
        var undoClear=config.RetainerAssignmentSetups.GetValueOrDefault(character)?.FirstOrDefault(s=>s.Name=="Before last clear assignments");
        if(undoClear!=null){ImGui.SameLine();if(ImGui.SmallButton("Undo clear"))Restore(undoClear,"Before undo clear assignments","Restored assignments from before Clear.");}
        ImGui.EndDisabled();
        if(ImGui.TreeNode("Save / load assignments")){
            ImGui.SetNextItemWidth(220);ImGui.InputText("Setup name",ref assignmentSetupName,80);
            var name=assignmentSetupName.Trim();
            ImGui.BeginDisabled(name.Length==0||busy);
            if(ImGui.SmallButton("Save / replace named setup")){
                BackupAssignmentSetup(character,name);SaveConfiguration();assignmentSetupNotice="Saved "+name+" for this character.";
            }
            ImGui.EndDisabled();
            if(config.RetainerAssignmentSetups.TryGetValue(character,out var saved)){
                foreach(var setup in saved.ToArray()){
                    ImGui.PushID(setup.Name);ImGui.BeginDisabled(busy);
                    if(ImGui.SmallButton("Load")){
                        var copy=AssignmentRecommendations.Copy(setup.Assignments);
                        BackupAssignmentSetup(character,"Before last loaded assignments");
                        rows.Clear();rows.AddRange(copy);
                        if(setup.Enabled)config.UseRetainerAssignments.Add(character);else config.UseRetainerAssignments.Remove(character);
                        SaveConfiguration();InvalidateOrganizePreview();assignmentCoverageDirty=true;assignmentRecommendation=null;
                        assignmentSetupNotice="Loaded "+setup.Name+"; previous setup backed up. No items moved.";
                    }
                    ImGui.EndDisabled();ImGui.SameLine();ImGui.TextUnformatted(setup.Name);ImGui.PopID();
                }
            }
            ImGui.TextWrapped("Saved per character, including categories, specific items, linked plan IDs and Use assignments. Linked plans keep following their current item lists.");
            ImGui.TreePop();
        }
        if(assignmentSetupNotice.Length>0)ImGui.TextWrapped(assignmentSetupNotice);
    }
}
