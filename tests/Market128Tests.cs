using System.Net;
using EquinoxCompanion;
internal static class Market128Tests
{
    internal static async Task Run()
    {
        void Check(bool ok, string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS 128 " + name); }
        var handler = new Handler(); using var service = new MarketSearch(handler);
        var world = await service.Search(5111, 2, 0, CancellationToken.None, world:80, worldName:"Cerberus");
        Check(handler.Urls.Count == 1 && handler.Urls[0].Contains("/80/5111?"), "selected world queried directly");
        Check(world.Listings.Single().WorldId == 80 && world.Listings[0].World == "Cerberus" && world.Listings[0].Region == "Europe", "single-world listing inherits world identity");
        await service.Search(5111, 2, 0, CancellationToken.None, world:80, worldName:"Cerberus");
        Check(handler.Urls.Count == 1, "world query cache reused");
        await service.Search(5111, 2, 0, CancellationToken.None, world:81, worldName:"Other");
        Check(handler.Urls.Count == 2, "cache isolates different worlds");
        await service.Search(5111, 2, 0, CancellationToken.None, force:true, world:80, worldName:"Cerberus");
        Check(handler.Urls.Count == 3, "world refresh bypasses cache");
        var original = new StorageDetails("bag:0", "Inventory 1", [10], [new(10, 2, false, "Item", [new(1,2)])]);
        var copy = original with { Items = [10], Stacks = [new(10, 2, false, "Item", [new(1,2)])] };
        Check(StorageObservationEquality.Same(original, copy), "identical independent storage snapshots skip serialization");
        Check(!StorageObservationEquality.Same(original, copy with { Stacks = [copy.Stacks![0] with { Quantity=3 }] }), "quantity changes are preserved");
        Check(!StorageObservationEquality.Same(original, copy with { Stacks = [copy.Stacks![0] with { Hq=true }] }), "quality changes are preserved");
        Check(!StorageObservationEquality.Same(original, copy with { Stacks = [copy.Stacks![0] with { Positions=[new(2,2)] }] }), "slot movement is preserved");
        Check(!StorageObservationEquality.Same(original, copy with { Items=[], Stacks=[] }), "empty container is recorded");
        Check(!StorageObservationEquality.Same(original, copy with { Stacks=null }), "unknown stacks distinct from known stacks");
    }
    private sealed class Handler : HttpMessageHandler
    {
        internal List<string> Urls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"listings":[{"pricePerUnit":10,"quantity":2,"hq":false}]}""") });
        }
    }
}
