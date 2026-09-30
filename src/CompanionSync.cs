using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;

public sealed record SyncEvent(string Id, string Kind, DateTimeOffset At, Actor Actor, Address? Address, int? Patch = null, int? Bed = null, PlantDetails? Plant = null, HouseDetails? House = null, CharacterDetails? Character = null);
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
    private sealed record Receipt(string[] Accepted);
    public void Dispose() { cancel.Cancel(); client.Dispose(); cancel.Dispose(); }
}
