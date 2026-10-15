using System.Globalization;
namespace EquinoxCompanion;
public static class HousingLotteryNotices
{
    public static string LocalTime(DateTimeOffset value) => value.ToLocalTime().ToString("MMM d, h:mm tt",CultureInfo.InvariantCulture);
    public static IEnumerable<(string Key,string Text)> Messages(IEnumerable<SyncEvent> events,string character,DateTimeOffset now)
    {
        var period=HousingLottery.Period(now);if(period.Entry)yield break;
        var all=events.ToArray();
        foreach(var e in all.Where(e=>e.Kind=="housing.lottery"&&e.Actor.ContentId==character&&HousingLottery.Valid(e.Lottery)&&DateTimeOffset.Parse(e.Lottery!.CycleStart)==period.Start)
            .GroupBy(e=>(e.Actor.HomeWorldId,e.Lottery!.District,e.Lottery.Ward,e.Lottery.Plot,e.Lottery.Entry)).Select(g=>g.MaxBy(e=>e.At)!))
        {
            var b=e.Lottery!;
            if(all.Any(h=>h.Kind=="house.discovered"&&h.House?.Evidence=="owned-estate-id"&&h.At>=period.Start.AddDays(5)&&h.Address is {} a&&a.WorldId==e.Actor.HomeWorldId&&HousingLottery.District(a.DistrictName??"")==b.District&&a.Ward==b.Ward&&a.Plot==b.Plot&&(b.Buyer=="fc"&&b.FcId!=""?h.House.FreeCompany?.Id==b.FcId:h.Actor.ContentId==character&&b.Buyer=="private"&&h.House.Type=="Private house")))continue;
            var status=b.Status;
            var result=all.LastOrDefault(x=>x.Kind=="housing.lottery-result"&&x.LotteryResult is {} r&&r.WorldId==e.Actor.HomeWorldId&&r.District==b.District&&r.Ward==b.Ward&&r.Plot==b.Plot&&DateTimeOffset.Parse(r.CycleStart)==period.Start)?.LotteryResult;
            if(status=="entered"&&result is not null)status=result.Winner==b.Entry?"won":"lost";
            var action=status switch {"won"=>"Your entry won! Go to the placard to claim it.","lost"=>"Your entry was not selected. Visit the placard to collect your refund.","forfeited"=>"Your claim was forfeited. Visit the placard to check your refund.",_=>"Entries are closed. Go to the placard to check your result."};
            yield return ($"{period.Start:O}|{e.Actor.HomeWorldId}|{b.District}|{b.Ward}|{b.Plot}|{b.Entry}",$"[Equinox] Housing bid · {(b.Buyer=="fc"?"FC":b.Buyer=="private"?"Private":"Saved")} · {b.District}, Ward {b.Ward}, Plot {b.Plot} · Entry #{b.Entry}. {action} Results end {LocalTime(period.End)} (local time).");
        }
    }
}
public sealed class LotteryBidNoticeGate
{
    private ulong character;private DateTimeOffset ready;private readonly HashSet<string> sent=[];
    public bool Due(ulong id,string key,DateTimeOffset now)
    {
        if(id!=character){character=id;sent.Clear();ready=now.AddSeconds(20);}
        return id!=0&&now>=ready&&key.Length>0&&sent.Add(key);
    }
    public void Reset(){character=0;sent.Clear();}
}
