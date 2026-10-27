using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace EquinoxCompanion;

// Deliberately uses the game's enabled Trade action. No item-transfer function,
// numeric prompt confirmation or trade acceptance is invoked by this feature.
internal static unsafe class TradeItemShortcutNative
{
    // Eligibility comes from the game's enabled Trade menu action, not a list
    // of bag IDs. Validate each container's actual capacity before reading it.
    internal static bool ContainerSlotAvailable(InventoryContainer* container, InventoryType type, int slot) =>
        type != InventoryType.Invalid && container != null && container->IsLoaded &&
        container->Items != null && container->Type == type && slot >= 0 && slot < container->Size;

    internal static bool CaptureItem(InventoryItem* source, InventoryItem* contextItem,
        InventoryType type, int slot, out TradeItemShortcutTarget target,
        SellerNativeInterop.LinkedItemReader? readLinked = null)
    {
        target = default;
        if (source == null || contextItem == null || type == InventoryType.Invalid || slot < 0 ||
            source->Container != type || source->Slot != slot) return false;
        var item = SellerNativeInterop.Resolve(source, readLinked);
        if (item == null || (contextItem != source && contextItem != item) ||
            item->ItemId == 0 || item->Quantity <= 0) return false;
        // Preserve both the clicked view slot and the resolved item. A linked
        // slot being repointed, even to an identical item, cancels the request.
        target = new(0, 0, 0, (nint)source, (uint)type, slot, item->ItemId, (byte)item->Flags, item->Quantity)
        {
            ResolvedItemPointer = (nint)item,
            ResolvedInventory = (uint)item->Container,
            ResolvedSlot = item->Slot
        };
        return true;
    }

    internal static long OfferedQuantity(InventoryManager* inventory, TradeItemShortcutTarget target)
    {
        if (inventory == null) return 0;
        long quantity = 0;
        for (var i = 0; i < 5; i++)
        {
            var item = inventory->TradeItemsLocal[i];
            if (item.ItemId == target.ItemId && (byte)item.Flags == target.Flags && item.Quantity > 0)
                quantity += item.Quantity;
        }
        return quantity;
    }

    internal static int NativeEntry(AgentInventoryContext* agent, IReadOnlySet<string> labels)
    {
        if (agent == null || agent->ContextItemCount is < 1 or > 32 ||
            agent->ContexItemStartIndex < 0 ||
            agent->ContexItemStartIndex + agent->ContextItemCount > agent->EventParams.Length) return -1;
        var found = -1;
        for (var i = 0; i < agent->ContextItemCount; i++)
        {
            var value = agent->EventParams[agent->ContexItemStartIndex + i];
            if (value.Type is not (AtkType.String or AtkType.ManagedString or AtkType.ConstString) ||
                !labels.Contains(Read(value.String.Value))) continue;
            if (found >= 0 || (agent->ContextItemDisabledMask & (1u << i)) != 0 ||
                (agent->ContextItemSubmenuMask & (1u << i)) != 0) return -1;
            found = i;
        }
        return found;
    }

    internal static int DisplayedEntry(AtkValue* values, int valueCount, string label)
    {
        // Dalamud can prepend plugin entries. Never use the native entry's
        // index as the rendered callback index: it could now mean another action.
        if (values == null || valueCount < 9 || values[0].Type != AtkType.UInt ||
            values[0].UInt is < 1 or > 32 || string.IsNullOrEmpty(label)) return -1;
        var count = (int)values[0].UInt;
        if (valueCount != 8 + count && valueCount != 8 + count * 2) return -1;
        if (values[2].Type != AtkType.UInt || values[3].Type != AtkType.UInt) return -1;
        var found = -1;
        for (var i = 0; i < count; i++)
        {
            var value = values[8 + i];
            if (value.Type is not (AtkType.String or AtkType.ManagedString or AtkType.ConstString) ||
                !string.Equals(Read(value.String.Value), label, StringComparison.Ordinal)) continue;
            if (found >= 0 || ((values[2].UInt | values[3].UInt) & (1u << i)) != 0) return -1;
            if (valueCount == 8 + count * 2)
            {
                var disabled = values[8 + count + i];
                if (disabled.Type != AtkType.Int || disabled.Int != 0) return -1;
            }
            found = i;
        }
        return found;
    }

