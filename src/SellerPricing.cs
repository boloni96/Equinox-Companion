namespace EquinoxCompanion;

public enum SellerHistoryRule { Median, HighestSupported, Latest }
public enum SellerStatus { Unknown, Checking, Update, Competitive, History, Skipped }

[Serializable]
public sealed class SellerSettings
{
    public bool Enabled { get; set; }
    public bool OpenAtBell { get; set; } = true;
    public bool CloseAtBell { get; set; } = true;
    public bool ShowToolbar { get; set; } = true;
    public bool ToolbarOnTop { get; set; } = true;
    public bool Highlights { get; set; } = true;
    public bool ShowMarketBar { get; set; } = true;
    public bool VisitReminders { get; set; } = true;
    public int VisitReminderDays { get; set; } = 6;
    public bool BackgroundChecks { get; set; } = true;
    public int BackgroundIntervalMinutes { get; set; } = 15;
    public int BackgroundMaxAgeMinutes { get; set; } = 60;
    public bool OpenCompare { get; set; } = true;
    public bool OpenHistory { get; set; } = true;
    public bool ClickSetsPrice { get; set; } = true;
    public bool ClickConfirms { get; set; }
    public bool CopyPrice { get; set; } = true;
    public bool CtrlPastePrice { get; set; } = true;
    public bool AltTogglesHistory { get; set; } = true;
    public bool LimitNewStacks { get; set; } = true;
    public int NewStackSize { get; set; } = 99;
    public bool PercentUndercut { get; set; }
    public int UndercutGil { get; set; } = 1;
    public double UndercutPercent { get; set; } = 1;
    public double LowOutlierPercent { get; set; } = 40;
    public int RoundTo { get; set; } = 1;
    public bool RaiseCompetitivePrices { get; set; }
    public bool UseHistory { get; set; } = true;
    public SellerHistoryRule HistoryRule { get; set; } = SellerHistoryRule.Median;
    public int HistoryDays { get; set; } = 7;
    public int MinimumHistorySales { get; set; } = 3;
    public double HistoryOutlierPercent { get; set; } = 50;
    public Dictionary<uint, int> MinimumPrices { get; set; } = [];
    public HashSet<uint> ExcludedItems { get; set; } = [];
    public HashSet<ulong> SelectedRetainers { get; set; } = [];
}

public sealed record SellerOffer(ulong RetainerId, long Price, bool Hq, uint Quantity, bool Mannequin = false);
public sealed record SellerSale(long Price, bool Hq, uint Quantity, DateTimeOffset At, bool Mannequin = false);
public sealed record SellerComparison(uint ItemId, uint WorldId, DateTimeOffset At, bool Complete, SellerOffer[] Offers,
    bool HistoryComplete, SellerSale[] History, string Error = "");
public sealed record SellerDecision(SellerStatus Status, int? Price, string Reason, string Source, bool Undercut = false, long? LowestCompetitor = null)
{
    public bool NeedsAttention => CanApply || Undercut;
    public SellerStatus VisualStatus => Undercut ? SellerStatus.Update : Status;
    public bool CanApply => Price is > 0 && Status is SellerStatus.Update or SellerStatus.History;
}

