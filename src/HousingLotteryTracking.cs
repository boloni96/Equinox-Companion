using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly LotteryLogin lotteryLogin=new();
    private readonly LotteryBidNoticeGate lotteryBidNotices=new();
    private DateTimeOffset nextLotteryReminderCheck,nextLotteryVisibleCheck;
    private object? lotteryDiagnostic;
    private LotteryResult? lotteryLastResult;
    private (LotteryBid Bid,ulong Character,uint World,ulong Target,long Amount,DateTimeOffset At)? lotteryRefund;
    private (LotteryBid Bid, ulong Character, ulong Target, uint World, DateTimeOffset At)? lotteryPlacard;
    private LotteryBuyerContext? lotteryBuyerContext;
    private void InitializeLottery()
    {
        foreach(var name in new[]{"ContentsInfoDetail","SelectYesNoTextScroll","SelectYesno","SelectOk","SelectString","Talk","HousingSignBoard"})
        { Addons.RegisterListener(AddonEvent.PostSetup,name,ObserveLotteryAddon); Addons.RegisterListener(AddonEvent.PostRefresh,name,ObserveLotteryAddon); }
        Chat.ChatMessage+=ObserveLotteryChat;
    }
    private void DisposeLottery(){Addons.UnregisterListener(ObserveLotteryAddon);Chat.ChatMessage-=ObserveLotteryChat;}
    private bool LotteryLoaded => Player.IsLoaded && Player.ContentId!=0 && !Conditions[ConditionFlag.BetweenAreas] && !Conditions[ConditionFlag.BetweenAreas51];
    private void UpdateLottery(DateTimeOffset now)
    {
        var id=Player.IsLoaded?Player.ContentId:0;
        if(id==0){lotteryBidNotices.Reset();lotteryLogin.Reset();lotteryPlacard=null;lotteryRefund=null;lotteryBuyerContext=null;return;}
        if(LotteryLoaded&&config.SyncHouseDetails&&now>=nextLotteryVisibleCheck){nextLotteryVisibleCheck=now.AddMilliseconds(500);ObserveSafely("housing-lottery",()=>PollLotteryViews(now));}
        lotteryBidNotices.Due(id,"",now);
        if(lotteryLogin.Due(id,now,config.NotifyHousingLottery,!LotteryLoaded))
        {
            var p=HousingLottery.Period(now);var left=p.End-now;
            PrintEquinox($"[Equinox] Housing lottery · scheduled {(p.Entry?"entry period":"results period (entries closed)")} · ends {HousingLotteryNotices.LocalTime(p.End)} (local time; {(int)left.TotalDays}d {left.Hours}h {left.Minutes}m). {(p.Entry?"Place bids at the plot placard.":"Check your bid's placard for the result and claim any win.")} Duty → Timers → Estate confirms the game status.");
        }
        if(!config.NotifyHousingLottery||!LotteryLoaded||now<nextLotteryReminderCheck)return;
        nextLotteryReminderCheck=now.AddSeconds(15);
        foreach(var notice in HousingLotteryNotices.Messages(HousingLotteryNotices.WithShared(config.Discoveries,config.SharedRoster),id.ToString(),now))
            if(lotteryBidNotices.Due(id,notice.Key,now))PrintEquinox(notice.Text);
    }

    private static unsafe string LotteryText(AtkUnitBase* addon,int i)
    {
        if(addon==null||addon->AtkValues==null||i<0||i>=addon->AtkValuesCount)return "";
        var v=&addon->AtkValues[i];return ((int)v->Type&15) is 8 or 10 ?LotteryMenuText.Read(v->String.Value):"";
    }
    private unsafe void ObserveLotteryAddon(AddonEvent type,AddonArgs args)
    {
        if(!LotteryLoaded||!config.SyncHouseDetails||Client.ClientLanguage!=Dalamud.Game.ClientLanguage.English||args.Addon.IsNull)return;
        try
        {
            var addon=(AtkUnitBase*)args.Addon.Address;var now=DateTimeOffset.UtcNow;
            if(args.AddonName=="HousingSignBoard")
            {
                CaptureLotteryPlacard(addon,now);
            }
            else if(args.AddonName=="ContentsInfoDetail")CaptureLotteryTimers(addon,now);
            else if(args.AddonName is "SelectYesNoTextScroll" or "SelectYesno")CaptureLotteryBuyer(addon,now);
            if(args.AddonName is "HousingSignBoard" or "SelectYesno" or "SelectOk" or "SelectString" or "Talk" or "SelectYesNoTextScroll")
                ObservePublicLotteryResult(addon,now);
            if(args.AddonName is "SelectYesno" or "SelectOk" or "SelectString" or "Talk" or "SelectYesNoTextScroll" && HousingLottery.Result(LotteryVisibleText(addon)) is {} result)
            {
                // Only connect a personal result dialog to a previously observed entry
                // at the exact targeted plot. Never infer a result from a public winner.
                var target=Targets.Target;
                if(target is null||lotteryPlacard is not {} plot||plot.Character!=Player.ContentId||plot.Target!=target.GameObjectId||plot.World!=Player.CurrentWorld.RowId||now-plot.At>TimeSpan.FromMinutes(1))return;
                var matches=HousingLotteryNotices.WithShared(config.Discoveries,config.SharedRoster).Where(e=>e.Kind=="housing.lottery"&&e.Actor.ContentId==Player.ContentId.ToString()&&e.Lottery is {} b&&b.District==plot.Bid.District&&b.Ward==plot.Bid.Ward&&b.Plot==plot.Bid.Plot&&e.Actor.HomeWorldId==plot.World&&DateTimeOffset.TryParse(b.CycleStart,out var cycle)&&cycle==HousingLottery.Period(now).Start&&!HousingLottery.Period(now).Entry).GroupBy(e=>(e.Lottery!.CycleStart,e.Lottery.Entry)).Select(g=>g.MaxBy(e=>e.At)!).Where(e=>e.Lottery!.Status=="entered").ToArray();
                if(matches.Length==1)RecordLottery(matches[0].Lottery! with {Status=result,Evidence="result-dialog"},now);
            }
        }
        catch(Exception ex){errorJournal.Record("housing-lottery","Lottery view could not be read; existing entries kept.",exceptionType:ex.GetType().Name);}
    }
    private unsafe void CaptureLotteryTimers(AtkUnitBase* addon,DateTimeOffset now)
    {
        var bid=HousingLottery.ParseTimers(LotteryVisibleText(addon),now);
        if(bid is not null)RecordLottery(bid,now);
    }
    private unsafe void CaptureLotteryBuyer(AtkUnitBase* addon,DateTimeOffset now)
    {
        var text=LotteryVisibleText(addon);
        if(!HousingLottery.EntryConfirmation(text))return;
        lotteryBuyerContext=null;
        var buyer=HousingLottery.Buyer(text);
        var board=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSignBoard").Address;
        if(board!=null&&board->IsVisible)CaptureLotteryPlacard(board,now);
        if(lotteryPlacard is not {} plot||plot.Character!=Player.ContentId||plot.World!=Player.CurrentWorld.RowId||
            Targets.Target?.GameObjectId!=plot.Target||now-plot.At>TimeSpan.FromMinutes(1))
        {
            lotteryDiagnostic=new{at=now,state="Entry type seen; exact placard context unavailable",buyer};
            return;
        }
        lotteryBuyerContext=new(Player.ContentId,plot.World,plot.Bid.District,plot.Bid.Ward,plot.Bid.Plot,plot.Bid.CycleStart,buyer,now);
        lotteryDiagnostic=new{at=now,state="Entry type captured; awaiting submission receipt",buyer,plot.Bid.District,plot.Bid.Ward,plot.Bid.Plot,plot.World};
    }
    private unsafe void CaptureLotteryPlacard(AtkUnitBase* addon,DateTimeOffset now)
    {
        if(addon==null||!addon->IsVisible)return;
        var address=HousingLottery.PlacardAddress(LotteryVisibleText(addon),now);
        if(address is not null&&Targets.Target is {} sign)
        {
            lotteryPlacard=(address,Player.ContentId,sign.GameObjectId,Player.CurrentWorld.RowId,now);
            lotteryDiagnostic=new{at=now,state="Placard address captured",address.District,address.Ward,address.Plot,world=Player.CurrentWorld.RowId};
        }
        else {lotteryPlacard=null;lotteryDiagnostic=new{at=now,state="Visible placard address or target unavailable"};}
    }
    private unsafe void PollLotteryViews(DateTimeOffset now)
    {
        if(Client.ClientLanguage!=Dalamud.Game.ClientLanguage.English)return;
        // Timer text can arrive after PostSetup/Refresh. It must not depend on
        // an open placard: it also repairs the buyer of already saved tickets.
        var timers=(AtkUnitBase*)GardenGui.GetAddonByName("ContentsInfoDetail").Address;
        if(timers!=null&&timers->IsVisible)CaptureLotteryTimers(timers,now);
        var board=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSignBoard").Address;
        if(board!=null&&board->IsVisible)CaptureLotteryPlacard(board,now);
        if(lotteryPlacard is not {} plot||plot.Character!=Player.ContentId||plot.World!=Player.CurrentWorld.RowId||Targets.Target?.GameObjectId!=plot.Target||now-plot.At>TimeSpan.FromMinutes(1)){lotteryPlacard=null;return;}
        foreach(var name in new[]{"SelectOk","SelectYesno","SelectString","Talk","SelectYesNoTextScroll"})
        {
            var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
            if(addon!=null&&addon->IsVisible)
            {
                if(name is "SelectYesno" or "SelectYesNoTextScroll")CaptureLotteryBuyer(addon,now);
                ObservePublicLotteryResult(addon,now);
            }
        }
    }
    private static unsafe string LotteryVisibleText(AtkUnitBase* addon)
    {
        var texts=new List<string>();
        for(var i=0;i<Math.Min((int)addon->AtkValuesCount,256);i++)
        { var text=LotteryText(addon,i);if(text.Length>0)texts.Add(text); }
        if(addon->UldManager.NodeList!=null&&addon->UldManager.NodeListCount<=512)
            for(var i=0;i<addon->UldManager.NodeListCount;i++)
            {
                var node=addon->UldManager.NodeList[i];
                if(node!=null&&node->Type==NodeType.Text&&node->IsVisible())texts.Add(LotteryMenuText.Read(((AtkTextNode*)node)->NodeText.StringPtr));
            }
        return string.Join("\n",texts.Distinct());
    }
    private unsafe bool ObservePublicLotteryResult(AtkUnitBase* addon,DateTimeOffset now)
    {
        var visible=LotteryVisibleText(addon);
        ObserveLotteryRefundPrompt(visible,now);
        var winner=HousingLottery.WinningNumber(visible);
        if(winner is null)return false;
        var board=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSignBoard").Address;
        if(board!=null&&board->IsVisible)CaptureLotteryPlacard(board,now);
        var period=HousingLottery.Period(now);
        if(period.Entry||lotteryPlacard is not {} plot||plot.Character!=Player.ContentId||plot.World!=Player.CurrentWorld.RowId||Targets.Target?.GameObjectId!=plot.Target||now-plot.At>TimeSpan.FromMinutes(1)){lotteryDiagnostic=new{at=now,state="Winning number seen; exact current placard context unavailable",winner};return false;}
        var actor=ReadActor();if(!SyncValidation.ActorReady(actor))return false;
        lotteryDiagnostic=new{at=now,state="Winning number matched to placard",winner,plot.World,plot.Bid.District,plot.Bid.Ward,plot.Bid.Plot};
        var result=new LotteryResult(plot.Bid.District,plot.Bid.Ward,plot.Bid.Plot,plot.World,winner.Value,period.Start.ToString("O"));
        if(lotteryLastResult==result)return true;
        if(config.Discoveries.Any(e=>e.Kind=="housing.lottery-result"&&e.LotteryResult==result)){lotteryLastResult=result;return true;}
        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"housing.lottery-result",now,WithWorldNames(actor),null,LotteryResult:result));
        lotteryLastResult=result;
        return true;
    }
    private void ObserveLotteryRefundPrompt(string text,DateTimeOffset now)
    {
        if(HousingLottery.RefundAmount(text) is not {} amount||HousingLottery.WinningNumber(text) is not {} winner)return;
        if(lotteryPlacard is not {} plot||plot.Character!=Player.ContentId||plot.World!=Player.CurrentWorld.RowId||Targets.Target?.GameObjectId!=plot.Target||now-plot.At>TimeSpan.FromMinutes(1))return;
        var own=HousingLotteryNotices.WithShared(config.Discoveries,config.SharedRoster)
            .Where(e=>e.Kind=="housing.lottery"&&e.Actor.ContentId==Player.ContentId.ToString()&&e.Actor.HomeWorldId==plot.World&&e.Lottery is {} b&&b.District==plot.Bid.District&&b.Ward==plot.Bid.Ward&&b.Plot==plot.Bid.Plot&&DateTimeOffset.TryParse(b.CycleStart,out var start)&&now>=start.AddDays(5)&&now<start.AddDays(95))
            .GroupBy(e=>(e.Lottery!.CycleStart,e.Lottery.Entry)).Select(g=>g.MaxBy(e=>e.At)!).ToArray();
        // A visible refund offer is not a receipt. Wait for its matching gil gain.
        if(own.Length!=1||own[0].Lottery is not {} bid||bid.Entry==winner||bid.Status is "won" or "forfeited"||bid.Evidence is "manual-refund" or "refund-receipt")return;
        lotteryRefund=(bid with {Status="lost",Evidence="refund-receipt"},Player.ContentId,plot.World,plot.Target,amount,now);
        lotteryDiagnostic=new{at=now,state="Refund offered; waiting for gil receipt",amount,bid.District,bid.Ward,bid.Plot,bid.Entry};
    }
    private void ObserveLotteryRefundReceipt(string text,DateTimeOffset now)
    {
        if(lotteryRefund is not {} pending||HousingLottery.ReceivedGil(text) is not {} amount)return;
        lotteryRefund=null;
        if(!HousingLottery.RefundReceiptMatches(pending.Character,pending.World,pending.Target,pending.Amount,pending.At,Player.ContentId,Player.CurrentWorld.RowId,Targets.Target?.GameObjectId,amount,now))return;
        RecordLottery(pending.Bid,now);
        lotteryDiagnostic=new{at=now,state="Refund collected; matching gil receipt",amount,pending.Bid.District,pending.Bid.Ward,pending.Bid.Plot,pending.Bid.Entry};
        nextRosterRead=default;
    }
    private void ObserveLotteryChat(IHandleableChatMessage message)
    {
        if(!LotteryLoaded||!config.SyncHouseDetails||message.LogKind is not (XivChatType.SystemMessage or XivChatType.Notice or XivChatType.LootNotice or XivChatType.Item)||message.OriginalSender.ExtractText().Length!=0)return;
        var text=message.OriginalMessage.ExtractText();
        ObserveLotteryRefundReceipt(text,DateTimeOffset.UtcNow);
        var m=Regex.Match(text,@"^You (?:have )?submitted a lottery entry for plot (\d+), ward (\d+), ([^.]+)\. Your lottery number is (\d+)",RegexOptions.IgnoreCase);
        if(!m.Success)return;var now=DateTimeOffset.UtcNow;
        var bid=HousingLottery.Parse($"Plot {m.Groups[1].Value}, {m.Groups[2].Value} Ward, {m.Groups[3].Value}",$"Lottery Number: {m.Groups[4].Value}","unknown",now,"submission");
        if(bid is not null)
        {
            var buyer=lotteryBuyerContext?.Match(Player.ContentId,Player.CurrentWorld.RowId,bid,now)??"unknown";
            bid=bid with {Buyer=buyer};
            RecordLottery(bid,now);
            lotteryDiagnostic=new{at=now,state="Entry receipt recorded",buyer,bid.District,bid.Ward,bid.Plot,bid.Entry};
        }
        lotteryBuyerContext=null;
    }
    private void RecordLottery(LotteryBid bid,DateTimeOffset now)
    {
        var actor=ReadActor();if(!SyncValidation.ActorReady(actor)||actor.HomeWorldId!=actor.CurrentWorldId)return;
        var prior=config.Discoveries.LastOrDefault(e=>e.Kind=="housing.lottery"&&e.Actor.ContentId==actor.ContentId&&e.Lottery is {} b&&b.District==bid.District&&b.Ward==bid.Ward&&b.Plot==bid.Plot&&b.Entry==bid.Entry&&b.CycleStart==bid.CycleStart);
        if(prior?.Lottery is {} old && bid.Evidence=="timers")bid=bid with {CycleStart=old.CycleStart,Status=old.Status,Buyer=bid.Buyer=="unknown"?old.Buyer:bid.Buyer,FcId=old.FcId.Length>0?old.FcId:bid.FcId};
        if(prior?.Lottery is {} known && bid.Buyer=="unknown" && known.Buyer!="unknown")bid=bid with {Buyer=known.Buyer,FcId=known.FcId};
        if(bid.Buyer=="fc"&&bid.FcId=="")bid=bid with {FcId=config.Discoveries.LastOrDefault(e=>e.Actor.ContentId==actor.ContentId&&e.Kind=="character.updated")?.Character is {FcMember: not false,FreeCompany: {} company}?company.Id:""};
        if(bid.Buyer=="fc"&&string.IsNullOrEmpty(bid.FcName))bid=bid with {FcName=prior?.Lottery?.FcName is {Length:>0} name?name:config.Discoveries.LastOrDefault(e=>e.Actor.ContentId==actor.ContentId&&e.Kind=="character.updated")?.Character?.FreeCompany?.Name??""};
        if(prior?.Lottery is {} previous && (previous with {Evidence=bid.Evidence})==bid)return;
        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"housing.lottery",now,WithWorldNames(actor),null,Lottery:bid));
    }
}

