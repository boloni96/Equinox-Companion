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
    public bool SyncToJournal { get; set; } = true;
    public int BackgroundIntervalMinutes { get; set; } = 15;
    public int BackgroundMaxAgeMinutes { get; set; } = 60;
    public bool OpenCompare { get; set; } = true;
    public bool OpenHistory { get; set; } = true;
    public bool ClickSetsPrice { get; set; } = true;
    public bool ManualAllowOtherQuality { get; set; }
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
    public double MaximumIncreasePercent { get; set; } = 100;
    public bool UseHistory { get; set; } = true;
    public SellerHistoryRule HistoryRule { get; set; } = SellerHistoryRule.Median;
    public int HistoryDays { get; set; } = 7;
    public int MinimumHistorySales { get; set; } = 3;
    public double HistoryOutlierPercent { get; set; } = 50;
    public Dictionary<uint, int> MinimumPrices { get; set; } = [];
    public HashSet<uint> ExcludedItems { get; set; } = [];
    public HashSet<ulong> SelectedRetainers { get; set; } = [];
}

public sealed record SellerOffer(ulong RetainerId, long Price, bool Hq, uint Quantity, bool Mannequin = false, string? Materia = "");
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
    // Matching a known own offer must not subtract an undercut or round it down.
    public static int OfferPrice(long reference, bool own, SellerSettings s, int minimum = 1) => own
        ? (int)Math.Clamp(reference, Math.Clamp(minimum, 1, MaxPrice), MaxPrice)
        : Adjust(reference, s, minimum);

    // The caller supplies only current, verified listings of the editor's base
    // item. A deliberate click may cross HQ/NQ when opted in; automatic Decide
    // never reads this setting. Manual clicks may also choose another meld.
    public static (int Price, bool MatchOwn)? ManualClickPrice(long reference, bool editorHq,
        IEnumerable<SellerOffer> verifiedOffers, ISet<ulong> ownRetainers, SellerSettings settings, int minimum = 1)
    {
        if (reference is <= 0 or > MaxPrice) return null;
        var candidates = verifiedOffers.Where(x => x.Price == reference && x.Quantity > 0 && !x.Mannequin &&
            (settings.ManualAllowOtherQuality || x.Hq == editorHq)).ToArray();
        if (candidates.Length == 0) return null;
        var matchOwn = candidates.Any(x => x.RetainerId != 0 && ownRetainers.Contains(x.RetainerId));
        return (OfferPrice(reference, matchOwn, settings, minimum), matchOwn);
    }
    private static SellerDecision LimitIncrease(SellerDecision decision,int current,int minimum,SellerSettings settings)
    {
        if(!decision.CanApply||decision.Price is not {} target||current<=0||target<=current||target<=Math.Clamp(minimum,1,MaxPrice))return decision;
        var percent=FinitePercent(settings.MaximumIncreasePercent,100,0,10000);
        if((decimal)(target-current)*100<=current*(decimal)percent)return decision;
        return new(SellerStatus.Skipped,null,$"Suggested {target:N0} gil exceeds your {percent:N0}% automatic increase limit from {current:N0} gil. Existing price kept; review recent matching sales before raising it. "+decision.Reason,decision.Source,decision.Undercut,decision.LowestCompetitor);
    }
    public static SellerDecision Decide(uint item, uint world, bool hq, int current, SellerComparison? result,
        ISet<ulong> ownRetainers, SellerSettings s, DateTimeOffset now, string? materia = "")
    {
        SellerDecision Skip(string why) => new(SellerStatus.Skipped, null, why, "None");
        if (s.ExcludedItems.Contains(item)) return Skip("Excluded by your item rule.");
        if (result is null || !result.Complete || result.Error.Length > 0) return Skip(result?.Error is {Length: >0} error ? error : "Comparison did not finish; existing price kept.");
        if (result.ItemId != item || result.WorldId != world || result.At > now.AddSeconds(5) || now-result.At > TimeSpan.FromMinutes(5)) return Skip("Comparison belongs to another item/world or is stale.");
        if(result.Offers.Any(x=>x.Price is <=0 or >MaxPrice || x.Quantity==0))return Skip("Comparison contains an invalid listing; existing price kept.");
        if (!SellerMateria.Known(materia)) return Skip("Materia was not recorded for this item. Open this retainer's selling list to refresh it; existing price kept.");
        var quality = result.Offers.Where(x=>x.Hq == hq && !x.Mannequin).ToArray();
        var all = quality.Where(x=>SellerMateria.Matches(materia,x.Materia)).ToArray();
        var recent = RecentSales(result.History, hq, s, now);
        var context = $"{SellerMateria.Describe(materia)} · {all.Length}/{quality.Length} same-quality listings comparable, across all {result.Offers.Length} returned listings. ";
        if (result.HistoryComplete && recent.Length > 0)
            context += $"Latest recent {(hq?"HQ":"NQ")} sale: {recent[0].Price:N0} gil. Sale history does not report materia. ";
        bool Own(SellerOffer offer)=>offer.RetainerId!=0&&ownRetainers.Contains(offer.RetainerId);
        var prices = all.Where(x=>!Own(x)).Select(x=>x.Price).Distinct().Order().ToArray();
        var ownPrice = all.Where(Own).Select(x=>(long?)x.Price).Min();
        var minimum = s.MinimumPrices.GetValueOrDefault(item, 1);
        if (ownPrice is {} match && (prices.Length==0 || match<=prices[0]))
        {
            var price=OfferPrice(match,true,s,minimum);
            var reason=context+$"Match your known retainer's {match:N0} gil unit price; no undercut or rounding.";
            if(price!=match)reason+=" Your item minimum prevents an exact match.";
            var keep=current>=Math.Clamp(minimum,1,MaxPrice)&&(current==price||current<price&&!s.RaiseCompetitivePrices);
            return LimitIncrease(new(keep?SellerStatus.Competitive:SellerStatus.Update,keep?current:price,
                reason+(keep?" Your rules keep the current price.":""),"Live listings",prices.Length>0&&prices[0]<current,prices.Length>0?prices[0]:null),current,minimum,s);
        }
        if (prices.Length > 0)
        {
            var lowGap = LowGap(prices, s);
            // History lacks meld information: it can corroborate a plain low
            // price, never value a melded item or establish its materia premium.
            var supporting = materia == "" && s.UseHistory && result.HistoryComplete
                ? recent.Count(x=>Math.Abs((decimal)x.Price-prices[0]) <= prices[0]*0.10m) : 0;
            var lowSupported = supporting >= Math.Clamp(s.MinimumHistorySales,1,20);
            var ignoreLowest = lowGap && !lowSupported;
            var undercut = prices[0] < current;
            var reference = prices[ignoreLowest ? 1 : 0];
            var price = Adjust(reference, s, minimum);
            var reason = context + (ignoreLowest ? $"Ignored unusually low {prices[0]:N0}; next distinct comparable price {reference:N0}." : $"Lowest comparable competing unit price: {reference:N0}.");
            if (lowGap && lowSupported) reason += $" Kept the low price: {supporting} recent same-quality sales within 10% support it.";
            if (price >= reference && minimum >= reference) reason += " Your minimum prevents undercutting.";
            if (current >= Math.Clamp(minimum,1,MaxPrice) && (current == price || current < price && !s.RaiseCompetitivePrices))
                return new(SellerStatus.Competitive, current, reason+" Your rules keep the current price."+(undercut?" A lower listing exists; review the ignored low-price rule.":""), "Live listings",undercut,prices[0]);
            return LimitIncrease(new(SellerStatus.Update, price, reason, "Live listings",undercut,prices[0]),current,minimum,s);
        }
        if (materia != "") return Skip(context+"No listing with the same materia types and grades. Existing price kept; review other melds manually. History cannot price this materia setup.");
        if (quality.Any(x=>!SellerMateria.Known(x.Materia))) return Skip(context+"Some listings have unknown materia; existing price kept until their details are available.");
        if (!s.UseHistory) return Skip(context+"No comparable listings; sale-history pricing is disabled.");
        if (!result.HistoryComplete) return Skip("No listings; recent sales did not finish loading.");
        var days=Math.Clamp(s.HistoryDays,1,90);
        var matching=result.History.Where(x=>x.Hq == hq && !x.Mannequin && x.Quantity>0 && x.Price is >0 and <=MaxPrice).ToArray();
        var count = Math.Clamp(s.MinimumHistorySales,1,20);
        if (recent.Length < count) return Skip($"No listings; {result.History.Length} sales loaded, {matching.Length} valid {(hq?"HQ":"NQ")} sales, but only {recent.Length}/{count} within {days} days. Existing price kept.");
        static long Median(IEnumerable<long> source) { var a=source.Order().ToArray();return a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2; }
        var median = Median(recent.Select(x=>x.Price));
        var gap = (decimal)FinitePercent(s.HistoryOutlierPercent,50,1,99)/100;
        var supported = recent.Where(x=>x.Price>=median*(1-gap) && x.Price<=median*(1+gap)).ToArray();
        if (supported.Length < count) return Skip($"No listings; too few sales remain after excluding outliers ({supported.Length}/{count}).");
        var referenceHistory = s.HistoryRule switch { SellerHistoryRule.HighestSupported=>supported.Max(x=>x.Price), SellerHistoryRule.Latest=>supported[0].Price, _=>Median(supported.Select(x=>x.Price)) };
        var target = Adjust(referenceHistory,s,minimum,false);
        return LimitIncrease(new(current==target?SellerStatus.Competitive:SellerStatus.History,target,context+$"No comparable listings · {s.HistoryRule} of {supported.Length} recent { (hq?"HQ":"NQ") } sales. No undercut applied to history.","Recent sales"),current,minimum,s);
    }

    private static SellerSale[] RecentSales(IEnumerable<SellerSale> history, bool hq, SellerSettings s, DateTimeOffset now) => history
        .Where(x=>x.Hq==hq&&!x.Mannequin&&x.Quantity>0&&x.Price is >0 and <=MaxPrice&&x.At<=now.AddSeconds(5)&&x.At>=now.AddDays(-Math.Clamp(s.HistoryDays,1,90)))
        .OrderByDescending(x=>x.At).Take(20).ToArray();
    private static bool LowGap(long[] prices, SellerSettings s) => prices.Length>1&&s.LowOutlierPercent>0&&
        100m*(prices[1]-prices[0])/prices[1]>(decimal)FinitePercent(s.LowOutlierPercent,40,0,100);
    public static bool NeedsHistory(IEnumerable<SellerOffer> offers, bool hq, string? materia, ISet<ulong> own, SellerSettings s)
    {
        if (!s.UseHistory || materia != "") return false;
        var matching = offers.Where(x=>x.Hq==hq&&!x.Mannequin&&SellerMateria.Matches(materia,x.Materia)).ToArray();
        var prices = matching.Where(x=>x.RetainerId==0||!own.Contains(x.RetainerId)).Select(x=>x.Price).Distinct().Order().ToArray();
        return matching.Length==0 || LowGap(prices,s);
    }
}
