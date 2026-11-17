using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private PreparationPlan? craftingPreparation;
    private RetrievalPreset? preparationRestore;
    private bool craftingMissingFirst=true,craftingHideCovered;
    private string craftingPreparationError="";
    private string craftingPreparationKey="";
    private DateTimeOffset craftingPreparationAt;
    private RetrievalEntry[] CraftingOutputs()
    {
        if(retrievalCatalog==null)return [];
        var outputs=RetainerRetrieval.Requested(retrievalItems,retrievalMultiplier,retrievalIncludesSubcrafts,RetrievalRecipes());
        return RetainerRetrieval.WorkshopTurnIns(RetainerRetrieval.WorkshopTurnIns(outputs,retrievalCatalog.Outfits),retrievalCatalog.WorkshopByItem);
    }
    private PreparationPlan BuildPreparation(bool stock)
    {
        var bags=stock?RetrievalBagTotals():new Dictionary<uint,int>();
        var retainers=stock?retrievalStock.GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Quantity)):new Dictionary<uint,int>();
        return CraftingPreparation.Build(CraftingOutputs(),RetrievalRecipes(),bags,retainers,!stock||retrievalStockComplete);
    }
    private void RefreshPreparation()
    {
        craftingPreparation=null;craftingPreparationError="";
        try{RefreshRetrievalStock();craftingPreparation=BuildPreparation(true);}
        catch(RetrievalRecipeChoiceException e){retrievalRecipeChoice=e.ItemId;craftingPreparationError=e.Message+" Choose its recipe in Transfer.";}
        catch(Exception e){craftingPreparationError=e.Message;}
        craftingPreparationAt=DateTimeOffset.UtcNow;
    }
    private void WithdrawPreparation(RetrievalEntry[] entries,RetrievalQuality quality=RetrievalQuality.Any)
    {
        if(entries.Length==0)return;
        preparationRestore??=new(retrievalName,retrievalItems.ToArray(),retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality);
        // Work as a temporary list. Saved presets remain unchanged.
        retrievalEditingPreset=null;retrievalEntrust=null;retrievalEntrustWithdrawal=false;
        retrievalItems=RetainerRetrieval.Combine(entries);retrievalMultiplier=1;retrievalIncludesSubcrafts=false;
        retrievalMode=RetrievalMode.CompletedItems;retrievalQuality=quality;retrievalSubtract=false;retrievalTransferAll=false;
        retrievalFilterCategories=false;retrievalSendCategories.Clear();
        retrievalPreviewDirty=true;retrievalShowList=true;StartRetrieval(false);
    }
    private void DrawCraftingPreparation()
    {
        if(preparationRestore!=null&&!retrievalRun.Running&&!craftingWithdrawalPending&&ImGui.Button("Restore crafting source list")){SelectRetrievalPreset(preparationRestore);preparationRestore=null;craftingPreparationKey="";}
        ImGui.TextWrapped("Choose a saved list above, or add any crafting-log items in Plans. Quantities include the list multiplier. Existing materials are allocated once across the whole list.");
        var key=Player.ContentId+"|"+retrievalMultiplier+"|"+string.Join(';',retrievalItems.Select(e=>$"{e.ItemId}:{e.Quantity}:{e.RecipeId}"));
        if(key!=craftingPreparationKey){craftingPreparationKey=key;RefreshPreparation();}
        if(ImGui.Button("Refresh materials"))RefreshPreparation();
        ImGui.SameLine();var colors=config.CraftingColors;if(ImGui.Checkbox("Colours",ref colors)){config.CraftingColors=colors;SaveConfiguration();}
        ImGui.SameLine();ImGui.Checkbox("Missing first",ref craftingMissingFirst);ImGui.SameLine();ImGui.Checkbox("Hide covered",ref craftingHideCovered);
        ImGui.TextDisabled("Snapshot: "+craftingPreparationAt.ToLocalTime().ToString("HH:mm:ss")+" · NQ + HQ for crafting");
        ImGui.TextWrapped(retrievalStockNotice);
        if(craftingPreparationError.Length>0)ImGui.TextWrapped(craftingPreparationError);
        if(craftingPreparation is not {} p)return;
        ImGui.TextWrapped("Green: in bags · Gold: withdraw · Blue: craftable · Red: missing · Grey: stock incomplete. Hover an item for what uses it. Workshop turn-ins also require whole NQ or HQ sets; use FC workshop for that review.");
        ImGui.BeginDisabled(retrievalRun.Running||craftingWithdrawalPending||workshopRunning||organizeRunning||chestRunning||gearRouting||armoireRunning);
        if(ImGui.Button("Withdraw allocated stock"))RetrievalTry(()=>WithdrawPreparation(p.Withdraw));
        ImGui.SameLine();if(ImGui.Button("Prepare Artisan list…"))PrepareRetrievalArtisanExport();
        ImGui.SameLine();if(ImGui.Button("Copy missing ingredients"))ImGui.SetClipboardText(string.Join('\n',p.Missing.Select(e=>$"{e.Quantity} x {e.Name}")));
        ImGui.EndDisabled();DrawRetrievalArtisanExport();
        ImGui.TextDisabled($"{p.Steps.Length} recipes to craft · {p.Withdraw.Length} items to withdraw · {p.Missing.Length} ingredient shortages");
        if(!ImGui.BeginTable("Preparation",6,ImGuiTableFlags.BordersInnerV|ImGuiTableFlags.RowBg|ImGuiTableFlags.ScrollY,new Vector2(0,Math.Max(100,ImGui.GetContentRegionAvail().Y))))return;
        foreach(var title in new[]{"Item / use","Required","Bags used","Retainers used","To craft","Still missing"})ImGui.TableSetupColumn(title);
        ImGui.TableSetupScrollFreeze(0,1);ImGui.TableHeadersRow();
        IEnumerable<PreparationRow> rows=p.Rows;
        if(craftingHideCovered)rows=rows.Where(r=>r.Coverage is not PreparationCoverage.Bags);
        if(craftingMissingFirst)rows=rows.OrderByDescending(r=>r.Coverage is PreparationCoverage.Missing or PreparationCoverage.Incomplete).ThenByDescending(r=>r.Missing).ThenBy(r=>r.Name);
        foreach(var r in rows){
            ImGui.TableNextRow();
            if(config.CraftingColors){var c=r.Coverage switch{PreparationCoverage.Bags=>new Vector4(.08f,.5f,.16f,.65f),PreparationCoverage.Retainers=>new Vector4(.55f,.4f,.04f,.65f),PreparationCoverage.Craftable=>new Vector4(.05f,.3f,.6f,.65f),PreparationCoverage.Missing=>new Vector4(.6f,.12f,.1f,.65f),_=>new Vector4(.3f,.3f,.3f,.65f)};ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,ImGui.ColorConvertFloat4ToU32(c));}
            ImGui.TableNextColumn();ImGui.TextUnformatted(r.Name);if(ImGui.IsItemHovered())ImGui.SetTooltip("Used for: "+string.Join(", ",r.UsedFor)+"\n"+r.Coverage);
            ImGui.TableNextColumn();ImGui.TextUnformatted(r.Required.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.Bags.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.Retainers.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.CraftQuantity.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.Missing>0&&!r.Complete?$"≤ {r.Missing} · verify":r.Missing.ToString());
        }
        ImGui.EndTable();
    }
}
