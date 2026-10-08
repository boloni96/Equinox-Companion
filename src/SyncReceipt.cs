using System.Text.Json;
namespace EquinoxCompanion;
public static class SyncReceipt
{
    private sealed record Receipt(string[]? Accepted, int? Inserted = null, int? Duplicates = null);
    public static SyncResult Parse(string body, IEnumerable<string> sentIds)
    {
        var result = JsonSerializer.Deserialize<Receipt>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var sent = sentIds.ToHashSet();
        if (result?.Accepted is null || result.Accepted.Any(x => x is null || !sent.Contains(x)) || result.Accepted.Distinct().Count() != result.Accepted.Length)
            return new([], "Unexpected website response; local records are kept.", true);
        if (result.Inserted is not null || result.Duplicates is not null)
        {
            if (result.Inserted is not >= 0 || result.Duplicates is not >= 0 || result.Inserted > result.Accepted.Length || result.Duplicates > result.Accepted.Length || result.Inserted + result.Duplicates != result.Accepted.Length)
                return new([], "Inconsistent website receipt; local records are kept.", true);
            return new(result.Accepted, $"Server acknowledged {result.Inserted} new · {result.Duplicates} already stored. Journal application is separate.", false);
        }
        return new(result.Accepted, $"Server acknowledged {result.Accepted.Length} events (may include duplicates). Journal V7.11.90 shows new/duplicate counts.", false);
    }
}
