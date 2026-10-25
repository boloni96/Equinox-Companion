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
        inventory.TradeItemsLocal[5].ItemId = 1; inventory.TradeItemsLocal[5].Quantity = 1_000_000;
        GilTradeNative.Offers(&inventory, out var items, out var local, out var remote);
        Check(!items && local == 1_000_000 && remote == 0, "currency uses sixth slot");
        inventory.TradeItemsRemote[5].ItemId = 1; inventory.TradeItemsRemote[5].Quantity = 123;
        GilTradeNative.Offers(&inventory, out items, out local, out remote);
        Check(!items && local == 1_000_000 && remote == 123, "both currency sides read independently");
        for (var i = 0; i < 5; i++)
        {
            inventory.TradeItemsRemote[i].ItemId = 99; GilTradeNative.Offers(&inventory, out items, out _, out _);
            Check(items, "every remote item slot checked"); inventory.TradeItemsRemote[i] = default;
            inventory.TradeItemsLocal[i].Quantity = 1; GilTradeNative.Offers(&inventory, out items, out _, out _);
            Check(items, "every local item slot checked even if partially loaded"); inventory.TradeItemsLocal[i] = default;
        }
        inventory.TradeItemsLocal[5].ItemId = 99; GilTradeNative.Offers(&inventory, out items, out _, out _);
        Check(items, "unknown currency slot rejected");
        GilTradeNative.Offers(null, out items, out local, out remote); Check(items && local == 0 && remote == 0, "missing manager cannot trade");
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
