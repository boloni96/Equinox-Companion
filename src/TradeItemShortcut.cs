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
    private TradeItemOfferWatch? tradeItemOfferWatch;
    private readonly List<TradeItemShortcutTrace> tradeItemShortcutTrace = [];
    private Task<HashSet<string>>? tradeItemLabelsTask;
    private string tradeItemShortcutStatus = "No shortcut used this session.";
    private int tradeItemShortcutActions;
    private int tradeItemShortcutConfirmed;
    private long tradeItemShortcutLastAction = long.MinValue / 2;
    private static readonly string[] TradeShortcutAddons = ["Trade", "ContextMenu"];

    private void InitializeTradeItemShortcut()
    {
        ContextMenu.OnMenuOpened += OnTradeItemShortcutMenu;
        Addons.RegisterListener(AddonEvent.PreFinalize, TradeShortcutAddons, OnTradeItemShortcutClosed);
        Addons.RegisterListener(AddonEvent.PostSetup, "ContextMenu", OnTradeItemShortcutReady);
        Addons.RegisterListener(AddonEvent.PostRefresh, "ContextMenu", OnTradeItemShortcutReady);
        if (config.EnableTradeItemShortcut) PrepareTradeItemShortcutLabels();
    }

    private void DisposeTradeItemShortcut()
    {
        pendingTradeItemShortcut = null;
        tradeItemMenuCleanup = null;
        tradeItemOfferWatch = null;
        ContextMenu.OnMenuOpened -= OnTradeItemShortcutMenu;
        Addons.UnregisterListener(AddonEvent.PreFinalize, TradeShortcutAddons, OnTradeItemShortcutClosed);
        Addons.UnregisterListener(OnTradeItemShortcutReady);
    }

    private void SetTradeItemShortcutEnabled(bool enabled)
    {
        config.EnableTradeItemShortcut = enabled;
        pendingTradeItemShortcut = null;
        tradeItemMenuCleanup = null;
        tradeItemOfferWatch = null;
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

    private void OnTradeItemShortcutReady(AddonEvent _, AddonArgs args)
    {
        pendingTradeItemShortcut?.BindMenu(args.Addon.Address);
        CaptureItemTradeMenu(args);
    }

    private void OnTradeItemShortcutClosed(AddonEvent _, AddonArgs args)
    {
        if(args.AddonName=="Trade"||itemTradeMenuSelection?.Menu==args.Addon.Address)itemTradeMenuSelection=null;
        if (pendingTradeItemShortcut is {} request &&
            (args.AddonName == "Trade" || request.MenuClosed(args.Addon.Address)))
        {
            pendingTradeItemShortcut = null;
            RecordTradeItemShortcut(request.Target, "Cancelled", "The trade or clicked item menu closed before selection.");
        }
        if (args.AddonName == "Trade" || tradeItemMenuCleanup?.Menu == args.Addon.Address)
            tradeItemMenuCleanup = null;
        // Keep the offer watcher: ContextMenu normally closes before the item
        // or its quantity choice has finished updating the trade window.
    }

    private unsafe bool ReadTradeItemShortcutTarget(out TradeItemShortcutTarget target, out string reason, out bool syncPending)
    {
        target = default;
        reason = ""; syncPending = false;
        if (!config.EnableTradeItemShortcut) { reason = "The item shortcut is disabled."; return false; }
        if (TraderRunning) { reason = "Stop the active Gil Trader session before adding items."; return false; }
        if (!Player.IsLoaded || Player.ContentId == 0 ||
            Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51] ||
            Conditions[ConditionFlag.LoggingOut] || Conditions[ConditionFlag.InCombat])
        { reason = "The character is not ready to trade."; return false; }
        if (SellerAddon("InputNumeric") != null || SellerAddon("SelectYesno") != null)
        { reason = "Finish or cancel the open quantity/confirmation dialog first."; return false; }
        var trade = SellerAddon("Trade");
        var inv = InventoryManager.Instance();
        var agent = AgentInventoryContext.Instance();
        if (trade == null || inv == null || agent == null || inv->TradePartnerEntityId is 0 or 0xE0000000)
        { reason = "Open a trade with a player first."; return false; }
        if ((int)inv->TradeLocalState != 3 || (int)inv->TradeRemoteState != 3)
        { reason = "The trade is locked or awaiting confirmation; return to choosing items first."; return false; }
        syncPending = inv->TradeIsSyncPending;
        var container = inv->GetInventoryContainer(agent->TargetInventoryId);
        if (!TradeItemShortcutNative.ContainerSlotAvailable(container, agent->TargetInventoryId, agent->TargetInventorySlotId))
        { reason = "The clicked inventory slot is unavailable or not loaded."; return false; }
        var hasRoom = false;
        for (var i = 0; i < 5; i++) hasRoom |= inv->TradeItemsLocal[i].ItemId == 0 && inv->TradeItemsLocal[i].Quantity == 0;
        if (!hasRoom) { reason = "All five item slots in the trade are occupied."; return false; }
        var item = inv->GetInventorySlot(agent->TargetInventoryId, agent->TargetInventorySlotId);
        if (!TradeItemShortcutNative.CaptureItem(item, agent->TargetInventorySlot,
            agent->TargetInventoryId, agent->TargetInventorySlotId, out target))
        { reason = "The item menu does not match the loaded inventory slot."; return false; }
        target = target with { Character = Player.ContentId, Partner = inv->TradePartnerEntityId, TradeAddon = (nint)trade };
        return true;
    }

    private unsafe void OnTradeItemShortcutMenu(IMenuOpenedArgs args)
    {
        // Any new menu invalidates an older click, including another inventory
        // slot or a plugin-supplied item menu.
        if (pendingTradeItemShortcut is {} previous)
            RecordTradeItemShortcut(previous.Target, "Cancelled", "Another item menu opened before this click completed.");
        pendingTradeItemShortcut = null;
        tradeItemMenuCleanup = null;
        if (!config.EnableTradeItemShortcut || args.MenuType != ContextMenuType.Inventory || openingPluginItem != 0 ||
            args.AgentPtr != (nint)AgentInventoryContext.Instance()) return;
        var focused = ShortcutKeyboard.OwnGameFocused();
        if (!ShortcutKeyboard.Down(0x11, focused) || ShortcutKeyboard.Down(0x10, focused) || ShortcutKeyboard.Down(0x12, focused)) return;
        if (SellerAddon("Trade") == null) return;
        try
        {
            var now = Environment.TickCount64;
            if (!ReadTradeItemShortcutTarget(out var target, out var reason, out var syncPending))
            { RecordTradeItemShortcut(ReadTradeItemDiagnosticTarget(), "Skipped", reason); return; }
            if (tradeItemLabelsTask?.IsCompletedSuccessfully != true) PrepareTradeItemShortcutLabels();
            // Capture the clicked item now; menu labels can finish populating
            // after OnMenuOpened, especially with item-specific plugin entries.
            pendingTradeItemShortcut = new(target, "", now);
            RecordTradeItemShortcut(target, "Waiting", syncPending ? "Waiting for the previous trade update." : "Waiting for the item menu.");
        }
        catch (Exception ex)
        {
            pendingTradeItemShortcut = null;
            RecordTradeItemShortcut(ReadTradeItemDiagnosticTarget(), "Skipped", "Shortcut could not read the menu (" + ex.GetType().Name + "). Use Trade normally.");
            Log.Warning(ex, "Inventory trade shortcut skipped.");
        }
    }

    private unsafe void UpdateTradeItemShortcut(DateTimeOffset _)
    {
        UpdateTradeItemOfferStatus();
        UpdateTradeItemMenuCleanup();
        var request = pendingTradeItemShortcut;
        if (request is null) return;
        var now = Environment.TickCount64;
        if (request.Expired(now))
        {
            pendingTradeItemShortcut = null;
            RecordTradeItemShortcut(request.Target, "Not added", tradeItemShortcutStatus + " Try Trade normally from the same menu. " + TradeItemMenuDiagnostic());
            return;
        }
        if (now - request.Created < 50) return; // Wait until Dalamud has inserted other plugins' entries.
        if (!ShortcutKeyboard.OwnGameFocused())
        {
            pendingTradeItemShortcut = null;
            RecordTradeItemShortcut(request.Target, "Cancelled", "The game lost focus before selection."); return;
        }
        if (!ReadTradeItemShortcutTarget(out var current, out var reason, out var syncPending) || !request.Matches(current, now))
        {
            pendingTradeItemShortcut = null;
            RecordTradeItemShortcut(request.Target, "Cancelled", reason.Length > 0 ? reason : "The trade or clicked item changed before selection.");
            return;
        }
        var menu = SellerAddon("ContextMenu");
        if (request.Menu == 0) return;
        if (menu == null)
        { pendingTradeItemShortcut = null; RecordTradeItemShortcut(request.Target, "Cancelled", "The clicked item menu was closed."); return; }
        if ((nint)menu != request.Menu)
        { pendingTradeItemShortcut = null; RecordTradeItemShortcut(request.Target, "Cancelled", "A different item menu replaced this click."); return; }
        if (!request.Ready(now, syncPending, tradeItemShortcutLastAction))
        { tradeItemShortcutStatus = "Waiting for the previous trade update."; return; }
        var agent = AgentInventoryContext.Instance();
        if (tradeItemLabelsTask?.IsCompletedSuccessfully != true)
        { tradeItemShortcutStatus = "Trade labels are still loading."; return; }
        var nativeIndex = TradeItemShortcutNative.NativeEntry(agent, tradeItemLabelsTask.Result);
        if (nativeIndex < 0) { tradeItemShortcutStatus = "The game's Trade action is missing, disabled or ambiguous for this item."; return; }
        var label = TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex + nativeIndex].String.Value);
        var index = TradeItemShortcutNative.DisplayedEntry(menu->AtkValues, menu->AtkValuesCount, label);
        if (index < 0) { tradeItemShortcutStatus = "The enabled Trade action could not be matched in the displayed menu."; return; }
        pendingTradeItemShortcut = null; // Select only once, including if it throws.
        var cleanup = new TradeItemMenuCleanup((nint)menu, current,
            TradeItemShortcutNative.OfferedQuantity(InventoryManager.Instance(), current), now);
        tradeItemMenuCleanup = cleanup;
        tradeItemOfferWatch = new(current, cleanup.OfferedBefore, now);
        tradeItemShortcutLastAction = now;
        // OnMenuSelected is the real click route, including Dalamud's mapping
        // from display indices to native indices. A raw FireCallback bypasses
        // that mapping and can select the wrong action on item-specific menus.
        ((AddonContextMenu*)menu)->OnMenuSelected(index, 0);
        tradeItemShortcutActions++;
        RecordTradeItemShortcut(current, "Selected Trade", $"Native action {nativeIndex}; displayed row {index}. Choose the amount if asked. Waiting for the item to appear in the offer.");
        // Do not close immediately: stacks may open their quantity dialog on
        // the next frame. Cleanup only follows an observed offer or dismissed prompt.
    }

    private unsafe TradeItemShortcutTarget ReadTradeItemDiagnosticTarget()
    {
        var agent = AgentInventoryContext.Instance();
        if (agent == null) return default;
        var item = agent->TargetInventorySlot;
        return new(Player.ContentId, 0, 0, 0, (uint)agent->TargetInventoryId, agent->TargetInventorySlotId,
            item != null && !item->IsSymbolic ? item->ItemId : 0, item != null ? (byte)item->Flags : (byte)0,
            item != null ? item->Quantity : 0);
    }

    private void RecordTradeItemShortcut(TradeItemShortcutTarget target, string stage, string detail)
    {
        var name = target.ItemId > 0 ? $"Item #{target.ItemId}" : "Item";
        try
        {
            if (target.ItemId > 0 && DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(target.ItemId) is {} row)
                name = row.Name.ExtractText();
        }
        catch { /* Keep the ID when sheet data is unavailable. */ }
        tradeItemShortcutStatus = name + " · " + stage + ": " + (detail.Length > 200 ? detail[..200] + "…" : detail);
        tradeItemShortcutTrace.Add(new(DateTimeOffset.UtcNow, target.ItemId, name, target.Inventory, target.Slot, target.Quantity, stage, detail));
        if (tradeItemShortcutTrace.Count > 32) tradeItemShortcutTrace.RemoveAt(0);
    }

    private unsafe string TradeItemMenuDiagnostic()
    {
        var agent = AgentInventoryContext.Instance();
        var inv = InventoryManager.Instance();
        var menu = SellerAddon("ContextMenu");
        var labels = new List<string>();
        if (agent != null && agent->ContexItemStartIndex >= 0 && agent->ContextItemCount is > 0 and <= 32 &&
            agent->ContexItemStartIndex + agent->ContextItemCount <= agent->EventParams.Length)
            for (var i = 0; i < agent->ContextItemCount; i++)
            {
                var value = agent->EventParams[agent->ContexItemStartIndex + i];
                if (value.Type is not (AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString)) continue;
                var label = TradeItemShortcutNative.Read(value.String.Value);
                if (label.Length > 80) label = label[..80];
                labels.Add($"{i}:{label} (disabled={((agent->ContextItemDisabledMask & (1u << i)) != 0)})");
            }
        return $"Trade state={(inv != null ? (int)inv->TradeLocalState : -1)}/{(inv != null ? (int)inv->TradeRemoteState : -1)}; syncPending={inv != null && inv->TradeIsSyncPending}; menuValues={(menu != null ? menu->AtkValuesCount : 0)}; native=[{string.Join("; ", labels)}].";
    }

    private unsafe void UpdateTradeItemOfferStatus()
    {
        var watch = tradeItemOfferWatch;
        if (watch is null) return;
        var inv = InventoryManager.Instance();
        var trade = SellerAddon("Trade");
        var sameTrade = config.EnableTradeItemShortcut && Player.IsLoaded && Player.ContentId == watch.Target.Character &&
            inv != null && inv->TradePartnerEntityId == watch.Target.Partner && trade != null && (nint)trade == watch.Target.TradeAddon;
        var decision = watch.Step(Environment.TickCount64, sameTrade, SellerAddon("InputNumeric") != null,
            inv != null && TradeItemShortcutNative.OfferedQuantity(inv, watch.Target) > watch.OfferedBefore);
        if (decision == TradeItemOfferDecision.Wait) return;
        tradeItemOfferWatch = null;
        if (decision == TradeItemOfferDecision.Added)
        {
            tradeItemShortcutConfirmed++;
            RecordTradeItemShortcut(watch.Target, "Added to offer", "The item appeared in your trade window. Final acceptance remains manual.");
        }
        else if (decision == TradeItemOfferDecision.NoOffer)
            RecordTradeItemShortcut(watch.Target, "No item observed", "The quantity choice was cancelled, or the selected Trade action did not add the item. " + TradeItemMenuDiagnostic());
        else RecordTradeItemShortcut(watch.Target, "Unconfirmed", "The trade closed or changed before an added item was observed.");
    }

    private unsafe void UpdateTradeItemMenuCleanup()
    {
        var cleanup = tradeItemMenuCleanup;
        if (cleanup is null) return;
        var menu = SellerAddon("ContextMenu");
        var trade = SellerAddon("Trade");
        var inv = InventoryManager.Instance();
        var agent = AgentInventoryContext.Instance();
        var same = config.EnableTradeItemShortcut && !TraderRunning &&
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
            // The offer watcher reports the actual result, independently of
            // whether the menu needed closing.
        }
    }
}


