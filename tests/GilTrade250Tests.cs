using EquinoxCompanion;
using System.Runtime.CompilerServices;

internal static class GilTrade250Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Gil Trader: " + why); }
        var t = DateTimeOffset.Parse("2026-10-25T12:00:00Z");
        var initial = new GilTradeFrame(11, true, true, 22, 8_000_000, false, 0, false, 0, 0, 1, 1, false, false, false);
        var r = new GilTradeRun();
        Check(!r.Start(true, 0, initial, t), "zero cannot start");
        Check(!r.Start(true, 8_000_001, initial, t), "insufficient funds cannot start");
        Check(!r.Start(true, 100, initial with { TradeOpen = true }, t), "existing trade cannot be adopted by sender");
        Check(!r.Start(false, 0, initial with { Wallet = GilTradeRun.WalletLimit }, t), "full receiver wallet");
        Check(!r.Start(true, 100, initial with { PartnerNearby = false }, t), "must be nearby");
        Check(!r.Start(true, 100, initial with { Character = 0 }, t), "must be logged in");
        Check(!r.Start(true, 100, initial with { OtherDialog = true }, t), "preexisting dialog");
        Check(!r.Start(true, 100, initial with { Confirmation = true }, t), "preexisting confirmation");
        Check(r.Start(true, 2_500_123, initial, t), "sender starts");
        Check(!r.Start(false, 0, initial, t), "active permission cannot be replaced");
        uint wallet = initial.Wallet;
        foreach (var amount in new uint[] { 1_000_000, 1_000_000, 500_123 })
        {
            var idle = initial with { Wallet = wallet };
            Check(r.Step(idle, t) == GilTradeAction.Request && r.ThisTrade == amount, "exact next amount and partial remainder");
            Check(r.Step(idle, t.AddMilliseconds(100)) == GilTradeAction.None, "no repeated requests while waiting");
            var trade = idle with { TradeOpen = true, TradePartner = 22, LocalState = 3, RemoteState = 3 };
            Check(r.Step(trade with { SyncPending = true }, t.AddMilliseconds(150)) == GilTradeAction.None, "wait for native sync before setting");
            Check(r.Step(trade, t.AddMilliseconds(200)) == GilTradeAction.SetGil, "fill empty offer once");
            Check(r.Step(trade, t.AddMilliseconds(300)) == GilTradeAction.None, "await fill acknowledgment");
            trade = trade with { LocalGil = amount };
            Check(r.Step(trade, t.AddSeconds(1.1)) == GilTradeAction.LockOffer, "lock exact amount");
            Check(r.Step(trade with { LocalState = 4 }, t.AddSeconds(1.2)) == GilTradeAction.None, "wait for receiver");
            trade = trade with { LocalState = 5, RemoteState = 4, Confirmation = true };
            Check(r.Step(trade with { SyncPending = true }, t.AddSeconds(1.4)) == GilTradeAction.None, "no Yes while native sync pending");
            Check(r.Step(trade, t.AddSeconds(1.5)) == GilTradeAction.Confirm, "confirm matching locked trade");
            var previousDone = r.Done;
            Check(r.Step(trade, t.AddSeconds(1.6)) == GilTradeAction.None && r.Done == previousDone, "click alone is not success");
            wallet -= amount;
            Check(r.Step(trade with { Wallet = wallet }, t.AddSeconds(1.7)) == GilTradeAction.None && r.Done == previousDone, "wallet alone before window closes is not success");
            r.Step(idle with { Wallet = wallet }, t.AddSeconds(2));
            Check(r.Done == previousDone + amount, "closed and exact wallet delta confirms once");
            r.Step(idle with { Wallet = wallet }, t.AddSeconds(2.1));
            Check(r.Done == previousDone + amount, "no double count");
            t = t.AddSeconds(5);
        }
        Check(r.Stage == GilTradeStage.Complete && r.Done == 2_500_123 && r.Trades == 3, "three trades complete exact total");
        Check(r.Step(initial, t) == GilTradeAction.None, "complete never restarts");

        GilTradeRun SenderAtOffer()
        {
            var run = new GilTradeRun(); run.Start(true, 123, initial, t); run.Step(initial, t);
            run.Step(initial with { TradeOpen = true, TradePartner = 22, LocalState = 3, RemoteState = 3 }, t.AddMilliseconds(100)); return run;
        }
        var offer = initial with { TradeOpen = true, TradePartner = 22, LocalGil = 123, LocalState = 3, RemoteState = 3 };
        foreach (var bad in new[] {
            offer with { Character = 99 }, offer with { Ready = false }, offer with { PartnerNearby = false },
            offer with { PartnerEntity = 99 }, offer with { TradePartner = 99 }, offer with { ItemsPresent = true },
            offer with { RemoteGil = 1 }, offer with { LocalGil = 124 }, offer with { Wallet = 8_000_001 },
            offer with { OtherDialog = true }, offer with { TradeOpen = false }, offer with { LocalState = 99 }, offer with { RemoteState = 0 } })
        {
            var run = SenderAtOffer(); Check(run.Step(bad, t.AddSeconds(2)) == GilTradeAction.None && !run.Running && run.Done == 0, "interruption cannot transfer or count");
        }
        var changed = SenderAtOffer(); changed.Step(offer, t.AddSeconds(1));
        Check(changed.Step(offer with { LocalGil = 122, LocalState = 4, RemoteState = 4, Confirmation = true }, t.AddSeconds(2)) == GilTradeAction.None && !changed.Running, "changed locked offer cannot confirm");
        var stop = SenderAtOffer(); stop.Stop("Stopped");
        Check(stop.Step(offer, t.AddSeconds(2)) == GilTradeAction.None && stop.Done == 0, "Stop immediately revokes permission");
        var timeout = SenderAtOffer(); timeout.Step(offer, t.AddSeconds(62)); Check(!timeout.Running, "timeout stops without retry");
        var stale = SenderAtOffer(); stale.Step(offer, t.AddMinutes(31)); Check(!stale.Running, "session hard deadline");
        var unconfirmed = SenderAtOffer(); unconfirmed.Step(offer, t.AddSeconds(1));
        unconfirmed.Step(offer with { LocalState = 4, RemoteState = 4, Confirmation = true }, t.AddSeconds(2));
        unconfirmed.Step(initial, t.AddSeconds(15));
        Check(!unconfirmed.Running && unconfirmed.Done == 0, "closed trade without wallet change is not counted");
        var wrongDelta = SenderAtOffer(); wrongDelta.Step(offer, t.AddSeconds(1));
        wrongDelta.Step(offer with { LocalState = 4, RemoteState = 4, Confirmation = true }, t.AddSeconds(2));
        wrongDelta.Step(initial with { Wallet = initial.Wallet - 122 }, t.AddSeconds(3));
        Check(!wrongDelta.Running && wrongDelta.Done == 0, "incorrect wallet delta cannot count or repeat");
        var receive = new GilTradeRun(); Check(receive.Start(false, 0, initial, t), "receiver explicitly opts in");
        Check(receive.Step(initial, t) == GilTradeAction.None, "receiver never initiates a trade");
        var incoming = initial with { TradeOpen = true, TradePartner = 22, LocalState = 3, RemoteState = 3, RemoteGil = 450 };
        Check(receive.Step(incoming, t) == GilTradeAction.None, "receiver waits for stable offer");
        Check(receive.Step(incoming, t.AddSeconds(1)) == GilTradeAction.None, "receiver waits for sender lock");
        Check(receive.Step(incoming with { RemoteState = 4 }, t.AddSeconds(2)) == GilTradeAction.LockOffer && receive.ThisTrade == 450, "receiver accepts stable locked gil");
        Check(receive.Step(incoming with { LocalState = 4, RemoteState = 5, Confirmation = true }, t.AddSeconds(3)) == GilTradeAction.Confirm, "receiver confirms only own empty side");
        receive.Step(initial with { Wallet = initial.Wallet + 450 }, t.AddSeconds(4));
        Check(receive.Done == 450 && receive.Trades == 1, "receiver counts exact gain");
        receive.Step(initial with { Wallet = initial.Wallet + 450 }, t.AddSeconds(67));
        Check(!receive.Running, "receiver permission expires without more trades");
        foreach (var bad in new[] { incoming with { LocalGil = 1 }, incoming with { ItemsPresent = true }, incoming with { RemoteGil = 1_000_001 }, incoming with { TradePartner = 99 } })
        { var run = new GilTradeRun(); run.Start(false, 0, initial, t); run.Step(bad, t); Check(!run.Running, "receiver refuses unsafe trade"); }
        var capacity = new GilTradeRun(); var almostFull = initial with { Wallet = GilTradeRun.WalletLimit - 100 };
        capacity.Start(false, 0, almostFull, t); capacity.Step(incoming with { Wallet = almostFull.Wallet }, t);
        Check(!capacity.Running, "receiver cannot exceed wallet capacity");
        var loading = new GilTradeRun(); loading.Start(true, 123, initial, t); loading.Step(initial, t);
        Check(loading.Step(offer with { TradeReady = false, TradePartner = 0, LocalState = 0 }, t.AddSeconds(1)) == GilTradeAction.None && loading.Running, "wait for loading UI before reading partner");
        Check(loading.Step(offer with { LocalGil = 0, SyncPending = true, TradePartner = 0 }, t.AddSeconds(2)) == GilTradeAction.None && loading.Running, "wait for loading native partner");
        Check(loading.Step(offer with { LocalGil = 0 }, t.AddSeconds(3)) == GilTradeAction.SetGil, "loaded native state resumes without repeating request");
        var stuckLoad = SenderAtOffer(); stuckLoad.Step(offer with { TradeReady = false }, t.AddSeconds(62)); Check(!stuckLoad.Running, "loading state has bounded wait");
        // Two independent clients seeing the same trade must both reach final
        // confirmation without a private relay or relying on the Journal.
        var sendPeer = new GilTradeRun(); var receivePeer = new GilTradeRun();
        sendPeer.Start(true, 1, initial, t); receivePeer.Start(false, 0, initial, t);
        sendPeer.Step(initial, t);
        var wire = initial with { TradeOpen = true, TradePartner = 22, LocalState = 3, RemoteState = 3 };
        Check(sendPeer.Step(wire, t.AddMilliseconds(100)) == GilTradeAction.SetGil, "paired sender fills");
        var senderView = wire with { LocalGil = 1 }; var receiverView = wire with { RemoteGil = 1 };
        receivePeer.Step(receiverView, t.AddMilliseconds(150));
        Check(sendPeer.Step(senderView, t.AddSeconds(1)) == GilTradeAction.LockOffer, "paired sender initiates lock");
        Check(receivePeer.Step(receiverView with { RemoteState = 4 }, t.AddSeconds(1.1)) == GilTradeAction.LockOffer, "paired receiver responds to lock");
        senderView = senderView with { LocalState = 5, RemoteState = 4, Confirmation = true };
        receiverView = receiverView with { LocalState = 5, RemoteState = 4, Confirmation = true };
        Check(sendPeer.Step(senderView, t.AddSeconds(1.2)) == GilTradeAction.Confirm && receivePeer.Step(receiverView, t.AddSeconds(1.2)) == GilTradeAction.Confirm, "both clients confirm without deadlock");
        sendPeer.Step(initial with { Wallet = initial.Wallet - 1 }, t.AddSeconds(2)); receivePeer.Step(initial with { Wallet = initial.Wallet + 1 }, t.AddSeconds(2));
        Check(sendPeer.Done == 1 && receivePeer.Done == 1, "both clients count the same completed gil transfer");
        var closing = SenderAtOffer(); closing.Step(offer, t.AddSeconds(1));
        closing.Step(offer with { LocalState = 4, RemoteState = 4, Confirmation = true }, t.AddSeconds(2));
        Check(closing.Step(initial with { TradeOpen = true, TradeReady = false, TradePartner = 0, Wallet = initial.Wallet - 123 }, t.AddSeconds(3)) == GilTradeAction.None && closing.Running, "closing clears partner before window without losing receipt");
        closing.Step(initial with { Wallet = initial.Wallet - 123, PartnerNearby = false, PartnerEntity = 0 }, t.AddSeconds(4));
        Check(closing.Done == 123 && closing.Stage == GilTradeStage.Complete, "completed transfer survives partner leaving immediately afterward");
        Console.WriteLine($"Gil Trader checks passed: {count}.");
    }
}