public static class SellerPricing
{
    public const int MaxPrice = 999_999_999;
    private static double FinitePercent(double value,double fallback,double min,double max)=>double.IsFinite(value)?Math.Clamp(value,min,max):fallback;
    public static bool TryReadGil(string text,out int price)
    {
        price=0;if(string.IsNullOrWhiteSpace(text))return false;
        var digits=new System.Text.StringBuilder();
        foreach(var c in text){
            if(c is >= '0' and <= '9')digits.Append(c);
            else if(c is ',' or '.' or '\uE049'||char.IsWhiteSpace(c))continue;
            else return false;
        }
        return int.TryParse(digits.ToString(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out price)&&price is >0 and <=MaxPrice;
    }
    public static int Stack(int requested, int available, int maximum) => Math.Max(0, Math.Min(Math.Clamp(requested, 1, 9999), Math.Min(available, maximum)));
    public static int Adjust(long reference, SellerSettings s, int minimum = 1, bool undercut = true)
    {
        decimal price = reference;
        if (undercut) price -= s.PercentUndercut ? price * (decimal)FinitePercent(s.UndercutPercent,1,0,99) / 100 : Math.Clamp(s.UndercutGil, 0, MaxPrice);
        var round = Math.Clamp(s.RoundTo, 1, MaxPrice);
        price = decimal.Floor(price / round) * round;
        return (int)Math.Clamp(price, Math.Clamp(minimum, 1, MaxPrice), MaxPrice);
    }
    public static SellerDecision Decide(uint item, uint world, bool hq, int current, SellerComparison? result,
        ISet<ulong> ownRetainers, SellerSettings s, DateTimeOffset now)
    {
        SellerDecision Skip(string why) => new(SellerStatus.Skipped, null, why, "None");
        if (s.ExcludedItems.Contains(item)) return Skip("Excluded by your item rule.");
        if (result is null || !result.Complete || result.Error.Length > 0) return Skip(result?.Error is {Length: >0} error ? error : "Comparison did not finish; existing price kept.");
        if (result.ItemId != item || result.WorldId != world || result.At > now.AddSeconds(5) || now-result.At > TimeSpan.FromMinutes(5)) return Skip("Comparison belongs to another item/world or is stale.");
        if(result.Offers.Any(x=>x.Price is <=0 or >MaxPrice || x.Quantity==0))return Skip("Comparison contains an invalid listing; existing price kept.");
        var all = result.Offers.Where(x=>x.Hq == hq && !x.Mannequin).ToArray();
        var prices = all.Where(x=>!ownRetainers.Contains(x.RetainerId)).Select(x=>x.Price).Distinct().Order().ToArray();
        var minimum = s.MinimumPrices.GetValueOrDefault(item, 1);
        if (prices.Length > 0)
        {
            var ignoreLowest = prices.Length > 1 && s.LowOutlierPercent > 0 &&
                100m * (prices[1]-prices[0]) / prices[1] > (decimal)FinitePercent(s.LowOutlierPercent,40,0,100);
            var undercut = prices[0] < current;
            var reference = prices[ignoreLowest ? 1 : 0];
            var price = Adjust(reference, s, minimum);
            var reason = ignoreLowest ? $"Ignored unusually low {prices[0]:N0}; next distinct price {reference:N0}." : $"Lowest competing unit price: {reference:N0}.";
            if (price >= reference && minimum >= reference) reason += " Your minimum prevents undercutting.";
            if (current >= Math.Clamp(minimum,1,MaxPrice) && (current == price || current < price && !s.RaiseCompetitivePrices))
                return new(SellerStatus.Competitive, current, reason+" Your rules keep the current price."+(undercut?" A lower listing exists; review the ignored low-price rule.":""), "Live listings",undercut,prices[0]);
            return new(SellerStatus.Update, price, reason, "Live listings",undercut,prices[0]);
        }
        // Our own listings are not an empty market. Never repeatedly undercut ourselves.
        if (all.Length > 0) return new(SellerStatus.Competitive, current > 0 ? current : null, "Only your known retainers offer this quality; existing price kept.", "Live listings");
        if (!s.UseHistory) return Skip("No competing listings; sale-history pricing is disabled.");
        if (!result.HistoryComplete) return Skip("No listings; recent sales did not finish loading.");
        var recent = result.History.Where(x=>x.Hq == hq && !x.Mannequin && x.Quantity>0 && x.Price is >0 and <=MaxPrice && x.At<=now.AddSeconds(5) && x.At>=now.AddDays(-Math.Clamp(s.HistoryDays,1,90))).OrderByDescending(x=>x.At).Take(20).ToArray();
        var count = Math.Clamp(s.MinimumHistorySales,1,20);
        if (recent.Length < count) return Skip($"No listings; only {recent.Length}/{count} recent matching sales. Existing price kept.");
        static long Median(IEnumerable<long> source) { var a=source.Order().ToArray();return a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2; }
        var median = Median(recent.Select(x=>x.Price));
        var gap = (decimal)FinitePercent(s.HistoryOutlierPercent,50,1,99)/100;
        var supported = recent.Where(x=>x.Price>=median*(1-gap) && x.Price<=median*(1+gap)).ToArray();
        if (supported.Length < count) return Skip($"No listings; too few sales remain after excluding outliers ({supported.Length}/{count}).");
        var referenceHistory = s.HistoryRule switch { SellerHistoryRule.HighestSupported=>supported.Max(x=>x.Price), SellerHistoryRule.Latest=>supported[0].Price, _=>Median(supported.Select(x=>x.Price)) };
        var target = Adjust(referenceHistory,s,minimum,false);
        return new(current==target?SellerStatus.Competitive:SellerStatus.History,target,$"Empty market · {s.HistoryRule} of {supported.Length} recent { (hq?"HQ":"NQ") } sales. No undercut applied to history.","Recent sales");
    }
}
