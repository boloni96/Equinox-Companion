using System.Net;
using EquinoxCompanion;
internal static class Market126Tests
{
    internal static async Task Run()
    {
        void Check(bool condition, string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS 126 market " + name); }
        const string body = """{"listings":[{"pricePerUnit":20,"quantity":99,"worldID":80,"worldName":"Cerberus","hq":true,"lastReviewTime":1000,"retainerName":"Seller"},{"pricePerUnit":5,"quantity":1,"worldID":80,"worldName":"Cerberus","hq":false,"lastReviewTime":2000},{"pricePerUnit":0,"quantity":2,"worldID":80,"worldName":"Cerberus","hq":false}]}""";
        var rows = MarketSearch.Parse(body, "Europe");
        Check(rows.Length == 2 && rows[0].Total == 1980 && rows[0].Hq, "stack total and invalid price");
        Check(rows[0].Reviewed == 1000 && rows[1].Reviewed == 2000, "per-listing timestamps preserved");
        Check(MarketSearch.Age(0, DateTimeOffset.UtcNow) == "Unknown", "unknown timestamp");
        Check(MarketSearch.Age(1000, DateTimeOffset.FromUnixTimeSeconds(8200)) == "2h ago", "age in seconds");
        var handler = new MarketHandler(body);
        using var service = new MarketSearch(handler);
        var result = await service.Search(5111, 0, 2, CancellationToken.None);
        Check(handler.Urls.Count == 4 && handler.Urls.All(u => u.Contains("hq=true")), "HQ applied before region listing limit");
        Check(result.Listings.Length == 8 && result.Listings[0].Price == 5 && result.FailedRegions.Length == 0, "four regions sorted");
        await service.Search(5111, 0, 2, CancellationToken.None);
        Check(handler.Urls.Count == 4, "repeated search cached");
        handler.FailJapan = true;
        result = await service.Search(5112, 0, 0, CancellationToken.None);
        Check(result.FailedRegions.SequenceEqual(new[]{"Japan"}) && result.Listings.Length == 6, "partial outage preserves other regions");
        handler.FailJapan = false;
        result = await service.Search(5112, 0, 0, CancellationToken.None);
        Check(result.FailedRegions.Length == 0 && handler.Urls.Count == 12, "partial failure not cached");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await service.Search(5113, 0, 0, canceled.Token); throw new Exception("cancellation ignored"); }
        catch(OperationCanceledException) { Console.WriteLine("PASS 126 market cancellation"); }
        handler.Body = "{}";
        result = await service.Search(5114, 2, 0, CancellationToken.None);
        Check(result.FailedRegions.SequenceEqual(new[]{"Europe"}), "invalid response marked unavailable");
    }
    private sealed class MarketHandler(string body) : HttpMessageHandler
    {
        internal readonly List<string> Urls = [];
        internal bool FailJapan;
        internal string Body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var url = request.RequestUri!.ToString(); Urls.Add(url);
            return Task.FromResult(new HttpResponseMessage(FailJapan && url.Contains("Japan") ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(Body) });
        }
    }
}
