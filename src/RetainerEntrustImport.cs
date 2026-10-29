using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint[] ReadAutoRetainerProtection()
    {
        return EntrustSelection.ReadProtectionDirectory(Pi.ConfigFile.DirectoryName!);
    }
    private void ImportRetrievalAsNewPlan()
    {
        var imported=RetainerRetrieval.Import(retrievalPaste,ResolveRetrievalItem,ReadAutoRetainerProtection);
        SaveNewRetrievalPlan(imported);
    }
    private void SaveNewRetrievalPlan(RetrievalPreset imported)
    {
        var current=new RetrievalPreset(retrievalName,retrievalItems.ToArray(),retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality,DestinationRetainer:retrievalSendTo,SendCategories:retrievalSendCategories.ToArray(),CategoryWithdrawQuantity:retrievalCategoryWithdraw,CategoryKeepQuantity:retrievalCategoryKeep,FilterPlanCategories:retrievalFilterCategories,Entrust:retrievalEntrust);
        var updated=EntrustSelection.AddImportedPlan(config.RetrievalPresets,current,retrievalEditingPreset,imported);
        config.RetrievalPresets=updated.ToList();SaveConfiguration();SelectRetrievalPreset(updated[^1]);
    }

    private void DrawImportedEntrust()
    {
        var plan=retrievalEntrust!;
        if(ImGui.SmallButton("Withdraw items…")){retrievalEntrustWithdrawal=true;retrievalPreviewDirty=true;}
        MarketHelp("Withdraw listed items allowed by the category filter. Set withdrawal quantities separately from Keep; your Entrust rules remain saved.");
        MarketHelp("Entrust all matching items from bags and crystals, minus Keep. HQ/NQ share the keep amount. No crafting expansion. Imported protection is a saved snapshot. Plans created here use your selected items; they do not read AutoRetainer protection.");
        ImGui.SameLine();if(ImGui.SmallButton("Entrust…"))ImGui.OpenPopup("ImportedEntrustOptions");
        if(ImGui.BeginPopup("ImportedEntrustOptions")){
            var q=(int)retrievalQuality;ImGui.SetNextItemWidth(160);if(ImGui.Combo("Quality",ref q,"Any quality\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)q;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
            DrawRetrievalSendControls(true);ImGui.EndPopup();
        }
        if(plan.Protected.Length>0){ImGui.TextDisabled($"{plan.Protected.Length} protected items");MarketHelp("Protected items are excluded from Entrust. The imported protection snapshot is unchanged by withdrawal.");}
        ImGui.BeginChild("EntrustSelectionItems",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);
        if(ImGui.BeginTable("EntrustSelection",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.ScrollY|ImGuiTableFlags.Resizable)){
            ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("Keep",ImGuiTableColumnFlags.WidthFixed,85);ImGui.TableSetupColumn("",ImGuiTableColumnFlags.WidthFixed,25);ImGui.TableHeadersRow();
            var rows=FilteredPlanItems();var rowHeight=Math.Max(ImGui.GetFrameHeight(),ImGui.GetTextLineHeight()+4)+ImGui.GetStyle().CellPadding.Y*2;var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length,rowHeight);while(clip.Step())for(var index=clip.DisplayStart;index<clip.DisplayEnd;index++){var e=rows[index];
                ImGui.PushID((int)e.ItemId);ImGui.TableNextRow(ImGuiTableRowFlags.None,rowHeight);ImGui.TableNextColumn();DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted(e.Name);
                if(plan.Protected.Contains(e.ItemId)){ImGui.SameLine();ImGui.TextDisabled("(protected)");}
                ImGui.TableNextColumn();var keep=plan.Keep.GetValueOrDefault(e.ItemId);ImGui.SetNextItemWidth(-1);
                if(ImGui.InputInt("##Keep",ref keep)){plan.Keep[e.ItemId]=Math.Clamp(keep,0,RetainerRetrieval.MaxQuantity);if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
                ImGui.TableNextColumn();if(ImGui.SmallButton("×")){retrievalItems=retrievalItems.Where(x=>x.ItemId!=e.ItemId).ToArray();retrievalSelected.Remove(e.ItemId);if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}ImGui.PopID();
            }clip.End();clip.Destroy();ImGui.EndTable();
        }ImGui.EndChild();
    }
}

