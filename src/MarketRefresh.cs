namespace EquinoxCompanion;

internal static class MarketRefresh
{
    internal static bool Due(bool visible, uint item, bool busy, DateTimeOffset now, DateTimeOffset due) => visible && item != 0 && !busy && now >= due;
    internal static DateTimeOffset Next(MarketResult result, int failures, int seconds = 30) => result.FetchedAt.AddSeconds(failures == 0 ? (seconds is 15 or 30 or 60 ? seconds : 30) : Math.Min(300, 60 * (1 << Math.Min(failures - 1, 3))));
    internal static MarketResult Merge(MarketResult? previous, MarketResult incoming)
    {
        if(previous is null || incoming.FailedRegions.Length == 0) return incoming;
        var failed = incoming.FailedRegions.ToHashSet(StringComparer.Ordinal);
        return incoming with { Listings = incoming.Listings.Concat(previous.Listings.Where(r => failed.Contains(r.Region)))
            .OrderBy(r => r.Price).ThenBy(r => r.Total).ThenBy(r => r.World).ToArray() };
    }
}
