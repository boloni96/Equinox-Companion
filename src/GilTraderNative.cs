using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly GilTradeRun gilTrade = new();
    private ulong gilPartnerContent;
    private uint gilPartnerWorld, gilSessionWorld, gilSessionTerritory;
    private string gilPartnerName = "", gilTradePrompt = "", gilTradeNotice = "";
    private DateTimeOffset gilTradePoll;
    private readonly Queue<string> gilTradeDiagnostics = new();
    private bool gilTradeOwned;

    private void TraceGilTrade(string message)
    {
        gilTradeDiagnostics.Enqueue($"{DateTimeOffset.Now:HH:mm:ss} · {message}");
        while (gilTradeDiagnostics.Count > 20) gilTradeDiagnostics.Dequeue();
    }
    private IPlayerCharacter? GilTradePartner()
    {
        if (gilPartnerContent == 0 || !FollowParty.Any(x => x.ContentId == gilPartnerContent &&
            x.World.RowId == gilPartnerWorld && x.Name.TextValue == gilPartnerName)) return null;
        return Objects.OfType<IPlayerCharacter>().FirstOrDefault(x => x.Name.TextValue == gilPartnerName && x.HomeWorld.RowId == gilPartnerWorld && x.IsTargetable);
    }
    private unsafe GilTradeFrame ReadGilTradeFrame()
    {
        var inv = InventoryManager.Instance();
        var partner = GilTradePartner(); var self = Objects.LocalPlayer;
        var trade = SellerAddon("Trade");
        var open = trade != null || Conditions[ConditionFlag.TradeOpen];
        var yesno = (AddonSelectYesno*)SellerAddon("SelectYesno");
        var confirmation = yesno != null && yesno->PromptText != null &&
            GilTradeNative.PromptMatches(yesno->PromptText->NodeText.ToString(), gilTradePrompt);
        var ready = Player.IsLoaded && self != null && inv != null && !Conditions[ConditionFlag.InCombat] &&
            !Conditions[ConditionFlag.Unconscious] && !Conditions[ConditionFlag.BetweenAreas] && !Conditions[ConditionFlag.BetweenAreas51] &&
            !Conditions[ConditionFlag.LoggingOut] && !Conditions[ConditionFlag.Casting] && !Conditions[ConditionFlag.OccupiedSummoningBell] &&
            !Conditions[ConditionFlag.WatchingCutscene] && !Conditions[ConditionFlag.WatchingCutscene78] && !Conditions[ConditionFlag.OccupiedInCutSceneEvent] &&
            !Conditions[ConditionFlag.OccupiedInEvent] && !Conditions[ConditionFlag.OccupiedInQuestEvent] &&
            sellerRun?.Running != true && !HelperTravelBusy && !followSession.Armed && !followStopPending && !followStopUnconfirmed;
        if (gilTrade.Running) ready &= Player.CurrentWorld.RowId == gilSessionWorld && Client.TerritoryType == gilSessionTerritory;
        var nearby = self != null && partner != null && Vector3.DistanceSquared(self.Position, partner.Position) <= 25;
        var items = false; uint local = 0, remote = 0;
        if (inv != null && open) GilTradeNative.Offers(inv, out items, out local, out remote);
        var correctName = inv != null && inv->TradePartnerNameString == gilPartnerName;
        return new(Player.IsLoaded ? Player.ContentId : 0, ready, nearby, partner?.EntityId ?? 0, inv == null ? 0 : inv->GetGil(),
            open, inv == null || !correctName ? 0 : inv->TradePartnerEntityId, items, local, remote,
            inv == null ? 0 : (int)inv->TradeLocalState, inv == null ? 0 : (int)inv->TradeRemoteState,
            inv == null || inv->TradeIsSyncPending, confirmation,
            yesno != null && !confirmation || SellerAddon("SelectOk") != null || SellerAddon("SelectString") != null || SellerAddon("SelectIconString") != null,
            trade != null && inv != null);
    }
    private unsafe void StartGilTrade()
    {
        gilTradeNotice = "";
        if (!config.EnableGilTrader) return;
        gilTradePrompt = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(102223)?.Text.ExtractText() ?? "";
        if (string.IsNullOrWhiteSpace(gilTradePrompt)) { gilTradeNotice = "The game's trade confirmation text is unavailable."; return; }
        if (followSession.Armed) { gilTradeNotice = "Stop active following before starting a trade."; return; }
        gilSessionWorld = Player.CurrentWorld.RowId; gilSessionTerritory = Client.TerritoryType;
        if (!gilTrade.Start(!gilReceive, (uint)Math.Max(0, gilTotal), ReadGilTradeFrame(), DateTimeOffset.UtcNow)) return;
        gilTradeOwned = false; gilTradePoll = default;
        TraceGilTrade($"Started {(gilReceive ? "receiving" : $"sending {gilTotal:N0}")} · {gilPartnerName} @ {WorldName(gilPartnerWorld)}");
    }
    private unsafe void CancelOwnedGilTrade()
    {
        var inv = InventoryManager.Instance();
        if (gilTradeOwned && Player.IsLoaded && Player.ContentId == gilTrade.Character && inv != null &&
            inv->TradePartnerEntityId == gilTrade.PartnerEntity && inv->TradePartnerNameString == gilPartnerName &&
            (Conditions[ConditionFlag.TradeOpen] || SellerAddon("Trade") != null || (int)inv->TradeLocalState == 2)) inv->RefuseTrade();
        gilTradeOwned = false;
    }
    private void StopGilTrade(string reason)
    {
        if (!gilTrade.Running) return;
        gilTrade.Stop(reason);
        try { CancelOwnedGilTrade(); } catch (Exception ex) { Log.Warning(ex, "Gil Trader cancellation could not be confirmed"); }
        TraceGilTrade(gilTrade.Status);
    }
    private unsafe void UpdateGilTrader(DateTimeOffset now)
    {
        if (!gilTrade.Running || now < gilTradePoll) return;
        gilTradePoll = now.AddMilliseconds(150);
        try
        {
            if (!config.EnableGilTrader) { StopGilTrade("Gil Trader disabled."); return; }
            if (!gilTraderWindow.IsOpen && !minimizedLaunchers.Contains("Gil Trader")) { StopGilTrade("Gil Trader closed."); return; }
            var frame = ReadGilTradeFrame(); var before = gilTrade.Stage; var trades = gilTrade.Trades;
            if (frame.TradeOpen && frame.TradePartner == gilTrade.PartnerEntity && frame.Character == gilTrade.Character) gilTradeOwned = true;
            var action = gilTrade.Step(frame, now);
            if (trades != gilTrade.Trades) TraceGilTrade($"Completed trade {gilTrade.Trades} · total {gilTrade.Done:N0} gil");
            if (action != GilTradeAction.None)
            {
                var inv = InventoryManager.Instance();
                if (inv == null) { StopGilTrade("The game's trade manager is unavailable."); return; }
                // All reads and actions stay on the framework thread. Nothing is
                // queued for a later frame after identity/amount checks have passed.
                switch (action)
                {
                    case GilTradeAction.Request:
                        if (GilTradePartner() is not { } target) { StopGilTrade("Selected party member is no longer nearby."); return; }
                        Targets.Target = target; gilTradeOwned = true; inv->SendTradeRequest(target.EntityId); break;
                    case GilTradeAction.SetGil: inv->SetTradeGilAmount(gilTrade.ThisTrade); break;
                    case GilTradeAction.LockOffer:
                        var addon = SellerAddon("Trade");
                        if (!GilTradeNative.TradeButtonEvent(addon, out var click)) { StopGilTrade("The game's Trade button is unavailable; no confirmation was sent."); return; }
                        var data = new AtkEventData(); addon->ReceiveEvent(click.State.EventType, (int)click.Param, &click, &data); break;
                    case GilTradeAction.Confirm:
                        var dialog = (AddonSelectYesno*)SellerAddon("SelectYesno");
                        if (dialog == null || dialog->PromptText == null || dialog->YesButton == null || !dialog->YesButton->IsEnabled ||
                            !GilTradeNative.PromptMatches(dialog->PromptText->NodeText.ToString(), gilTradePrompt))
                        { StopGilTrade("Trade confirmation is no longer available."); return; }
                        dialog->FireCallbackInt(0); break;
                }
                TraceGilTrade($"{action} · {gilTrade.ThisTrade:N0} gil · native states {frame.LocalState}/{frame.RemoteState}");
            }
            if (!gilTrade.Running)
            {
                CancelOwnedGilTrade(); TraceGilTrade(gilTrade.Status);
                PrintEquinox("Gil Trader · " + gilTrade.Status);
            }
            else if (before != gilTrade.Stage && action == GilTradeAction.None) TraceGilTrade(gilTrade.Status);
        }
        catch (Exception ex)
        {
            StopGilTrade("Stopped: the game's trade state could not be read safely.");
            errorJournal.Record("gil-trader", "Gil Trader stopped; no automatic retry.", exceptionType: ex.GetType().Name);
            Log.Error(ex, "Gil Trader stopped");
        }
    }
}