    internal static string Read(byte* text)
    {
        if (text == null) return "";
        var length = 0;
        while (length < 2048 && text[length] != 0) length++;
        // Keep the raw label so plugin prefixes/formatting cannot impersonate
        // a plain native label after stripping SeString payloads.
        return length == 2048 ? "" : System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(text, length));
    }
}

internal readonly record struct TradeItemShortcutTarget(
    ulong Character, uint Partner, nint TradeAddon, nint ItemPointer,
    uint Inventory, int Slot, uint ItemId, byte Flags, int Quantity)
{
    internal nint ResolvedItemPointer { get; init; }
    internal uint ResolvedInventory { get; init; }
    internal int ResolvedSlot { get; init; }
}

internal sealed record TradeItemShortcutRequest(TradeItemShortcutTarget Target, string Label, long Created)
{
    internal const long Lifetime = 2500;
    internal nint Menu { get; private set; }
    internal void BindMenu(nint menu) { if (Menu == 0 && menu != 0) Menu = menu; }
    // OnMenuOpened runs BEFORE the game opens/replaces ContextMenu. Closing
    // the old addon must not cancel the request for the incoming menu.
    internal bool MenuClosed(nint menu) => Menu != 0 && Menu == menu;
    internal bool Expired(long now) => now < Created || now - Created > Lifetime;
    internal bool Matches(TradeItemShortcutTarget current, long now) =>
        !Expired(now) && Target == current;
    internal bool Ready(long now, bool syncPending, long lastAction) =>
        !Expired(now) && now - Created >= 50 && Menu != 0 && !syncPending && now - lastAction >= 250;
}

internal enum TradeItemMenuDecision { Wait, Close, Forget }

internal sealed class TradeItemMenuCleanup(nint menu, TradeItemShortcutTarget target, long offeredBefore, long created)
{
    internal nint Menu { get; } = menu;
    internal TradeItemShortcutTarget Target { get; } = target;
    internal long OfferedBefore { get; } = offeredBefore;
    private bool sawQuantity;

    internal TradeItemMenuDecision Step(long now, bool sameContext, bool quantityVisible, bool offerIncreased)
    {
        if (!sameContext || now < created || now - created > 300_000) return TradeItemMenuDecision.Forget;
        if (quantityVisible) { sawQuantity = true; return TradeItemMenuDecision.Wait; }
        if (offerIncreased || sawQuantity) return TradeItemMenuDecision.Close;
        // An action with no observed effect remains manual; never hide an error.
        return now - created > 3000 ? TradeItemMenuDecision.Forget : TradeItemMenuDecision.Wait;
    }
}

internal enum TradeItemOfferDecision { Wait, Added, NoOffer, Cancelled }

// Confirmation survives ContextMenu closing: the native action can finish
// adding the item on a later frame, or after the player's quantity choice.
internal sealed class TradeItemOfferWatch(TradeItemShortcutTarget target, long offeredBefore, long created)
{
    internal TradeItemShortcutTarget Target { get; } = target;
    internal long OfferedBefore { get; } = offeredBefore;
    private bool sawQuantity;
    private long? quantityDismissed;
    internal TradeItemOfferDecision Step(long now, bool sameTrade, bool quantityVisible, bool offerIncreased)
    {
        if (!sameTrade || now < created || now - created > 300_000) return TradeItemOfferDecision.Cancelled;
        if (offerIncreased) return TradeItemOfferDecision.Added;
        if (quantityVisible) { sawQuantity = true; return TradeItemOfferDecision.Wait; }
        if (sawQuantity)
        {
            quantityDismissed ??= now;
            return now - quantityDismissed.Value > 1500 ? TradeItemOfferDecision.NoOffer : TradeItemOfferDecision.Wait;
        }
        return now - created > 3000 ? TradeItemOfferDecision.NoOffer : TradeItemOfferDecision.Wait;
    }
}

internal sealed record TradeItemShortcutTrace(DateTimeOffset At, uint ItemId, string ItemName,
    uint Inventory, int Slot, int Quantity, string Stage, string Detail);

