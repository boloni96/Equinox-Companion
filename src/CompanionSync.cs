using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed record SyncEvent(string Id, string Kind, DateTimeOffset At, Actor Actor, Address? Address, int? Patch = null, int? Bed = null, PlantDetails? Plant = null, HouseDetails? House = null, CharacterDetails? Character = null, CropDetails? Crop = null);
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
    public async Task<RosterResult> ReadRoster(string key, long? revision)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://equinoxjournal.pages.dev/api/companion/roster");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            if (revision is not null) request.Headers.TryAddWithoutValidation("If-None-Match", $"\"roster-{revision}\"");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            if (response.StatusCode == HttpStatusCode.NotModified) return new(null, "Shared profiles up to date.", true);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new(null, "Shared profiles: pairing key invalid or revoked.", Unauthorized: true);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(null, "Shared profiles need Journal V7.9.23 and a new website save.");
            if (!response.IsSuccessStatusCode) return new(null, $"Shared profiles unavailable (HTTP {(int)response.StatusCode}); showing saved copy.");
            await response.Content.LoadIntoBufferAsync(1000000);
            var roster = JsonSerializer.Deserialize<SharedRoster>(await response.Content.ReadAsStringAsync(cancel.Token), Json);
            if (roster is null || roster.People is null || roster.People.Length > 100 || roster.People.Any(p => p is null || p.Characters is null || p.Characters.Any(c => c is null || c.Houses is null)))
                return new(null, "Invalid shared profile response; showing saved copy.");
            return new(roster, "Shared profiles updated.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        { return new(null, "Shared profiles unavailable; showing saved copy."); }
    }
    private sealed record Receipt(string[] Accepted);
    public void Dispose() { cancel.Cancel(); client.Dispose(); cancel.Dispose(); }
}
