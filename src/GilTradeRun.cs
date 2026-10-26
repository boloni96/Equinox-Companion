namespace EquinoxCompanion;

internal enum GilTradeAction { None, Request, SetGil, LockOffer, Confirm }
internal enum GilTradeStage { Stopped, Waiting, Opening, Offering, Locking, Confirming, Settling, Between, Complete }
internal sealed record GilTradeFrame(
    ulong Character, bool Ready, bool PartnerNearby, uint PartnerEntity, uint Wallet,
    bool TradeOpen, uint TradePartner, bool ItemsPresent, uint LocalGil, uint RemoteGil,
    int LocalState, int RemoteState, bool SyncPending, bool Confirmation, bool OtherDialog, bool TradeReady = true);

// No native pointers, persisted permission or network requests. One explicit Start
// authorizes one direction with one player on this character, for this session.
internal sealed class GilTradeRun
{
    internal const uint TradeLimit = 1_000_000, WalletLimit = 999_999_999;
    internal GilTradeStage Stage { get; private set; }
    internal bool Running => Stage is not (GilTradeStage.Stopped or GilTradeStage.Complete);
    internal bool Sending { get; private set; }
    internal ulong Character { get; private set; }
    internal uint PartnerEntity { get; private set; }
    internal uint Total { get; private set; }
    internal uint Done { get; private set; }
    internal int Trades { get; private set; }
    internal uint ThisTrade { get; private set; }
    internal string Status { get; private set; } = "Choose a player nearby.";
    internal bool FinalConfirmationSent { get; private set; }
    private uint walletBefore, stableOffer;
    private DateTimeOffset started, stageAt, stableAt;

    internal bool Start(bool sending, uint total, GilTradeFrame frame, DateTimeOffset now)
    {
        if (Running) return false;
        if (frame.Character == 0 || !frame.Ready || !frame.PartnerNearby || frame.PartnerEntity == 0)
        { Status = "Stand beside the selected player before starting."; return false; }
        if (frame.TradeOpen || frame.Confirmation || frame.OtherDialog)
        { Status = "Close the existing trade or dialog before starting."; return false; }
        if (sending && (total == 0 || total > WalletLimit || total > frame.Wallet))
        { Status = "Enter a total you have in your character’s wallet."; return false; }
        if (!sending && frame.Wallet >= WalletLimit)
        { Status = "Your wallet is full."; return false; }
        Sending = sending; Total = sending ? total : 0; Done = 0; Trades = 0; ThisTrade = 0;
        Character = frame.Character; PartnerEntity = frame.PartnerEntity; walletBefore = frame.Wallet;
        FinalConfirmationSent = false; stableOffer = 0; started = now;
        Move(GilTradeStage.Waiting, now, sending ? "Ready to send." : "Waiting for your selected player…");
        return true;
    }

