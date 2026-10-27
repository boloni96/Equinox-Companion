using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.ContextMenu;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private TradeItemShortcutRequest? pendingTradeItemShortcut;
    private TradeItemMenuCleanup? tradeItemMenuCleanup;
    private Task<HashSet<string>>? tradeItemLabelsTask;
    private string tradeItemShortcutStatus = "No shortcut used this session.";
    private int tradeItemShortcutActions;
    private long tradeItemShortcutLastAction = long.MinValue / 2;
    private static readonly string[] TradeShortcutAddons = ["Trade", "ContextMenu"];

    private void InitializeTradeItemShortcut()
    {
        ContextMenu.OnMenuOpened += OnTradeItemShortcutMenu;
        Addons.RegisterListener(AddonEvent.PreFinalize, TradeShortcutAddons, OnTradeItemShortcutClosed);
        if (config.EnableTradeItemShortcut) PrepareTradeItemShortcutLabels();
    }

    private void DisposeTradeItemShortcut()
    {
        pendingTradeItemShortcut = null;
        tradeItemMenuCleanup = null;
        ContextMenu.OnMenuOpened -= OnTradeItemShortcutMenu;
        Addons.UnregisterListener(AddonEvent.PreFinalize, TradeShortcutAddons, OnTradeItemShortcutClosed);
    }

    private void SetTradeItemShortcutEnabled(bool enabled)
    {
        config.EnableTradeItemShortcut = enabled;
        pendingTradeItemShortcut = null;
        tradeItemMenuCleanup = null;
        if (enabled) PrepareTradeItemShortcutLabels();
        SaveConfiguration();
    }

    private void PrepareTradeItemShortcutLabels()
    {
        // Resolve stable Addon row IDs from English, then use this client's
        // localized labels. Run the one-time sheet scan off the gameplay thread.
        if (tradeItemLabelsTask is not null && !tradeItemLabelsTask.IsFaulted) return;
        tradeItemLabelsTask = Task.Run(() =>
        {
            var local = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>();
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>(ClientLanguage.English))
                if (row.Text.ExtractText() == "Trade" && local.GetRowOrDefault(row.RowId) is {} translated)
                {
                    var label = translated.Text.ExtractText();
                    if (label.Length > 0) labels.Add(label);
                }
            return labels;
        });
    }

    private void OnTradeItemShortcutClosed(AddonEvent _, AddonArgs __)
    {
        pendingTradeItemShortcut = null;
        tradeItemMenuCleanup = null;
    }

    private unsafe bool ReadTradeItemShortcutTarget(out TradeItemShortcutTarget target)
    {
        target = default;
        if (!config.EnableTradeItemShortcut || gilTrade.Running || !Player.IsLoaded || Player.ContentId == 0 ||
            Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51] ||
            Conditions[ConditionFlag.LoggingOut] || Conditions[ConditionFlag.InCombat] ||
            SellerAddon("InputNumeric") != null || SellerAddon("SelectYesno") != null) return false;
        var trade = SellerAddon("Trade");
        var inv = InventoryManager.Instance();
        var agent = AgentInventoryContext.Instance();
        if (trade == null || inv == null || agent == null || inv->TradePartnerEntityId is 0 or 0xE0000000 ||
            (int)inv->TradeLocalState != 3 || (int)inv->TradeRemoteState != 3 || inv->TradeIsSyncPending) return false;
        var container = inv->GetInventoryContainer(agent->TargetInventoryId);
        if (!TradeItemShortcutNative.ContainerSlotAvailable(container, agent->TargetInventoryId, agent->TargetInventorySlotId)) return false;
        var hasRoom = false;
        for (var i = 0; i < 5; i++) hasRoom |= inv->TradeItemsLocal[i].ItemId == 0 && inv->TradeItemsLocal[i].Quantity == 0;
        if (!hasRoom) return false;
        var item = inv->GetInventorySlot(agent->TargetInventoryId, agent->TargetInventorySlotId);
        if (!TradeItemShortcutNative.CaptureItem(item, agent->TargetInventorySlot,
            agent->TargetInventoryId, agent->TargetInventorySlotId, out target)) return false;
        target = target with { Character = Player.ContentId, Partner = inv->TradePartnerEntityId, TradeAddon = (nint)trade };
        return true;
    }

    private unsafe void OnTradeItemShortcutMenu(IMenuOpenedArgs args)
    {
        // Any new menu invalidates an older click, including another inventory
        // slot or a plugin-supplied item menu.
        pendingTradeItemShortcut = null;
        tradeItemMenuCleanup = null;
        if (!config.EnableTradeItemShortcut || args.MenuType != ContextMenuType.Inventory || openingPluginItem != 0 ||
            args.AgentPtr != (nint)AgentInventoryContext.Instance()) return;
        var focused = ShortcutKeyboard.OwnGameFocused();
        if (!ShortcutKeyboard.Down(0x11, focused) || ShortcutKeyboard.Down(0x10, focused) || ShortcutKeyboard.Down(0x12, focused)) return;
        try
        {
            if (gilTrade.Running) { tradeItemShortcutStatus = "Stop the active Gil Trader session before adding items."; return; }
            if (tradeItemLabelsTask?.IsCompletedSuccessfully != true)
            {
                if (tradeItemLabelsTask?.Exception is {} error) Log.Warning(error, "Trade shortcut labels unavailable.");
                tradeItemShortcutStatus = "Trade labels are loading; use the normal Trade menu for this click.";
                PrepareTradeItemShortcutLabels(); return;
            }
            var now = Environment.TickCount64;
            if (now - tradeItemShortcutLastAction < 250 || !ReadTradeItemShortcutTarget(out var target)) return;
            // Capture the clicked item now; menu labels can finish populating
            // after OnMenuOpened, especially with item-specific plugin entries.
            pendingTradeItemShortcut = new(target, "", now);
            tradeItemShortcutStatus = "Waiting for the item menu.";
        }
        catch (Exception ex)
        {
            pendingTradeItemShortcut = null;
            tradeItemShortcutStatus = "Shortcut skipped; use the normal Trade menu.";
            Log.Warning(ex, "Inventory trade shortcut skipped.");
        }
    }

    private unsafe void UpdateTradeItemShortcut(DateTimeOffset _)
    {
        UpdateTradeItemMenuCleanup();
        var request = pendingTradeItemShortcut;
        if (request is null) return;
        var now = Environment.TickCount64;
        if (now - request.Created < 50) return; // Wait until Dalamud has inserted other plugins' entries.
        if (!ShortcutKeyboard.OwnGameFocused() || !ReadTradeItemShortcutTarget(out var current) || !request.Matches(current, now))
        {
            pendingTradeItemShortcut = null;
            tradeItemShortcutStatus = "Shortcut cancelled: the trade, inventory item or menu changed.";
            return;
        }
        var menu = SellerAddon("ContextMenu");
        if (menu == null) return; // Bounded by the request's 750 ms lifetime.
        var agent = AgentInventoryContext.Instance();
        if (tradeItemLabelsTask?.IsCompletedSuccessfully != true) return;
        var nativeIndex = TradeItemShortcutNative.NativeEntry(agent, tradeItemLabelsTask.Result);
        if (nativeIndex < 0) { tradeItemShortcutStatus = "Waiting for an enabled Trade action; unavailable items remain manual."; return; }
        var label = TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex + nativeIndex].String.Value);
        var index = TradeItemShortcutNative.DisplayedEntry(menu->AtkValues, menu->AtkValuesCount, label);
        if (index < 0) { tradeItemShortcutStatus = "Waiting for the item menu to finish loading."; return; }
        pendingTradeItemShortcut = null; // Select only once, including if it throws.
        var cleanup = new TradeItemMenuCleanup((nint)menu, current,
            TradeItemShortcutNative.OfferedQuantity(InventoryManager.Instance(), current), now);
        tradeItemMenuCleanup = cleanup;
        tradeItemShortcutLastAction = now;
        // OnMenuSelected is the real click route, including Dalamud's mapping
        // from display indices to native indices. A raw FireCallback bypasses
        // that mapping and can select the wrong action on item-specific menus.
        ((AddonContextMenu*)menu)->OnMenuSelected(index, 0);
        tradeItemShortcutActions++;
        tradeItemShortcutStatus = "Selected Trade. Choose the amount if asked; final acceptance stays manual.";
        // Do not close immediately: stacks may open their quantity dialog on
        // the next frame. Cleanup only follows an observed offer or dismissed prompt.
    }

    private unsafe void UpdateTradeItemMenuCleanup()
    {
        var cleanup = tradeItemMenuCleanup;
        if (cleanup is null) return;
        var menu = SellerAddon("ContextMenu");
        var trade = SellerAddon("Trade");
        var inv = InventoryManager.Instance();
        var agent = AgentInventoryContext.Instance();
        var same = config.EnableTradeItemShortcut && !gilTrade.Running &&
            Player.IsLoaded && Player.ContentId == cleanup.Target.Character &&
            menu != null && (nint)menu == cleanup.Menu && trade != null && (nint)trade == cleanup.Target.TradeAddon &&
            inv != null && inv->TradePartnerEntityId == cleanup.Target.Partner &&
            agent != null && (uint)agent->TargetInventoryId == cleanup.Target.Inventory &&
            agent->TargetInventorySlotId == cleanup.Target.Slot;
        var decision = cleanup.Step(Environment.TickCount64, same, SellerAddon("InputNumeric") != null,
            inv != null && TradeItemShortcutNative.OfferedQuantity(inv, cleanup.Target) > cleanup.OfferedBefore);
        if (decision == TradeItemMenuDecision.Wait) return;
        tradeItemMenuCleanup = null;
        if (decision == TradeItemMenuDecision.Close)
        {
            menu->Hide(false, true, 0);
            tradeItemShortcutStatus = "Item menu closed. Review the offered items before accepting the trade.";
        }
    }
}
