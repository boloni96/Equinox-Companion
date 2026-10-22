namespace EquinoxCompanion;

// Fresh game evidence only. This object is never serialized. A stable ready
// account/list sample identifies this launch independently of the clicked target.
internal sealed class CurrentLoginIdentification
{
    private string sample = "";
    private DateTimeOffset sampledAt;
    public bool ReturningToTitle { get; private set; }
    public bool ReturnRequested { get; set; }
    public bool ServiceAccountSelected { get; set; }

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