    internal void Stop(string reason)
    {
        Stage = GilTradeStage.Stopped;
        Status = reason + (FinalConfirmationSent ? " The last trade may still settle; check your wallet before starting again." : "");
    }
    private void Move(GilTradeStage stage, DateTimeOffset now, string status) { Stage = stage; stageAt = now; Status = status; }
    private GilTradeAction Halt(string why) { Stop(why); return GilTradeAction.None; }
    internal GilTradeAction Step(GilTradeFrame f, DateTimeOffset now)
    {
        if (!Running) return GilTradeAction.None;
        if (f.Character != Character) return Halt("Stopped: character changed.");

        // After our final Yes, only an exact wallet delta AND a closed trade can
        // credit a transfer. A click, closed/cancelled window or unrelated gain cannot.
        if (Stage == GilTradeStage.Settling)
        {
            var delta = Sending ? (long)walletBefore - f.Wallet : (long)f.Wallet - walletBefore;
            if (delta != 0 && delta != ThisTrade) return Halt("Stopped: unexpected wallet change; completion is unverified.");
            if (!f.TradeOpen && !f.Confirmation && delta == ThisTrade)
            {
                Done += ThisTrade; Trades++; walletBefore = f.Wallet; FinalConfirmationSent = false;
                ThisTrade = 0; stableOffer = 0;
                if (Sending && Done == Total) Move(GilTradeStage.Complete, now, $"Sent {Done:N0} gil · {Trades} completed trades.");
                else if (!f.Ready || !f.PartnerNearby || f.PartnerEntity != PartnerEntity || f.OtherDialog)
                    Stop("Trade completed. Stopped because the character or selected player is no longer ready.");
                else Move(GilTradeStage.Between, now, Sending ? "Trade completed; preparing the next one…" : $"Received {Done:N0} gil · waiting for the next trade…");
            }
            else if (now - stageAt > TimeSpan.FromSeconds(12)) return Halt("Stopped: completion was not confirmed.");
            return GilTradeAction.None;
        }
        if (!f.Ready) return Halt("Stopped: game state changed.");
        if (!f.PartnerNearby || f.PartnerEntity != PartnerEntity) return Halt("Stopped: the selected player left or moved away.");
        if (now - started > TimeSpan.FromMinutes(30)) return Halt("Stopped: session expired.");
        if (f.OtherDialog) return Halt("Stopped: another game dialog needs your attention.");
        if (f.TradeOpen && (!f.TradeReady || f.SyncPending))
        {
            if (now - stageAt > TimeSpan.FromSeconds(60)) return Halt("Stopped: the game's trade data did not finish loading.");
            return GilTradeAction.None;
        }
        if (f.TradeOpen && f.TradePartner != PartnerEntity) return Halt("Stopped: a different character opened a trade.");
        if (f.TradeOpen && (f.LocalState is < 1 or > 6 || f.RemoteState is < 1 or > 6)) return Halt("Stopped: unknown native trade state.");
        if (f.TradeOpen && f.ItemsPresent) return Halt("Stopped: this tool trades gil only. An item was added.");
        if (f.TradeOpen && (Sending ? f.RemoteGil != 0 : f.LocalGil != 0))
            return Halt("Stopped: gil was added on the wrong side of the trade.");
        if (f.Wallet != walletBefore) return Halt("Stopped: your wallet changed outside a confirmed trade.");
        if (now - stageAt > TimeSpan.FromSeconds(60)) return Halt("Stopped: no trade progress for 60 seconds.");
        if (Stage == GilTradeStage.Between)
        {
            if (now - stageAt < TimeSpan.FromSeconds(2)) return GilTradeAction.None;
            Move(GilTradeStage.Waiting, now, Sending ? "Ready for the next trade." : "Waiting for your selected player…");
        }
        if (Stage == GilTradeStage.Waiting)
        {
            if (f.TradeOpen)
            {
                if (Sending) return Halt("Stopped: an unexpected trade opened before the request.");
                Move(GilTradeStage.Offering, now, "Waiting for their gil offer…");
            }
            else if (Sending)
            {
                ThisTrade = Math.Min(TradeLimit, Total - Done);
                if (ThisTrade > f.Wallet) return Halt("Stopped: not enough gil for the next trade.");
                Move(GilTradeStage.Opening, now, $"Requesting a trade for {ThisTrade:N0} gil…");
                return GilTradeAction.Request;
            }
            else return GilTradeAction.None;
        }
        if (Stage == GilTradeStage.Opening)
        {
            if (!f.TradeOpen) return GilTradeAction.None;
            if (f.LocalGil != 0 || f.RemoteGil != 0) return Halt("Stopped: the new trade already contains gil.");
            if (f.SyncPending || f.LocalState != 3) return GilTradeAction.None;
            Move(GilTradeStage.Offering, now, $"Offering {ThisTrade:N0} gil…");
            return GilTradeAction.SetGil;
        }
        if (!f.TradeOpen) return Halt("Stopped: the trade closed or was cancelled before confirmation.");
        if (f.SyncPending) return GilTradeAction.None;
        if (Stage == GilTradeStage.Offering)
        {
            if (Sending)
            {
                if (f.LocalGil != ThisTrade)
                {
                    if (f.LocalGil != 0 || now - stageAt > TimeSpan.FromSeconds(5)) return Halt("Stopped: the game did not keep the requested gil amount.");
                    return GilTradeAction.None;
                }
                if (now - stageAt < TimeSpan.FromMilliseconds(800)) return GilTradeAction.None;
            }
            else
            {
                if (f.RemoteGil == 0) return GilTradeAction.None;
                if (f.RemoteGil > TradeLimit || (ulong)f.Wallet + f.RemoteGil > WalletLimit) return Halt("Stopped: this gil offer exceeds your wallet space.");
                if (f.RemoteGil != stableOffer) { stableOffer = f.RemoteGil; stableAt = now; }
                if (now - stableAt < TimeSpan.FromMilliseconds(800) || f.RemoteState < 4) return GilTradeAction.None;
                ThisTrade = f.RemoteGil;
            }
            if (f.LocalState != 3) return Halt("Stopped: the trade was changed outside Gil Trader.");
            Move(GilTradeStage.Locking, now, "Waiting for both sides to accept…");
            return GilTradeAction.LockOffer;
        }
        if ((Sending ? f.LocalGil : f.RemoteGil) != ThisTrade) return Halt("Stopped: the gil offer changed after accepting.");
        if (Stage == GilTradeStage.Locking)
        {
            if (f.LocalState < 4)
            {
                if (now - stageAt > TimeSpan.FromSeconds(5)) return Halt("Stopped: the game did not accept the offer.");
                return GilTradeAction.None;
            }
            Move(GilTradeStage.Confirming, now, "Waiting for trade confirmation…");
        }
        if (Stage == GilTradeStage.Confirming)
        {
            if (f.LocalState < 4) return Halt("Stopped: the accepted offer was unlocked.");
            if (f.RemoteState < 4 || !f.Confirmation) return GilTradeAction.None;
            FinalConfirmationSent = true;
            Move(GilTradeStage.Settling, now, "Confirming the gil transfer…");
            return GilTradeAction.Confirm;
        }
        return GilTradeAction.None;
    }
}
