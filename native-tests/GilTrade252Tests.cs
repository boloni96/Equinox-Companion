using EquinoxCompanion;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.CompilerServices;

internal static unsafe class GilTrade252Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Gil offer integration: " + why); }
        var time = DateTimeOffset.Parse("2026-10-26T05:53:33Z");
        var idle = new GilTradeFrame(11, true, true, 22, 10_000_000, false, 0, false, 0, 0, 1, 1, false, false, false);
        InventoryManager inventory = default;
        var values = stackalloc int[133];
        for (var i = 0; i < 133; i++) values[i] = 0;
        // Deliberately nonzero presentation data with empty inventory slots.
        // UI templates/stale decorations must not be treated as offered items.
        for (var i = 0; i < 10; i++) { values[i * 6] = 42; values[i * 6 + 1] = 1; }
        NumberArrayData numbers = default; numbers.Size = 133; numbers.IntArray = values;
        var sender = new GilTradeRun(); var receiver = new GilTradeRun();
        sender.Start(true, 1_000_123, idle, time); receiver.Start(false, 0, idle, time);
        uint sent = 0;
        foreach (var amount in new uint[] { 1_000_000, 123 })
        {
            var senderIdle = idle with { Wallet = idle.Wallet - sent };
            var receiverIdle = idle with { Wallet = idle.Wallet + sent };
            Check(sender.Step(senderIdle, time) == GilTradeAction.Request, "start next exact trade");
            values[60] = values[61] = 0;
            Check(GilTradeNative.Offers(&inventory, &numbers, out var openingItems, out _, out _), "read newly opened trade");
            var open = senderIdle with { TradeOpen = true, TradePartner = 22, LocalState = 3, RemoteState = 3, ItemsPresent = openingItems };
            Check(sender.Step(open, time.AddMilliseconds(100)) == GilTradeAction.SetGil, "set amount once");
            Check(sender.Step(open with { TradeReady = false }, time.AddMilliseconds(200)) == GilTradeAction.None && sender.Running,
                "missing UI offer waits without accepting");
            values[60] = (int)amount; values[61] = 0;
            Check(GilTradeNative.Offers(&inventory, &numbers, out var items, out var local, out var remote), "read sender window");
            var sendView = open with { ItemsPresent = items, LocalGil = local, RemoteGil = remote };
            Check(sender.Step(sendView, time.AddSeconds(1)) == GilTradeAction.LockOffer && sender.ThisTrade == amount,
                "visible amount advances sender despite zero inventory currency");
            values[60] = 0; values[61] = (int)amount;
            Check(GilTradeNative.Offers(&inventory, &numbers, out items, out local, out remote), "read receiver window");
            var receiveView = open with { Wallet = receiverIdle.Wallet, ItemsPresent = items, LocalGil = local, RemoteGil = remote, RemoteState = 4 };
            receiver.Step(receiveView, time.AddSeconds(1));
            Check(receiver.Step(receiveView, time.AddSeconds(2)) == GilTradeAction.LockOffer && receiver.ThisTrade == amount,
                "receiver accepts same stable displayed amount");
            Check(sender.Step(sendView with { LocalState = 5, RemoteState = 4, Confirmation = true }, time.AddSeconds(3)) == GilTradeAction.Confirm,
                "sender reaches final confirmation");
            Check(receiver.Step(receiveView with { LocalState = 5, Confirmation = true }, time.AddSeconds(3)) == GilTradeAction.Confirm,
                "receiver reaches final confirmation");
            sent += amount;
            sender.Step(idle with { Wallet = idle.Wallet - sent }, time.AddSeconds(4));
            receiver.Step(idle with { Wallet = idle.Wallet + sent }, time.AddSeconds(4));
            Check(sender.Done == sent && receiver.Done == sent, "only exact closed trade wallet changes count");
            time = time.AddSeconds(7);
        }
        Check(sender.Stage == GilTradeStage.Complete && sender.Trades == 2 && receiver.Trades == 2, "million plus remainder completes on both sides");

        // A different amount in the real UI remains a stop, not an excuse to
        // accept our requested value or the old inventory value instead.
        var mismatch = new GilTradeRun(); mismatch.Start(true, 1_000_000, idle, time); mismatch.Step(idle, time);
        var active = idle with { TradeOpen = true, TradePartner = 22, LocalState = 3, RemoteState = 3 };
        mismatch.Step(active, time.AddMilliseconds(100));
        inventory.TradeItemsLocal[5].Quantity = 1_000_000;
        values[60] = 999_999; values[61] = 0;
        GilTradeNative.Offers(&inventory, &numbers, out var hasItems, out var offered, out var other);
        Check(mismatch.Step(active with { ItemsPresent = hasItems, LocalGil = offered, RemoteGil = other }, time.AddSeconds(1)) == GilTradeAction.None &&
            !mismatch.Running && mismatch.Done == 0, "incorrect displayed offer stops even if inventory matches request");
        Console.WriteLine($"Gil Trader UI offer integration checks passed: {count}.");
    }
}
