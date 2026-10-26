using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;
internal static unsafe class GilTradeNative
{
    internal static bool PromptMatches(string actual, string expected) =>
        !string.IsNullOrWhiteSpace(expected) && string.Equals(actual.Trim(), expected.Trim(), StringComparison.Ordinal);

    internal static bool Offers(InventoryManager* inv, NumberArrayData* numbers, out bool items, out uint localGil, out uint remoteGil)
    {
        items = inv == null; localGil = remoteGil = 0;
        if (inv == null || numbers == null || numbers->IntArray == null || numbers->Size < sizeof(TradeNumberArray) / sizeof(int)) return false;
        var view = (TradeNumberArray*)numbers->IntArray;
        for (var i = 0; i < 5; i++)
            items |= inv->TradeItemsLocal[i].ItemId != 0 || inv->TradeItemsRemote[i].ItemId != 0 ||
                inv->TradeItemsLocal[i].Quantity != 0 || inv->TradeItemsRemote[i].Quantity != 0 ||
                view->ItemsGive[i].IconId != 0 || view->ItemsGive[i].Quantity != 0 ||
                view->ItemsReviece[i].IconId != 0 || view->ItemsReviece[i].Quantity != 0;
        // Currency slot is separate from the five item slots. Reject malformed
        // records rather than silently treating an unknown item as currency.
        items |= inv->TradeItemsLocal[5].ItemId > 1 || inv->TradeItemsRemote[5].ItemId > 1;
        items |= inv->TradeItemsLocal[5].Quantity < 0 || inv->TradeItemsRemote[5].Quantity < 0;
        // These are the counters rendered by Trade, not the inventory copies of
        // currency slot 5 (which can remain zero after SetTradeGilAmount).
        // Read only while the current Trade addon is ready; never substitute the
        // requested amount or a wallet balance for an observed offer.
        localGil = view->SendGilCount; remoteGil = view->ReceiveGilCount;
        return true;
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
