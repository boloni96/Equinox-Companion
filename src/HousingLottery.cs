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
    public static LotteryBid? PlacardAddress(string text,DateTimeOffset now)
    {
        var found=text.Split('\n').Select(line=>Parse(line,"Lottery Number: 1","unknown",now,"timers"))
            .Where(b=>b is not null).DistinctBy(b=>(b!.District,b.Ward,b.Plot)).ToArray();
        return found.Length==1?found[0]:null;
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
    public static bool Valid(LotteryBid? b) => b is not null && District(b.District) is not null && b.Ward is >=1 and <=60 && b.Plot is >=1 and <=60 && b.Entry is >=1 and <=100000 && b.Buyer is "private" or "fc" or "unknown" && (b.FcId==""||CompanyProfileIdentity.ValidId(b.FcId)) && b.Status is "entered" or "won" or "lost" or "forfeited" && DateTimeOffset.TryParse(b.CycleStart,out var at)&&at>=Epoch && (b.Evidence is "timers" or "submission" or "result-dialog" || (b.Evidence is "manual-refund" or "refund-receipt")&&b.Status=="lost");
    public static bool RefundReceiptMatches(ulong character,uint world,ulong target,long amount,DateTimeOffset offered,ulong currentCharacter,uint currentWorld,ulong? currentTarget,long received,DateTimeOffset now) => character!=0&&character==currentCharacter&&world==currentWorld&&target==currentTarget&&amount==received&&now>=offered&&now-offered<=TimeSpan.FromSeconds(30);
    public static long? RefundAmount(string text)
    {
        var m=Regex.Match(text,@"Accept\s+a\s+full\s+refund\s+of\s+your\s+deposit\s+of\s+([\d,]+)\s+gil\?",RegexOptions.IgnoreCase);
        return m.Success&&long.TryParse(m.Groups[1].Value.Replace(",",""),out var amount)&&amount>0?amount:null;
    }
    public static long? ReceivedGil(string text)
    {
        var m=Regex.Match(text.Trim(),@"^You obtain ([\d,]+) gil\.$",RegexOptions.IgnoreCase);
        return m.Success&&long.TryParse(m.Groups[1].Value.Replace(",",""),out var amount)&&amount>0?amount:null;
    }
    public static int? WinningNumber(string text)
    {
        var m=Regex.Match(text,@"\bThe\s+winning\s+number\s+(?:for\s+this\s+plot\s+is|is)\s*(?:\.{3}|…)?\s*(\d+)(?=[.!\s]|$)",RegexOptions.IgnoreCase);
        return m.Success&&int.TryParse(m.Groups[1].Value,out var n)&&n is >=1 and <=100000?n:null;
    }
    public static bool ValidResult(LotteryResult? r,DateTimeOffset at) => r is not null&&District(r.District) is not null&&r.Ward is >=1 and <=60&&r.Plot is >=1 and <=60&&r.WorldId is >=1 and <=65535&&r.Winner is >=1 and <=100000&&!Period(at).Entry&&DateTimeOffset.TryParse(r.CycleStart,out var start)&&start==Period(at).Start;
    public static string BuyerLabel(string buyer) => buyer switch { "fc"=>"FC house", "private"=>"Private house", _=>"Type unknown" };
    public static string StatusLabel(string status) => status switch { "won"=>"Won", "lost"=>"Lost", "claimed"=>"Claimed", "forfeited"=>"Forfeited", _=>"Result not observed" };
    public static bool BidActive(SharedHousingBid b) => b.Status is not ("claimed" or "forfeited") && !(b.Status == "entered" && b.FcClaimedAt is not null) && (b.Status != "lost" || b.RefundedAt is null);
    public static string BidStatusLabel(SharedHousingBid b)=>b.Status=="entered"&&b.FcClaimedAt is not null?"FC estate claimed · ticket result unknown":StatusLabel(b.Status);
    public static string BidEvidence(SharedHousingBid b)
    {
        var lines=new List<string>{$"Entry #{b.Entry} · {BuyerLabel(b.Buyer)}"};
        if(b.Winner is {} winner)lines.Add($"Winning number #{winner}"+(b.CheckedBy.Length>0?$" · checked by {b.CheckedBy}":""));
        else if(b.ResultEvidence=="current-round-owner")lines.Add("Result inferred from this round's estate owner: "+b.ObservedOwner);
        else if(b.Evidence=="result-dialog")lines.Add("Personal lottery result dialogue observed.");
        else lines.Add("Winning-number / result evidence not included in this record.");
        if(b.ResultAt is {} at)lines.Add("Result observed: "+at.ToLocalTime().ToString("g"));
        if(b.FcClaimedAt is {} claim)lines.Add("FC estate confirmed: "+claim.ToLocalTime().ToString("g")+(b.FcClaimedOwner.Length>0?" · "+b.FcClaimedOwner:"")+"\nFC ownership does not identify the individual winning ticket.");
        if(b.RefundedAt is {} refund)lines.Add("Refund collection recorded: "+refund.ToLocalTime().ToString("g"));
        return string.Join("\n",lines);
    }
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
