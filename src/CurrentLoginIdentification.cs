namespace EquinoxCompanion;

// Fresh game evidence only. This object is never serialized. A stable ready
// account/list sample identifies this launch independently of the clicked target.
internal sealed class CurrentLoginIdentification
{
    private string sample = "";
    private DateTimeOffset sampledAt;
    public bool ReturningToTitle { get; private set; }
    public bool ReturnRequested { get; set; }
    public bool ServiceAccountSelected => SelectedServiceAccount >= 0;
    public int SelectedServiceAccount { get; private set; } = -1;

    public static int RecordedIndex(string key, IReadOnlyDictionary<string, int> indices) =>
        key.Length == 64 && key.All(char.IsAsciiHexDigit)
            && indices.TryGetValue(key, out var index) && index is >= 0 and <= 15 ? index : -1;

    public void Selected(int index)
    {
        if (index is < 0 or > 15 || ServiceAccountSelected)
            throw new InvalidOperationException("Service account selection is invalid or already sent.");
        SelectedServiceAccount = index;
    }

    public bool MatchesSelectedAccount(int index) =>
        !ServiceAccountSelected || index == SelectedServiceAccount;

    public bool Observe(int serviceAccount, IEnumerable<string> contentIds, bool ready, DateTimeOffset now)
    {
        var ids = contentIds.Where(id => ulong.TryParse(id, out var cid) && cid != 0).Distinct().Order(StringComparer.Ordinal).ToArray();
        if (!ready || serviceAccount < 0 || ids.Length == 0) { sample = ""; sampledAt = default; return false; }
        var next = serviceAccount + ":" + string.Join(",", ids);
        if (next != sample) { sample = next; sampledAt = now; return false; }
        return now - sampledAt >= TimeSpan.FromMilliseconds(200);
    }

    public void ReturnToTitle() { ReturningToTitle = true; ReturnRequested = false; }
}
