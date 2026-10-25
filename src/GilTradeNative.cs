using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;
internal static unsafe class GilTradeNative
{
    internal static bool PromptMatches(string actual, string expected) =>
        !string.IsNullOrWhiteSpace(expected) && string.Equals(actual.Trim(), expected.Trim(), StringComparison.Ordinal);

    internal static void Offers(InventoryManager* inv, out bool items, out uint localGil, out uint remoteGil)
    {
        items = inv == null; localGil = remoteGil = 0;
        if (inv == null) return;
        for (var i = 0; i < 5; i++)
            items |= inv->TradeItemsLocal[i].ItemId != 0 || inv->TradeItemsRemote[i].ItemId != 0 ||
                inv->TradeItemsLocal[i].Quantity != 0 || inv->TradeItemsRemote[i].Quantity != 0;
        // Currency slot is separate from the five item slots. Reject malformed
        // records rather than silently treating an unknown item as currency.
        items |= inv->TradeItemsLocal[5].ItemId > 1 || inv->TradeItemsRemote[5].ItemId > 1;
        items |= inv->TradeItemsLocal[5].Quantity < 0 || inv->TradeItemsRemote[5].Quantity < 0;
        localGil = (uint)Math.Max(0, inv->TradeItemsLocal[5].Quantity); remoteGil = (uint)Math.Max(0, inv->TradeItemsRemote[5].Quantity);
    }

    internal static bool TradeButtonEvent(AtkUnitBase* addon, out AtkEvent click)
    {
        click = default;
        if (addon == null || !addon->IsVisible || !addon->IsReady || addon->NameString != "Trade" ||
            addon->UldManager.NodeList == null || addon->UldManager.NodeListCount <= 3) return false;
        var node = addon->UldManager.NodeList[3];
        if (node == null || (int)node->Type < 1000 || !node->IsVisible()) return false;
        var component = ((AtkComponentNode*)node)->Component;
        if (component == null || component->GetComponentType() != ComponentType.Button) return false;
        return RegisteredClick(addon, (AtkComponentButton*)component, out click);
    }

    internal static bool RegisteredClick(AtkUnitBase* addon, AtkComponentButton* button, out AtkEvent click)
    {
        click = default;
        if (addon == null || button == null || button->OwnerNode == null || !button->IsEnabled) return false;
        var e = button->OwnerNode->AtkEventManager.Event;
        for (var i = 0; e != null && i < 32; i++, e = e->NextEvent)
        {
            if (e->State.EventType != AtkEventType.ButtonClick) continue;
            click = *e; click.Listener = (AtkEventListener*)addon; return true;
        }
        return false; // Never invent an event parameter for a money transfer.
    }
}
