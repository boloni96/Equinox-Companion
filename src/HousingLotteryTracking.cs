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
    private DateTimeOffset nextLotteryReminderCheck;
    private (LotteryBid Bid, ulong Character, ulong Target, uint World, DateTimeOffset At)? lotteryPlacard;
    private string lotteryPendingBuyer="unknown";
    private ulong lotteryPendingCharacter;
    private uint lotteryPendingWorld;
    private DateTimeOffset lotteryPendingAt;
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
        if(id==0){lotteryBidNotices.Reset();lotteryLogin.Reset();return;}
        lotteryBidNotices.Due(id,"",now);
        if(lotteryLogin.Due(id,now,config.NotifyHousingLottery,!LotteryLoaded))
        {
            var p=HousingLottery.Period(now);var left=p.End-now;
            Chat.Print($"[Equinox] Housing lottery · scheduled {(p.Entry?"entry period":"results period (entries closed)")} · ends {HousingLotteryNotices.LocalTime(p.End)} (local time; {(int)left.TotalDays}d {left.Hours}h {left.Minutes}m). {(p.Entry?"Place bids at the plot placard.":"Check your bid's placard for the result and claim any win.")} Duty → Timers → Estate confirms the game status.");
        }
        if(!config.NotifyHousingLottery||!LotteryLoaded||now<nextLotteryReminderCheck)return;
        nextLotteryReminderCheck=now.AddSeconds(15);
        foreach(var notice in HousingLotteryNotices.Messages(HousingLotteryNotices.WithShared(config.Discoveries,config.SharedRoster),id.ToString(),now))
            if(lotteryBidNotices.Due(id,notice.Key,now))Chat.Print(notice.Text);
    }

    private static unsafe string LotteryText(AtkUnitBase* addon,int i)
    {
        if(addon==null||addon->AtkValues==null||i<0||i>=addon->AtkValuesCount)return "";
        var v=&addon->AtkValues[i];return ((int)v->Type&15) is 8 or 10 && v->String.Value!=null?v->String.ToString():"";
    }
    private unsafe void ObserveLotteryAddon(AddonEvent type,AddonArgs args)
    {
        if(!LotteryLoaded||!config.SyncHouseDetails||Client.ClientLanguage!=Dalamud.Game.ClientLanguage.English||args.Addon.IsNull)return;
        try
        {
            var addon=(AtkUnitBase*)args.Addon.Address;var now=DateTimeOffset.UtcNow;
            if(args.AddonName=="HousingSignBoard")
            {
                var node=addon->GetTextNodeById(56);
                var location=node==null?"":node->NodeText.ToString();
                var address=HousingLottery.Parse(location,"Lottery Number: 1","unknown",now,"timers");
                if(address is not null && Targets.Target is {} sign)lotteryPlacard=(address,Player.ContentId,sign.GameObjectId,Player.CurrentWorld.RowId,now);
                else lotteryPlacard=null;
            }
            else if(args.AddonName=="ContentsInfoDetail")
            {
                // Read-only, bounded UI fields; text shape is checked before accepting data.
                var status=LotteryText(addon,119);if(!status.Contains("period in progress",StringComparison.OrdinalIgnoreCase)&&!status.Contains("Current Entry",StringComparison.OrdinalIgnoreCase))return;
                if(status.Contains("Entry period",StringComparison.OrdinalIgnoreCase)!=HousingLottery.Period(now).Entry)return;
                var kind=LotteryText(addon,123);
                var buyer=HousingLottery.Buyer(kind);
                var bid=HousingLottery.Parse(LotteryText(addon,121),LotteryText(addon,122),buyer,now,"timers");
                if(bid is not null)RecordLottery(bid,now);
            }
            else if(args.AddonName is "SelectYesNoTextScroll" or "SelectYesno" && HousingLottery.EntryConfirmation(LotteryText(addon,0)))
            {
                // The buyer can appear after a line break in either confirmation layout.
                // Read the whole prompt, not just its first line; mixed choices stay unknown.
                lotteryPendingBuyer=HousingLottery.Buyer(LotteryText(addon,0));
                lotteryPendingAt=now;lotteryPendingCharacter=Player.ContentId;lotteryPendingWorld=Player.CurrentWorld.RowId;
            }
            else if(ObservePublicLotteryResult(addon,now)){}
            else if(HousingLottery.Result(LotteryText(addon,0)) is {} result)
            {
                // Only connect a personal result dialog to a previously observed entry
                // at the exact targeted plot. Never infer a result from a public winner.
                var target=Targets.Target;
                if(target is null||lotteryPlacard is not {} plot||plot.Character!=Player.ContentId||plot.Target!=target.GameObjectId||plot.World!=Player.CurrentWorld.RowId||now-plot.At>TimeSpan.FromMinutes(1))return;
                var matches=config.Discoveries.Where(e=>e.Kind=="housing.lottery"&&e.Actor.ContentId==Player.ContentId.ToString()&&e.Lottery is {} b&&b.District==plot.Bid.District&&b.Ward==plot.Bid.Ward&&b.Plot==plot.Bid.Plot&&e.Actor.HomeWorldId==plot.World).GroupBy(e=>(e.Lottery!.CycleStart,e.Lottery.Entry)).Select(g=>g.MaxBy(e=>e.At)!).Where(e=>e.Lottery!.Status=="entered").ToArray();
                if(matches.Length==1)RecordLottery(matches[0].Lottery! with {Status=result,Evidence="result-dialog"},now);
            }
        }
        catch(Exception ex){errorJournal.Record("housing-lottery","Lottery view could not be read; existing entries kept.",exceptionType:ex.GetType().Name);}
    }
    private unsafe bool ObservePublicLotteryResult(AtkUnitBase* addon,DateTimeOffset now)
    {
        var period=HousingLottery.Period(now);
        if(period.Entry||lotteryPlacard is not {} plot||plot.Character!=Player.ContentId||plot.World!=Player.CurrentWorld.RowId||Targets.Target?.GameObjectId!=plot.Target||now-plot.At>TimeSpan.FromMinutes(1))return false;
        int? winner=null;
        for(var i=0;i<Math.Min((int)addon->AtkValuesCount,64);i++)
            if(HousingLottery.WinningNumber(LotteryText(addon,i)) is {} number){winner=number;break;}
        if(winner is null)return false;
        var actor=ReadActor();if(!SyncValidation.ActorReady(actor))return false;
        var result=new LotteryResult(plot.Bid.District,plot.Bid.Ward,plot.Bid.Plot,plot.World,winner.Value,period.Start.ToString("O"));
        if(config.Discoveries.Any(e=>e.Kind=="housing.lottery-result"&&e.LotteryResult==result))return true;
        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"housing.lottery-result",now,WithWorldNames(actor),null,LotteryResult:result));
        return true;
    }
    private void ObserveLotteryChat(IHandleableChatMessage message)
    {
        if(!LotteryLoaded||!config.SyncHouseDetails||message.LogKind is not (XivChatType.SystemMessage or XivChatType.Notice)||message.OriginalSender.ExtractText().Length!=0)return;
        var text=message.OriginalMessage.ExtractText();
        var m=Regex.Match(text,@"^You (?:have )?submitted a lottery entry for plot (\d+), ward (\d+), ([^.]+)\. Your lottery number is (\d+)",RegexOptions.IgnoreCase);
        if(!m.Success)return;var now=DateTimeOffset.UtcNow;
        var buyer=lotteryPendingCharacter==Player.ContentId&&lotteryPendingWorld==Player.CurrentWorld.RowId&&now-lotteryPendingAt<TimeSpan.FromMinutes(5)?lotteryPendingBuyer:"unknown";
        var bid=HousingLottery.Parse($"Plot {m.Groups[1].Value}, {m.Groups[2].Value} Ward, {m.Groups[3].Value}",$"Lottery Number: {m.Groups[4].Value}",buyer,now,"submission");
        if(bid is not null)RecordLottery(bid,now);
        lotteryPendingBuyer="unknown";lotteryPendingCharacter=0;lotteryPendingWorld=0;
    }
    private void RecordLottery(LotteryBid bid,DateTimeOffset now)
    {
        var actor=ReadActor();if(!SyncValidation.ActorReady(actor)||actor.HomeWorldId!=actor.CurrentWorldId)return;
        var prior=config.Discoveries.LastOrDefault(e=>e.Kind=="housing.lottery"&&e.Actor.ContentId==actor.ContentId&&e.Lottery is {} b&&b.District==bid.District&&b.Ward==bid.Ward&&b.Plot==bid.Plot&&b.Entry==bid.Entry&&b.CycleStart==bid.CycleStart);
        if(prior?.Lottery is {} old && bid.Evidence=="timers")bid=bid with {CycleStart=old.CycleStart,Status=old.Status,Buyer=bid.Buyer=="unknown"?old.Buyer:bid.Buyer,FcId=old.FcId};
        if(prior?.Lottery is {} known && bid.Buyer=="unknown" && known.Buyer!="unknown")bid=bid with {Buyer=known.Buyer,FcId=known.FcId};
        if(bid.Buyer=="fc"&&bid.FcId=="")bid=bid with {FcId=config.Discoveries.LastOrDefault(e=>e.Actor.ContentId==actor.ContentId&&e.Kind=="character.updated")?.Character is {FcMember: not false,FreeCompany: {} company}?company.Id:""};
        if(prior?.Lottery is {} previous && (previous with {Evidence=bid.Evidence})==bid)return;
        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"housing.lottery",now,WithWorldNames(actor),null,Lottery:bid));
    }
}
