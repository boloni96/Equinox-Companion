using EquinoxCompanion;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.CompilerServices;

internal static unsafe class GilTradeNative250Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Native gil: " + why); }
        InventoryManager inventory = default;
        // Trade's raw number-array fixture: counters are independent of the
        // inventory currency copies. This reproduces the observed fill stall.
        var values = stackalloc int[133];
        for (var i = 0; i < 133; i++) values[i] = 0;
        NumberArrayData numbers = default; numbers.Size = 133; numbers.IntArray = values;
        values[60] = 1_000_000;
        inventory.TradeItemsLocal[5].ItemId = 1; inventory.TradeItemsLocal[5].Quantity = 0;
        GilTradeNative.Offers(&inventory, &numbers, out var items, out var local, out var remote);
        Check(!items && local == 1_000_000 && remote == 0, "displayed million is read even while inventory currency copy remains zero");
        inventory.TradeItemsRemote[5].ItemId = 1; inventory.TradeItemsRemote[5].Quantity = 0; values[61] = 123;
        GilTradeNative.Offers(&inventory, &numbers, out items, out local, out remote);
        Check(!items && local == 1_000_000 && remote == 123, "both currency sides read independently");
        for (var i = 0; i < 5; i++)
        {
            inventory.TradeItemsRemote[i].ItemId = 99; GilTradeNative.Offers(&inventory, &numbers, out items, out _, out _);
            Check(items, "every remote item slot checked"); inventory.TradeItemsRemote[i] = default;
            inventory.TradeItemsLocal[i].Quantity = 1; GilTradeNative.Offers(&inventory, &numbers, out items, out _, out _);
            Check(items, "every local item slot checked even if partially loaded"); inventory.TradeItemsLocal[i] = default;
        }
        inventory.TradeItemsLocal[5].ItemId = 99; GilTradeNative.Offers(&inventory, &numbers, out items, out _, out _);
        Check(items, "unknown currency slot rejected");
        GilTradeNative.Offers(null, &numbers, out items, out local, out remote); Check(items && local == 0 && remote == 0, "missing manager cannot trade");
        inventory.TradeItemsLocal[5].ItemId = 1;
        inventory.TradeItemsLocal[5].Quantity = 999;
        values[60] = 1; values[61] = 1_000_000;
        Check(GilTradeNative.Offers(&inventory, &numbers, out items, out local, out remote) && !items && local == 1 && remote == 1_000_000,
            "stale inventory currency cannot replace either displayed amount");
        values[60] = values[61] = 0;
        Check(GilTradeNative.Offers(&inventory, &numbers, out items, out local, out remote) && local == 0 && remote == 0,
            "empty displayed offer never falls back to stale currency");
        for (var i = 0; i < 10; i++)
        {
            values[i * 6] = 42; GilTradeNative.Offers(&inventory, &numbers, out items, out _, out _);
            Check(items, "every displayed item icon checked independently"); values[i * 6] = 0;
            values[i * 6 + 1] = 1; GilTradeNative.Offers(&inventory, &numbers, out items, out _, out _);
            Check(items, "every displayed item quantity checked independently"); values[i * 6 + 1] = 0;
        }
        Check(!GilTradeNative.Offers(&inventory, null, out _, out _, out _), "missing number array is not a ready offer");
        numbers.Size = 64;
        Check(!GilTradeNative.Offers(&inventory, &numbers, out _, out _, out _), "short number array is not read");
        numbers.Size = 133; numbers.IntArray = null;
        Check(!GilTradeNative.Offers(&inventory, &numbers, out _, out _, out _), "missing array storage is not read");
        Check(GilTradeNative.PromptMatches("Complete trade?", "Complete trade?"), "exact native confirmation");
        Check(GilTradeNative.PromptMatches("Échanger ?", "Échanger ?"), "localized confirmation");
        Check(!GilTradeNative.PromptMatches("Complete trade?", "") && !GilTradeNative.PromptMatches("Accept teleport?", "Complete trade?"), "empty and unrelated prompts refused");
        AtkUnitBase addon = default; AtkComponentButton button = default; AtkComponentNode node = default;
        button.OwnerNode = &node; node.NodeFlags = NodeFlags.Enabled;
        Check(!GilTradeNative.RegisteredClick(&addon, &button, out _), "no invented callback if event missing");
        AtkEvent evt = new() { Param = 72, State = new() { EventType = AtkEventType.ButtonClick } }; node.AtkEventManager.Event = &evt;
        Check(GilTradeNative.RegisteredClick(&addon, &button, out var click) && click.Param == 72 && click.Listener == (AtkEventListener*)&addon, "actual registered click preserved");
        node.NodeFlags = 0; Check(!GilTradeNative.RegisteredClick(&addon, &button, out _), "disabled button refused");
        Check(!GilTradeNative.TradeButtonEvent(null, out _), "missing trade UI refused");
        Console.WriteLine($"Native Gil Trader checks passed: {count}.");
    }
}
