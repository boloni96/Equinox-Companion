using EquinoxCompanion;
using System.Runtime.CompilerServices;

internal static class GilTrade254Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Gil completion: " + why); }
        var t = DateTimeOffset.Parse("2026-10-26T08:04:24Z");
        var idle = new GilTradeFrame(11, true, true, 22, 10_000_000, false, 0, false, 0, 0, 1, 1, false, false, false);
        var offered = idle with { TradeOpen = true, TradePartner = 22, LocalState = 3, RemoteState = 4, RemoteGil = 1_000_000 };
        GilTradeRun Receiver()
        {
            var r = new GilTradeRun(); Check(r.Start(false, 0, idle, t), "receiver starts");
            r.Step(offered, t);
            Check(r.Step(offered, t.AddSeconds(1)) == GilTradeAction.LockOffer, "receiver locks observed amount");
            Check(r.Step(offered with { LocalState = 4 }, t.AddSeconds(1.1)) == GilTradeAction.None && r.Stage == GilTradeStage.Confirming,
                "both offers locked without clicking final Yes");
            return r;
        }
        var both = offered with { LocalState = 5, RemoteState = 6 };
        var closedPaid = idle with { Wallet = idle.Wallet + 1_000_000 };

        // Reported .253 sequence: 4/4 -> 5/5 -> 5/6 -> closed with wallet gain.
        // The final Yes may be handled between polls, so no popup is sampled.
        var receiver = Receiver();
        Check(receiver.Step(both with { RemoteState = 5 }, t.AddSeconds(1.2)) == GilTradeAction.None, "wait without popup");
        Check(receiver.Step(both, t.AddSeconds(1.3)) == GilTradeAction.None, "other side confirms first");
        Check(receiver.Step(closedPaid with { CompletionObserved = true }, t.AddSeconds(2)) == GilTradeAction.None &&
            receiver.Done == 1_000_000 && receiver.Trades == 1 && receiver.Stage == GilTradeStage.Between,
            "typed receipt and exact gain credit receiver without own Yes");
        receiver.Step(closedPaid with { CompletionObserved = true }, t.AddSeconds(2.1));
        Check(receiver.Trades == 1, "duplicate receipt cannot count twice");
        var second = offered with { Wallet = closedPaid.Wallet, RemoteGil = 1 };
        receiver.Step(second, t.AddSeconds(4));
        Check(receiver.Step(second, t.AddSeconds(5)) == GilTradeAction.LockOffer && receiver.ThisTrade == 1,
            "receiver remains armed for the one-gil remainder");
        receiver.Step(second with { LocalState = 5, RemoteState = 5 }, t.AddSeconds(5.1));
        receiver.Step(idle with { Wallet = closedPaid.Wallet + 1, CompletionObserved = true }, t.AddSeconds(6));
        Check(receiver.Done == 1_000_001 && receiver.Trades == 2, "consecutive receiver transfers retain exact total");

        var late = Receiver(); late.Step(both, t.AddSeconds(1.2));
        late.Step(closedPaid, t.AddSeconds(2));
        Check(late.Stage == GilTradeStage.Settling && late.Done == 0, "wallet arriving before receipt waits without counting");
        late.Step(closedPaid with { CompletionObserved = true }, t.AddSeconds(2.2));
        Check(late.Done == 1_000_000 && late.Running, "late receipt reconciles expected transfer");

        var early = Receiver();
        early.Step(both with { CompletionObserved = true }, t.AddSeconds(1.2));
        Check(early.Stage == GilTradeStage.Settling && early.Done == 0, "receipt alone cannot credit transfer");
        early.Step(both with { Wallet = closedPaid.Wallet }, t.AddSeconds(1.3));
        Check(early.Done == 0, "receipt and wallet still need closed trade");
        early.Step(closedPaid, t.AddSeconds(2));
        Check(early.Done == 1_000_000, "receipt can precede wallet and window close");

        var cleanup = Receiver();
        cleanup.Step(closedPaid with { TradeOpen = true, TradeReady = true, CompletionObserved = true }, t.AddSeconds(2));
        Check(cleanup.Stage == GilTradeStage.Settling && cleanup.Done == 0, "cleared native fields during window cleanup are tolerated");
        cleanup.Step(closedPaid, t.AddSeconds(2.2)); Check(cleanup.Done == 1_000_000, "cleanup completes only after close");

        var native = Receiver();
        Check(native.Step(both with { LocalState = 6 }, t.AddSeconds(1.2)) == GilTradeAction.None && native.Stage == GilTradeStage.Settling,
            "observed native confirmation never sends a second Yes");
        native.Step(closedPaid, t.AddSeconds(2)); Check(native.Done == 0, "native acceptance alone is not a completion receipt");
        native.Step(closedPaid with { CompletionObserved = true }, t.AddSeconds(2.2)); Check(native.Done == 1_000_000, "native confirmation plus receipt settles");

        var unverified = Receiver(); unverified.Step(both, t.AddSeconds(1.2));
        unverified.Step(closedPaid, t.AddSeconds(2)); unverified.Step(closedPaid, t.AddSeconds(15));
        Check(!unverified.Running && unverified.Done == 0, "unrelated matching gain without completion receipt never counts");
        var cancelled = Receiver(); cancelled.Step(both, t.AddSeconds(1.2));
        cancelled.Step(idle, t.AddSeconds(2)); cancelled.Step(idle, t.AddSeconds(15));
        Check(!cancelled.Running && cancelled.Done == 0, "cancelled locked trade does not count or restart");
        var incorrect = Receiver(); incorrect.Step(closedPaid with { Wallet = idle.Wallet + 999_999, CompletionObserved = true }, t.AddSeconds(2));
        Check(!incorrect.Running && incorrect.Done == 0, "receipt cannot justify wrong wallet change");
        var changed = Receiver(); changed.Step(both with { RemoteGil = 999_999, CompletionObserved = true }, t.AddSeconds(2));
        Check(!changed.Running && changed.Done == 0, "receipt cannot bypass changed open offer");
        var item = Receiver(); item.Step(both with { ItemsPresent = true, CompletionObserved = true }, t.AddSeconds(2));
        Check(!item.Running && item.Done == 0, "receipt cannot bypass item in live trade");
        var stranger = Receiver(); stranger.Step(both with { TradePartner = 33, CompletionObserved = true }, t.AddSeconds(2));
        Check(!stranger.Running && stranger.Done == 0, "receipt cannot bypass changed live partner");
        var logout = Receiver(); logout.Step(closedPaid with { Character = 0, CompletionObserved = true }, t.AddSeconds(2));
        Check(!logout.Running && logout.Done == 0, "receipt cannot cross character change");
        var stop = Receiver(); stop.Stop("Stopped"); stop.Step(closedPaid with { CompletionObserved = true }, t.AddSeconds(2));
        Check(!stop.Running && stop.Done == 0, "Stop revokes completion handling and continuation");

        var sender = new GilTradeRun(); sender.Start(true, 1_000_000, idle, t); sender.Step(idle, t);
        var sendOffer = offered with { LocalGil = 0, RemoteGil = 0, RemoteState = 3 };
        sender.Step(sendOffer, t.AddMilliseconds(100)); sendOffer = sendOffer with { LocalGil = 1_000_000 };
        sender.Step(sendOffer, t.AddSeconds(1)); sender.Step(sendOffer with { LocalState = 5, RemoteState = 5 }, t.AddSeconds(1.2));
        sender.Step(idle with { Wallet = idle.Wallet - 1_000_000, CompletionObserved = true }, t.AddSeconds(2));
        Check(sender.Done == 1_000_000 && sender.Stage == GilTradeStage.Complete, "externally confirmed sender also counts exact loss");

        var capture = new GilTradeCompletionCapture();
        capture.Observe(38, t); Check(!capture.Completed(11, 22, 1_000_000, t), "unsolicited completed event ignored");
        capture.Arm(11, 22, 1_000_000, t);
        capture.Observe(38, t.AddSeconds(-1)); Check(!capture.Completed(11, 22, 1_000_000, t), "receipt before scope ignored");
        capture.Observe(35, t); Check(!capture.Completed(11, 22, 1_000_000, t), "trade opened is not completed");
        capture.Observe(38, t.AddSeconds(1));
        Check(capture.Completed(11, 22, 1_000_000, t.AddSeconds(2)), "typed trade-complete event captured");
        Check(!capture.Completed(12, 22, 1_000_000, t.AddSeconds(2)) && !capture.Completed(11, 23, 1_000_000, t.AddSeconds(2)) &&
            !capture.Completed(11, 22, 1, t.AddSeconds(2)), "receipt bound to character partner and locked amount");
        capture.Observe(38, t.AddSeconds(11));
        Check(!capture.Completed(11, 22, 1_000_000, t.AddSeconds(14)), "duplicate cannot extend receipt lifetime");
        capture.Arm(11, 22, 1_000_000, t.AddSeconds(15));
        Check(!capture.Completed(11, 22, 1_000_000, t.AddSeconds(16)), "next trade cannot reuse prior receipt");
        foreach (uint failed in new uint[] { 36, 39, 40, 42, 43, 46, 47, 54 })
        {
            capture.Arm(11, 22, 1_000_000, t); capture.Observe(failed, t.AddSeconds(1)); capture.Observe(38, t.AddSeconds(2));
            Check(!capture.Completed(11, 22, 1_000_000, t.AddSeconds(3)), "cancelled or failed scope cannot acquire later receipt");
        }
        capture.Arm(11, 22, 1_000_000, t); capture.Clear(); capture.Observe(38, t.AddSeconds(1));
        Check(!capture.Completed(11, 22, 1_000_000, t.AddSeconds(2)), "stop clears receipt scope");
        Console.WriteLine($"Gil Trader completion receipt checks passed: {count}.");
    }
}
