using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint[] ReadAutoRetainerProtection()
    {
        var path=Path.Combine(Pi.ConfigFile.DirectoryName!,"AutoRetainer.json");
        if(!File.Exists(path)||new FileInfo(path).Length>32*1024*1024)throw new IOException("AutoRetainer.json is unavailable or too large to read its protection list. Save AutoRetainer settings first.");
        // Copy the union of protection lists: a conservative snapshot, independent
        // of future AutoRetainer changes or the currently selected cleanup plan.
        return EntrustSelection.ReadProtection(File.ReadAllText(path));
    }
    private void DrawImportedEntrust()
    {
        var plan=retrievalEntrust!;
        ImGui.TextUnformatted($"Entrust selection · {retrievalItems.Length} items");
        MarketHelp("Entrust all matching items from bags and crystals, minus Keep. HQ/NQ share the keep amount. No crafting expansion. Protected items were copied from all saved AutoRetainer protection lists at import; reimport to refresh them.");
        var q=(int)retrievalQuality;if(ImGui.Combo("Quality",ref q,"Any quality\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)q;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
        DrawRetrievalSendControls();
        ImGui.TextDisabled($"{plan.Protected.Length} protected item IDs copied");
        ImGui.BeginChild("EntrustSelectionItems",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);
        if(ImGui.BeginTable("EntrustSelection",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.ScrollY|ImGuiTableFlags.Resizable)){
            ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("Keep",ImGuiTableColumnFlags.WidthFixed,85);ImGui.TableSetupColumn("",ImGuiTableColumnFlags.WidthFixed,25);ImGui.TableHeadersRow();
            var rows=retrievalItems;var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length);while(clip.Step())for(var index=clip.DisplayStart;index<clip.DisplayEnd;index++){var e=rows[index];
                ImGui.PushID((int)e.ItemId);ImGui.TableNextRow();ImGui.TableNextColumn();DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted(e.Name);
                if(plan.Protected.Contains(e.ItemId)){ImGui.SameLine();ImGui.TextDisabled("(protected)");}
                ImGui.TableNextColumn();var keep=plan.Keep.GetValueOrDefault(e.ItemId);ImGui.SetNextItemWidth(-1);
                if(ImGui.InputInt("##Keep",ref keep)){plan.Keep[e.ItemId]=Math.Clamp(keep,0,RetainerRetrieval.MaxQuantity);if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
                ImGui.TableNextColumn();if(ImGui.SmallButton("×")){retrievalItems=retrievalItems.Where(x=>x.ItemId!=e.ItemId).ToArray();retrievalSelected.Remove(e.ItemId);if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}ImGui.PopID();
            }clip.End();clip.Destroy();ImGui.EndTable();
        }ImGui.EndChild();
    }
}
