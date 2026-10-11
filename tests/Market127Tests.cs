using EquinoxCompanion;
internal static class Market127Tests
{
    internal static void Run()
    {
        void Check(bool condition, string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS 127 market " + name); }
        var now = DateTimeOffset.UtcNow;
        var old = new MarketListing("Japan", 1, "World", false, 40, 2, "Seller", 1000);
        var eu = old with { Region = "Europe", Price = 50 };
        var previous = new MarketResult([old, eu], [], now.AddMinutes(-1));
        var incoming = new MarketResult([eu with { Price = 20 }], ["Japan"], now);
        var merged = MarketRefresh.Merge(previous, incoming);
        Check(merged.Listings.Length == 2 && merged.Listings[0].Price == 20 && merged.Listings[1] == old, "update successful region and retain failed region");
        Check(merged.FailedRegions.SequenceEqual(new[]{"Japan"}), "retained region clearly marked failed");
        Check(MarketRefresh.Merge(previous, new([], [], now)).Listings.Length == 0, "successful empty response removes sold listings");
        Check(MarketRefresh.Due(true, 5111, false, now, now), "refresh when visible and due");
        Check(!MarketRefresh.Due(false, 5111, false, now, now), "no refresh closed or minimized");
        Check(!MarketRefresh.Due(true, 5111, true, now, now), "no overlapping requests");
        Check(!MarketRefresh.Due(true, 0, false, now, now), "no request without an item");
        Check(!MarketRefresh.Due(true, 5111, false, now, now.AddSeconds(1)), "respect polling interval");
        Check(MarketRefresh.Next(incoming, 0) == now.AddSeconds(30), "default refresh after thirty seconds");
        Check(MarketRefresh.Next(incoming, 0, 15) == now.AddSeconds(15), "fifteen-second option");
        Check(MarketRefresh.Next(incoming, 0, 60) == now.AddSeconds(60), "sixty-second option");
        Check(MarketRefresh.Next(incoming, 0, 0) == now.AddSeconds(30), "invalid interval uses safe default");
        Check(MarketRefresh.Next(incoming, 4) == now.AddSeconds(300), "outage backs off to five minutes");
    }
}
