using Dalamud.Bindings.ImGui;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private RetrievalPreset? craftingWithdrawalPrevious;
    private string? craftingWithdrawalPreviousEditing;
    private bool craftingWithdrawalPending, craftingWithdrawalPrepared, craftingWithdrawalSelected, craftingWithdrawalRequireRecipe;
    private ulong craftingWithdrawalCharacter;
    private uint craftingWithdrawalWorld;
    private DateTimeOffset craftingWithdrawalDeadline, craftingWithdrawalBellAt;

    private void OpenCraftingWithdrawal(uint itemId, bool rawMaterials)
    {
        if (!RequireOptionalTool("Equinox Organizer")) return;
        if (craftingWithdrawalPending || retrievalRun.Running || organizeRunning || chestRunning || retainerGilRunning)
        {
            PrintEquinox("Finish or stop the current storage operation before choosing another recipe.");
            return;
        }
        // Opening a recipe must not edit the user's saved Entrust/crafting plan.
        // Keep the first draft until explicitly restored, even after another click.
        if (craftingWithdrawalPrevious == null)
        {
            craftingWithdrawalPrevious = new(retrievalName, retrievalItems.ToArray(),
                retrievalMultiplier, retrievalIncludesSubcrafts, Mode: retrievalMode,
                RecipeChoices: new(retrievalRecipeOverrides), Quality: retrievalQuality,
                DestinationRetainer: retrievalSendTo, SendCategories: retrievalSendCategories.ToArray(),
                Entrust: retrievalEntrust);
            craftingWithdrawalPreviousEditing = retrievalEditingPreset;
        }
        SelectRetrievalPreset(CraftingWithdrawal.Create(itemId, SearchItemName(itemId), rawMaterials));
        retrievalEditingPreset = null;
        retrievalError = "";
        retrievalExportSteps = [];
        OpenRetriever();
        retrievalSubtract = true;
        QueueCraftingWithdrawal(false, true);
    }

    private void QueueCraftingWithdrawal(bool selected, bool requireRecipe = false)
    {
        craftingWithdrawalPending = true; craftingWithdrawalPrepared = false;
        craftingWithdrawalSelected = selected; craftingWithdrawalRequireRecipe = requireRecipe;
        craftingWithdrawalCharacter = Player.ContentId; craftingWithdrawalWorld = Player.CurrentWorld.RowId;
        craftingWithdrawalDeadline = DateTimeOffset.UtcNow.AddSeconds(30); craftingWithdrawalBellAt = default;
    }

    private void CancelCraftingWithdrawal(string message)
    {
        if (!craftingWithdrawalPending) return;
        craftingWithdrawalPending = false;
        retrievalPort.Release();
        if (message.Length > 0) { retrievalError = message; PrintEquinox(message); }
    }

    private unsafe void TickCraftingWithdrawal(DateTimeOffset now)
    {
        if (!craftingWithdrawalPending) return;
        try
        {
            if (!config.EnableRetriever || !retrieverWindow.IsOpen || !Player.IsLoaded || Player.ContentId != craftingWithdrawalCharacter || Player.CurrentWorld.RowId != craftingWithdrawalWorld || Player.HomeWorld.RowId != craftingWithdrawalWorld || Conditions[ConditionFlag.InCombat] || Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51] || HelperInCutscene() || sellerRun.Running || gilTrade.Running || organizeRunning || chestRunning || retainerGilRunning)
                throw new InvalidOperationException("Withdrawal cancelled: character, world or active operation changed. Use a bell on your home world.");
            if (now >= craftingWithdrawalDeadline) throw new InvalidOperationException("Could not open the summoning bell in time. Move within reach and try Withdraw again.");
            if (retrievalCatalogTask?.IsCompleted == true) { retrievalCatalog = retrievalCatalogTask.GetAwaiter().GetResult(); retrievalCatalogTask = null; }
            if (retrievalCatalog == null) return;
            if (!craftingWithdrawalPrepared)
            {
                if (craftingWithdrawalRequireRecipe && retrievalItems.Any(x => !retrievalCatalog.Recipes.ContainsKey(x.ItemId) && !retrievalCatalog.WorkshopByItem.ContainsKey(x.ItemId)))
                    throw new InvalidOperationException("No crafting recipe is available for this item. Select the recipe result in the crafting log.");
                PreviewRetrieval(craftingWithdrawalSelected);
                if (retrievalPlan.Length == 0) throw new InvalidOperationException("Choose at least one item.");
                if (retrievalPreview.All(x => x.Need == 0)) { CancelCraftingWithdrawal("You already have the required materials in your bags."); return; }
                craftingWithdrawalPrepared = true;
            }
            if (!retrievalPort.Prepare()) return;
            if (SellerAddon("RetainerList") != null && RetainerToolsContext())
            {
                craftingWithdrawalPending = false;
                StartRetrieval(craftingWithdrawalSelected);
                if (!retrievalRun.Running) retrievalPort.Release();
                return;
            }
            if (craftingWithdrawalBellAt != default) return; // Interact once; don't reopen after Cancel.
            var player = Objects.LocalPlayer;
            if (player == null) throw new InvalidOperationException("Your character is not ready.");
            var bell = Objects.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj && x.IsTargetable && Vector3.Distance(x.Position, player.Position) <= 4.5f &&
                string.Equals(DataManager.GetExcelSheet<Lumina.Excel.Sheets.EObjName>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(x.BaseId)?.Singular.ToString(), "summoning bell", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => Vector3.DistanceSquared(x.Position, player.Position)).FirstOrDefault();
            if (bell == null) throw new InvalidOperationException("No summoning bell within reach. Move closer, then click Withdraw again.");
            CloseSellerAddon("RecipeNote");
            TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)bell.Address, false);
            craftingWithdrawalBellAt = now;
        }
        catch (RetrievalRecipeChoiceException e) { CancelCraftingWithdrawal(e.Message); retrievalRecipeChoice = e.ItemId; }
        catch (Exception e) { CancelCraftingWithdrawal(e.Message); }
    }

    private void DrawCraftingWithdrawalRestore()
    {
        if (craftingWithdrawalPending)
        {
            ImGui.TextUnformatted("Preparing withdrawal / opening bell…");
            ImGui.SameLine(); if (ImGui.SmallButton("Cancel withdrawal")) CancelCraftingWithdrawal("Withdrawal cancelled.");
            return;
        }
        if (craftingWithdrawalPrevious == null) return;
        if (ImGui.SmallButton("Restore previous list"))
        {
            SelectRetrievalPreset(craftingWithdrawalPrevious);
            retrievalEditingPreset = craftingWithdrawalPreviousEditing;
            craftingWithdrawalPrevious = null;
            craftingWithdrawalPreviousEditing = null;
            retrievalError = "";
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Restore the list you were working on before opening this crafting recipe. Saved presets are unchanged.");
    }
}
