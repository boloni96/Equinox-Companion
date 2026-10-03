using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed record SyncEvent(string Id, string Kind, DateTimeOffset At, Actor Actor, Address? Address, int? Patch = null, int? Bed = null, PlantDetails? Plant = null, HouseDetails? House = null, CharacterDetails? Character = null, CropDetails? Crop = null, CollectionDetails? Collection = null, FashionDetails? Fashion = null, VoyageDetails? Voyage = null, GardenTargetDetails? GardenTarget = null, StorageDetails? Storage = null, FreeCompanyDetails? Company = null, CachedVoyage? CachedVoyage = null, GardenPlanChange? PlanEdit = null);
public sealed record GardenTargetDetails(uint Argument, float X, float Y, float Z);
public sealed record StorageDetails(string Key, string Name, uint[] Items);
public sealed record CollectionDetails(string Category, uint[] Known, uint[] Unlocked, uint[] Obtained);
public sealed record FashionDetails(int Score, int Remaining, int ThemeId, string Cycle);
public sealed record SubmarineDetails(int Slot, string Name, int Rank, long ReturnTime, uint RegisterTime, ushort[] Parts, [property: System.Text.Json.Serialization.JsonConverter(typeof(SubmarineRouteJson))] byte[] Route);
public sealed record VoyageDetails(string FcId, SubmarineDetails[] Submarines);
public sealed record SyncResult(string[] Accepted, string Status, bool Retry);
public sealed class CompanionSync : IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CancellationTokenSource cancel = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    public async Task<SyncResult> Send(string key, SyncEvent[] events)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://equinoxjournal.pages.dev/api/companion/events");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = JsonContent.Create(new { events }, options: Json);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            if (!response.IsSuccessStatusCode)
                return new([], response.StatusCode switch {
                    HttpStatusCode.Unauthorized => "Pairing key invalid or revoked. Pair again on the website.",
                    HttpStatusCode.Forbidden => "Website refused the connection. Local records are kept.",
                    HttpStatusCode.NotFound => "Deploy the Companion website update first.",
                    HttpStatusCode.TooManyRequests => "Website busy; retrying later.",
                    _ => $"Website returned HTTP {(int)response.StatusCode}; local records are kept."
                }, true);
            await response.Content.LoadIntoBufferAsync(65536);
            var result = JsonSerializer.Deserialize<Receipt>(await response.Content.ReadAsStringAsync(cancel.Token), Json);
            var sent = events.Select(x => x.Id).ToHashSet();
            if (result?.Accepted is null || result.Accepted.Any(x => !sent.Contains(x)))
                return new([], "Unexpected website response; local records are kept.", true);
            return new(result.Accepted, $"Sent {result.Accepted.Length} events. Check Game connection on the website for matching.", false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { return new([], "Connection unavailable; local records are kept for retry.", true); }
    }
    private string? rosterETag;
    private string? rosterETagKey;
    public async Task<RosterResult> ReadRoster(string key, long? revision)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://equinoxjournal.pages.dev/api/companion/roster");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            if (revision is not null && rosterETagKey == key && rosterETag is not null)
                request.Headers.TryAddWithoutValidation("If-None-Match", rosterETag);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            if (response.StatusCode == HttpStatusCode.NotModified) return new(null, "Shared profiles up to date.", true);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new(null, "Shared profiles: pairing key invalid or revoked.", Unauthorized: true);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(null, "Shared profiles need Journal V7.10.2 and a new website save.");
            if (!response.IsSuccessStatusCode) return new(null, $"Shared profiles unavailable (HTTP {(int)response.StatusCode}); showing saved copy.");
            await response.Content.LoadIntoBufferAsync(1000000);
            var roster = JsonSerializer.Deserialize<SharedRoster>(await response.Content.ReadAsStringAsync(cancel.Token), Json);
            if (roster is null || roster.People is null || roster.People.Length > 100 || roster.People.Any(p => p is null || p.Characters is null || p.Characters.Any(c => c is null || c.Houses is null)))
                return new(null, "Invalid shared profile response; showing saved copy.");
            if (roster.CachedVoyages is { Length: > 500 } || roster.CachedVoyages?.Any(v=>v is null || v.CharacterId is null || v.CharacterName is null || v.World is null || v.FcName is null || v.ImportedAt.Year<2020 || v.ImportedAt>DateTimeOffset.UtcNow.AddMinutes(5) || !AutoRetainerCache.Valid(v.Data,DateTimeOffset.UtcNow)) == true)
                return new(null,"Invalid AutoRetainer cache response; showing saved copy.");
            if (roster.Voyages is { Length: > 200 } || roster.Voyages?.Any(v=>v is null || v.Submarines is null || v.Submarines.Length>4 || v.Submarines.Any(s=>s is null || s.Name is null || s.ReturnTime<0 || s.ReturnTime>DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds())) == true)
                return new(null,"Invalid shared voyage response; showing saved copy.");
            if (roster.GardenPlans is { Length: > 200 } || roster.GardenPlans?.Any(p => p is null || p.Beds is null || p.Beds.Length > 8 || p.Batch < 1 || p.Batch > 20 || p.Beds.Any(b => b is null || b.Bed < 1 || b.Bed > 8 || b.Crop is null || b.Soil is null || b.Days < 0 || b.Days > 365)) == true)
                return new(null, "Invalid garden plan response; showing saved copy.");
            if (roster.GardenCare is { Length: > 1000 } || roster.GardenCare?.Any(g => g is null || g.CharacterIds is null || g.Beds is null || g.Beds.Length > 8 || g.Beds.Any(b => b is null || b.Bed < 1 || b.Bed > 8)) == true)
                return new(null, "Invalid garden care response; showing saved copy.");
            rosterETag = response.Headers.ETag?.ToString();
            rosterETagKey = key;
            return new(roster, "Shared profiles updated.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        { return new(null, "Shared profiles unavailable; showing saved copy."); }
    }
    private sealed record Receipt(string[] Accepted);
    public void Dispose() { cancel.Cancel(); client.Dispose(); cancel.Dispose(); }
}

// Preserve legacy base64 caches while sending the numeric array required by the API.
public sealed class SubmarineRouteJson : System.Text.Json.Serialization.JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if(reader.TokenType==JsonTokenType.String)return reader.GetBytesFromBase64();
        if(reader.TokenType!=JsonTokenType.StartArray)throw new JsonException("Expected submarine route array.");
        var result=new List<byte>();
        while(reader.Read()&&reader.TokenType!=JsonTokenType.EndArray){if(result.Count>=5)throw new JsonException("Too many route sectors.");result.Add(reader.GetByte());}
        return result.ToArray();
    }
    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {writer.WriteStartArray();foreach(var sector in value)writer.WriteNumberValue(sector);writer.WriteEndArray();}
}
