using System.Net.Http;
using System.Text.Json;
namespace EquinoxCompanion;

internal sealed record MarketListing(string Region, uint WorldId, string World, bool Hq, long Price, int Quantity, string Retainer, long Reviewed)
{
    public long Total => Price * Quantity;
}
internal sealed record MarketResult(MarketListing[] Listings, string[] FailedRegions, DateTimeOffset FetchedAt);

internal sealed class MarketSearch : IDisposable
{
    internal static readonly string[] Regions = ["North-America", "Europe", "Japan", "Oceania"];
    private readonly HttpClient http;
    private readonly Dictionary<string, MarketResult> cache = new();
    internal MarketSearch(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("EquinoxCompanion/0.5.1.131");
    }
    internal async Task<MarketResult> Search(uint item, int region, int quality, CancellationToken token, bool force = false, uint world = 0, string worldName = "")
    {
        var key = $"{item}/{region}/{quality}/{world}";
        lock(cache)
            if(!force && cache.TryGetValue(key, out var saved) && DateTimeOffset.UtcNow - saved.FetchedAt < TimeSpan.FromSeconds(60)) return saved;
        var regions = world != 0 ? new[] { region > 0 ? Regions[region - 1] : "Selected world" } : region == 0 ? Regions : [Regions[region - 1]];
        var responses = await Task.WhenAll(regions.Select(async name =>
        {
            try
            {
                var qualityQuery = quality == 0 ? "" : quality == 1 ? "&hq=false" : "&hq=true";
                using var response = await http.GetAsync($"https://universalis.app/api/v2/{(world == 0 ? name : world.ToString(System.Globalization.CultureInfo.InvariantCulture))}/{item}?listings=100&entries=0{qualityQuery}", token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                return (Rows: Parse(body, name, world, worldName), Failed: (string?)null);
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; }
            catch(Exception ex) when(ex is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
            { return (Rows: Array.Empty<MarketListing>(), Failed: (string?)name); }
        })).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var result = new MarketResult(responses.SelectMany(r => r.Rows).OrderBy(r => r.Price).ThenBy(r => r.Total).ThenBy(r => r.World).ToArray(), responses.Where(r => r.Failed is not null).Select(r => r.Failed!).ToArray(), DateTimeOffset.UtcNow);
        // Never cache an incomplete outage as a successful empty search.
        if(result.FailedRegions.Length == 0) lock(cache)
        {
            if(cache.Count >= 32) cache.Clear();
            cache[key] = result;
        }
        return result;
    }
    internal static MarketListing[] Parse(string body, string region, uint fallbackWorld = 0, string fallbackName = "")
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if(!root.TryGetProperty("listings", out var listings) || listings.ValueKind != JsonValueKind.Array)
            throw new JsonException("Missing market listings");
        var rows = new List<MarketListing>();
        foreach(var listing in listings.EnumerateArray())
        {
            var price = listing.GetProperty("pricePerUnit").GetInt64();
            var quantity = listing.GetProperty("quantity").GetInt32();
            var worldId = listing.TryGetProperty("worldID", out var worldValue) ? worldValue.GetUInt32() : fallbackWorld;
            if(price <= 0 || quantity <= 0 || worldId == 0 || price > long.MaxValue / quantity) continue;
            rows.Add(new(region, worldId, listing.TryGetProperty("worldName", out var worldLabel) ? worldLabel.GetString() ?? $"World {worldId}" : fallbackName.Length > 0 ? fallbackName : $"World {worldId}", listing.GetProperty("hq").GetBoolean(), price, quantity,
                listing.TryGetProperty("retainerName", out var retainer) ? retainer.GetString() ?? "" : "",
                listing.TryGetProperty("lastReviewTime", out var reviewed) && reviewed.TryGetInt64(out var timestamp) ? timestamp : 0));
        }
        return rows.ToArray();
    }
    internal static string Age(long timestamp, DateTimeOffset now)
    {
        if(timestamp <= 0 || timestamp > now.ToUnixTimeSeconds() + 300) return "Unknown";
        var seconds = Math.Max(0, now.ToUnixTimeSeconds() - timestamp);
        return seconds < 60 ? "Just now" : seconds < 3600 ? $"{seconds / 60}m ago" : seconds < 86400 ? $"{seconds / 3600}h ago" : $"{seconds / 86400}d ago";
    }
    public void Dispose() => http.Dispose();
}
