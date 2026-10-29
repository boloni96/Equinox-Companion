using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool DrawEntrustDestination(string label)
    {
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        var assigned=config.UseRetainerAssignments.Contains(Player.ContentId);
        var all=retrievalSendTo==StorageAutoRouting.AllRetainers;var changed=false;
        if(ImGui.BeginCombo(label,all?(assigned?"Assignments":"All retainers"):names.GetValueOrDefault(retrievalSendTo,"Choose destination"))){
            if(ImGui.Selectable("Assignments · this character",all&&assigned)){
                retrievalSendTo=StorageAutoRouting.AllRetainers;config.UseRetainerAssignments.Add(Player.ContentId);changed=true;
            }
            if(ImGui.Selectable("All retainers",all&&!assigned)){
                retrievalSendTo=StorageAutoRouting.AllRetainers;config.UseRetainerAssignments.Remove(Player.ContentId);changed=true;
            }
            foreach(var row in names)if(ImGui.Selectable(row.Value+"##send"+row.Key,retrievalSendTo==row.Key)){retrievalSendTo=row.Key;changed=true;}
            ImGui.EndCombo();
        }
        MarketHelp("Assignments uses this character's saved item, plan and category homes, leaving unassigned items in bags. Shared with Use assignments in Organise retainers. A named retainer is an explicit destination. Keep and protection rules still apply.");
        if(changed){SaveConfiguration();InvalidateOrganizePreview();if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
        return changed;
    }
    private void DrawRetrievalSendControls(bool inline=false)
    {
        if(!inline&&!ImGui.CollapsingHeader("Entrust this plan"))return;
        ImGui.SetNextItemWidth(200);DrawEntrustDestination("To retainer");ImGui.SameLine();DrawAssignmentEditor();
        ImGui.SetNextItemWidth(200);if(DrawStorageCategoryPicker("Entrust categories",retrievalSendCategories,"None · listed items only")&&retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Entrust all matching items in your bags, plus the listed quantities. Categories affect entrusting only; withdraw uses the explicit item list. Save preset remembers the destination and categories.");
        if(ImGui.Button("Entrust plan"))SendRetrievalList(false);if(retrievalEntrust==null){ImGui.SameLine();if(ImGui.Button("Entrust duplicates"))SendRetrievalList(true);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Entrust bag items already stored on this destination retainer. Quality selection applies. No items are discarded or sold.");}
    }
}
