using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.ContextMenu;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private TradeItemShortcutRequest? pendingTradeItemShortcut;
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
        ContextMenu.OnMenuOpened -= OnTradeItemShortcutMenu;
        Addons.UnregisterListener(AddonEvent.PreFinalize, TradeShortcutAddons, OnTradeItemShortcutClosed);
    }

    private void SetTradeItemShortcutEnabled(bool enabled)
    {
        config.EnableTradeItemShortcut = enabled;
        pendingTradeItemShortcut = null;
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

    private void OnTradeItemShortcutClosed(AddonEvent _, AddonArgs __) => pendingTradeItemShortcut = null;

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
            (int)inv->TradeLocalState != 3 || (int)inv->TradeRemoteState != 3 || inv->TradeIsSyncPending ||
            (uint)agent->TargetInventoryId > 3 || agent->TargetInventorySlotId is < 0 or >= 35) return false;
        var hasRoom = false;
        for (var i = 0; i < 5; i++) hasRoom |= inv->TradeItemsLocal[i].ItemId == 0 && inv->TradeItemsLocal[i].Quantity == 0;
        if (!hasRoom) return false;
        var item = inv->GetInventorySlot(agent->TargetInventoryId, agent->TargetInventorySlotId);
        if (item == null || item != agent->TargetInventorySlot || item->IsSymbolic || item->ItemId == 0 ||
            item->Quantity <= 0 || item->Container != agent->TargetInventoryId || item->Slot != agent->TargetInventorySlotId) return false;
        target = new(Player.ContentId, inv->TradePartnerEntityId, (nint)trade, (nint)item,
            (uint)item->Container, item->Slot, item->ItemId, (byte)item->Flags, item->Quantity);
        return true;
    }

    private unsafe void OnTradeItemShortcutMenu(IMenuOpenedArgs args)
    {
        // Any new menu invalidates an older click, including another inventory
        // slot or a plugin-supplied item menu.
        pendingTradeItemShortcut = null;
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
            var agent = AgentInventoryContext.Instance();
            var index = TradeItemShortcutNative.NativeEntry(agent, tradeItemLabelsTask.Result);
            if (index < 0) { tradeItemShortcutStatus = "This item's Trade action is unavailable; the normal menu remains open."; return; }
            var label = TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex + index].String.Value);
            pendingTradeItemShortcut = new(target, label, now);
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
        pendingTradeItemShortcut = null; // One callback at most, including if it throws.
        var agent = AgentInventoryContext.Instance();
        if (tradeItemLabelsTask?.IsCompletedSuccessfully != true ||
            TradeItemShortcutNative.NativeEntry(agent, tradeItemLabelsTask.Result) < 0) return;
        var index = TradeItemShortcutNative.DisplayedEntry(menu->AtkValues, menu->AtkValuesCount, request.Label);
        if (index < 0) { tradeItemShortcutStatus = "Trade menu could not be identified safely; choose Trade manually."; return; }
        var values = stackalloc AtkValue[5];
        for (var i = 0; i < 5; i++) { values[i] = default; values[i].Type = AtkType.Int; }
        values[1].Int = index;
        values[2].Type = AtkType.UInt;
        tradeItemShortcutLastAction = now;
        menu->FireCallback(5, values);
        tradeItemShortcutActions++;
        tradeItemShortcutStatus = "Selected Trade. Choose the stack quantity if asked, then review and accept the trade yourself.";
        // Leave both the normal quantity prompt and final acceptance to the
        // player. Closing ContextMenu here can cancel the game's quantity prompt.
    }
}
