using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public sealed record LotteryBid(string District, int Ward, int Plot, int Entry, string Buyer, string FcId, string Status, string CycleStart, string Evidence, string FcName="");
public sealed record LotteryResult(string District,int Ward,int Plot,uint WorldId,int Winner,string CycleStart);
public static class HousingLottery
{
    public static readonly DateTimeOffset Epoch = new(2022,5,26,15,0,0,TimeSpan.Zero);
    public static (DateTimeOffset Start, DateTimeOffset End, bool Entry) Period(DateTimeOffset now)
    {
        var start = Epoch.AddDays(Math.Floor((now-Epoch).TotalDays/9)*9);
        var entry = now < start.AddDays(5);
        return (start, start.AddDays(entry ? 5 : 9), entry);
    }
    public static string? District(string s) => s.Trim().ToLowerInvariant() switch
    { "mist"=>"Mist", "the lavender beds" or "lavender beds"=>"Lavender Beds", "the goblet" or "goblet"=>"Goblet", "shirogane"=>"Shirogane", "empyreum"=>"Empyreum", _=>null };
    public static LotteryBid? Parse(string location, string number, string buyer, DateTimeOffset now, string evidence)
    {
        var m=Regex.Match(location,@"\bPlot\s+(\d+),\s*(\d+)(?:st|nd|rd|th)?\s+Ward,\s*([^\r\n(]+)",RegexOptions.IgnoreCase);
        var n=Regex.Match(number,@"\bLottery Number:\s*(\d+)",RegexOptions.IgnoreCase);
        if(!m.Success||!n.Success||!int.TryParse(m.Groups[1].Value,out var plot)||!int.TryParse(m.Groups[2].Value,out var ward)||!int.TryParse(n.Groups[1].Value,out var entry)||District(m.Groups[3].Value) is not {} district)return null;
        var bid=new LotteryBid(district,ward,plot,entry,buyer,"","entered",Period(now).Start.ToString("O"),evidence);
        return Valid(bid)?bid:null;
    }
    public static string Buyer(string text)
    {
        var fc=Regex.IsMatch(text,@"\bfree\s+company\b",RegexOptions.IgnoreCase);
        var personal=Regex.IsMatch(text,@"\b(?:personal|private|individual)\b",RegexOptions.IgnoreCase);
        return fc==personal?"unknown":fc?"fc":"private";
    }
    public static bool EntryConfirmation(string text)=>Regex.IsMatch(text,@"\benter\s+(?:the|this)\s+lottery\b",RegexOptions.IgnoreCase);
    public static LotteryBid? ParseTimers(string text,DateTimeOffset now)
    {
        if(!text.Contains("Housing Lottery Status",StringComparison.OrdinalIgnoreCase)||!text.Contains("Current Entry",StringComparison.OrdinalIgnoreCase))return null;
        var type=Regex.Match(text,@"Type of Entry:\s*([^\r\n]+)",RegexOptions.IgnoreCase);
        return Parse(text,text,type.Success?Buyer(type.Groups[1].Value):"unknown",now,"timers");
    }
    public static bool Valid(LotteryBid? b) => b is not null && District(b.District) is not null && b.Ward is >=1 and <=60 && b.Plot is >=1 and <=60 && b.Entry is >=1 and <=100000 && b.Buyer is "private" or "fc" or "unknown" && (b.FcId==""||CompanyProfileIdentity.ValidId(b.FcId)) && b.Status is "entered" or "won" or "lost" or "forfeited" && DateTimeOffset.TryParse(b.CycleStart,out var at)&&at>=Epoch && (b.Evidence is "timers" or "submission" or "result-dialog" || b.Evidence=="manual-refund"&&b.Status=="lost");
    public static int? WinningNumber(string text)
    {
        var m=Regex.Match(text,@"\bThe\s+winning\s+number\s+(?:for\s+this\s+plot\s+is|is)\s*(?:\.{3}|…)?\s*(\d+)(?=[.!\s]|$)",RegexOptions.IgnoreCase);
        return m.Success&&int.TryParse(m.Groups[1].Value,out var n)&&n is >=1 and <=100000?n:null;
    }
    public static bool ValidResult(LotteryResult? r,DateTimeOffset at) => r is not null&&District(r.District) is not null&&r.Ward is >=1 and <=60&&r.Plot is >=1 and <=60&&r.WorldId is >=1 and <=65535&&r.Winner is >=1 and <=100000&&!Period(at).Entry&&DateTimeOffset.TryParse(r.CycleStart,out var start)&&start==Period(at).Start;
    public static string BuyerLabel(string buyer) => buyer switch { "fc"=>"FC house", "private"=>"Private house", _=>"Type unknown" };
    public static string StatusLabel(string status) => status switch { "won"=>"Won", "lost"=>"Lost", "claimed"=>"Claimed", "forfeited"=>"Forfeited", _=>"Result not observed" };
    public static string ObservedStatus(SharedHousingBid bid, IEnumerable<LotteryResult> results)
    {
        if(bid.Status!="entered")return bid.Status;
        if(!DateTimeOffset.TryParse(bid.CycleStart,out var cycle))return bid.Status;
        var winners=results.Where(r=>r.WorldId==bid.WorldId&&r.District==bid.District&&r.Ward==bid.Ward&&r.Plot==bid.Plot&&DateTimeOffset.TryParse(r.CycleStart,out var round)&&round==cycle)
            .Select(r=>r.Winner).Where(n=>n>0).Distinct().Take(2).ToArray();
        return winners.Length==1?(winners[0]==bid.Entry?"won":"lost"):bid.Status;
    }
    public static string? Result(string text)
    {
        text=Regex.Replace(text,@"\s+"," ");
        if(text.Contains("claim to this plot of land has been forfeit",StringComparison.OrdinalIgnoreCase))return "forfeited";
        if(text.Contains("better luck in the future",StringComparison.OrdinalIgnoreCase))return "lost";
        // A negative sentence containing 'winner' is never evidence of a win.
        if(Regex.IsMatch(text,@"\b(?:not|no longer|never)\b",RegexOptions.IgnoreCase))return null;
        return text.Contains("winner of this lottery",StringComparison.OrdinalIgnoreCase)||text.Contains("sole entrant of this lottery",StringComparison.OrdinalIgnoreCase)?"won":null;
    }

}
public sealed class LotteryLogin
{
    private ulong character; private DateTimeOffset ready; private bool printed;
    public void Reset(){character=0;printed=false;}
    public bool Due(ulong id,DateTimeOffset now,bool enabled,bool loading)
    {
        if(id==0){Reset();return false;}
        if(id!=character){character=id;ready=now.AddSeconds(20);printed=false;}
        if(!enabled||loading||printed||now<ready)return false;
        printed=true;return true;
    }
}
