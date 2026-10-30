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
    private bool assignmentRelieveFullHomes;
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
        ImGui.BeginDisabled(busy||assignmentRecommendationTask!=null);
        if(ImGui.Checkbox("Relieve full homes",ref assignmentRelieveFullHomes))assignmentRecommendation=null;
        ImGui.EndDisabled();
        MarketHelp("Off: fill missing assignments only. On: also suggest extra category homes for recorded stock that cannot fit its currently assigned homes. Existing rules stay; specific-item and saved-plan homes are not overridden. Review each proposed category before applying.");
        ImGui.BeginDisabled(busy||assignmentRecommendationTask!=null||assignmentCoverage?.CheckedRetainers!=owners.Length||owners.Length==0);
        if(ImGui.SmallButton("Recommended assignments")){
            var inventory=assignmentCoverageStock.ToDictionary(x=>x.Key,x=>x.Value.ToArray());
            var snapshot=AssignmentRecommendations.Copy(rows);var plans=config.RetrievalPresets.ToArray();
            var categories=inventory.Values.SelectMany(x=>x).Where(x=>x.Item>0).Select(x=>x.Item).Distinct().ToDictionary(x=>x,OrganizeCategory);
            assignmentRecommendationRevision=assignmentCoverageRevision;assignmentRecommendation=null;var relieve=assignmentRelieveFullHomes;
            assignmentRecommendationTask=Task.Run(()=>Enum.GetValues<AssignmentRecommendationStyle>().Select(style=>AssignmentRecommendations.Build(owners,inventory,snapshot,plans,id=>categories.GetValueOrDefault(id),style,relieve)).ToArray());
        }
        ImGui.EndDisabled();
        MarketHelp("Compare all recorded retainers. Fill missing category homes, optionally relieving full assigned homes, while preserving existing rules. Uses recorded quantities, stack limits, HQ/NQ and capacity; reserves existing incoming assignments, prefers five spare slots, and shares large categories across existing holders when needed. Crystals use separate per-item capacity. Review before applying. Does not decide item value or move items.");
        if(assignmentRecommendationTask!=null)ImGui.TextDisabled("Calculating suggested homes…");
        if(assignmentRecommendation!=null&&assignmentRecommendationOptions.Length>0){
            ImGui.SetNextItemWidth(220);
            if(ImGui.Combo("Priority",ref assignmentRecommendationChoice,"Fewer item moves\0More spare space\0Fewer destination retainers\0"))assignmentRecommendation=assignmentRecommendationOptions[assignmentRecommendationChoice];
        }
        if(assignmentRecommendation is {} proposal){
            string Category(uint id)=>StorageGroups().SelectMany(g=>g.Categories).FirstOrDefault(c=>c.Id==id)?.Name??("Category "+id);
            string Label(AssignmentSuggestion suggestion)=>suggestion.Item>0?
                (retrievalCatalog?.Names.GetValueOrDefault(suggestion.Item)??($"Item {suggestion.Item}")):Category(suggestion.Category);
            ImGui.TextWrapped($"After apply: {proposal.Remaining.Length} unassigned item types · {proposal.Suggestions.Length} additions · {proposal.Suggestions.Select(x=>x.Retainer).Distinct().Count()} retainers");
            MarketHelp("All recorded retainer storage is checked, including crystals, independently of the current transfer plan or filters. Shared categories have several allowed homes. Having a home does not guarantee every transfer can run immediately; Preview checks the movement plan and available staging space.");
            if(ImGui.TreeNode("Proposed homes")){
                ImGui.BeginChild("assignmentProposedHomes",new System.Numerics.Vector2(0,ImGui.GetTextLineHeightWithSpacing()*9),true);
                foreach(var group in proposal.Suggestions.GroupBy(x=>x.Retainer)){
                    var labels=string.Join(", ",group.Select(Label));
                    ImGui.TextWrapped(names.GetValueOrDefault(group.Key,group.Key.ToString())+" · "+labels);
                    if(ImGui.IsItemHovered()){
                        ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*34);
                        foreach(var suggestion in group)ImGui.TextWrapped(Label(suggestion)+
                            (suggestion.CapacityRelief?" · extra home":"")+$" · {suggestion.MovedStacks} stacks to relocate · +{suggestion.AdditionalSlots} normal slots");
                        ImGui.TextWrapped("Zero moves means existing stock can stay here. Crystal capacity is separate from normal inventory slots. Final free-space estimates are conservative; outgoing slots are not assumed free.");
                        ImGui.PopTextWrapPos();ImGui.EndTooltip();
                    }
                }
                ImGui.EndChild();ImGui.TreePop();
            }
            if(proposal.ExistingUnplacedSlots>0){
                ImGui.TextDisabled($"Initial capacity warnings: {proposal.ExistingUnplacedSlots} stacks");
                MarketHelp("Before additions, these assigned stacks did not fit their homes in the conservative estimate. Enable Relieve full homes to add category homes. Specific item/plan restrictions are preserved and may need manual changes. This is separate from the count of items with no assigned home.");
            }
            if(proposal.Remaining.Length>0){
                ImGui.TextWrapped("Incomplete proposal · some items still need homes.");
                if(ImGui.IsItemHovered())ImGui.SetTooltip(string.Join("\n",proposal.Remaining.Select(x=>(retrievalCatalog?.Names.GetValueOrDefault(x.Item)??x.Item.ToString())+$" ×{x.Quantity:N0}")));
            }
            ImGui.BeginDisabled(busy||proposal.Suggestions.Length==0);
            if(ImGui.SmallButton(proposal.Remaining.Length>0?"Apply partial additions":"Apply suggested additions")){
                BackupAssignmentSetup(character,"Before last recommended assignments");
                var applied=AssignmentRecommendations.Apply(rows,proposal.Suggestions);
                rows.Clear();rows.AddRange(applied);
                SaveConfiguration();InvalidateOrganizePreview();assignmentCoverageDirty=true;assignmentRecommendation=null;
                assignmentSetupNotice=$"Saved · {proposal.Remaining.Length} item types without a home. Undo available; no items moved.";
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
        if(ImGui.TreeNode("Restore / clear")){
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
        ImGui.TreePop();
        }
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
