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

    // Persisted character evidence wins over account-wide inference. Display
    // labels (MAIN/ALT) never identify a native service-account slot.
    public static int RecordedServiceSlot(string scope,string contentId,string accountKey,IEnumerable<string> members,
        IReadOnlyDictionary<string,int> characters,IReadOnlyDictionary<string,int> accounts)
    {
        if(scope.Length!=64||!scope.All(char.IsAsciiHexDigit))return -1;
        int CharacterSlot(string id)=>ulong.TryParse(id,out var cid)&&cid!=0&&characters.TryGetValue(scope+":"+id,out var slot)&&slot is >=0 and <=15?slot:-1;
        var exact=CharacterSlot(contentId);if(exact>=0)return exact;
        var account=RecordedIndex(accountKey,accounts);if(account>=0)return account;
        var slots=members.Select(CharacterSlot).Where(i=>i>=0).Distinct().Take(2).ToArray();
        return slots.Length==1?slots[0]:-1;
    }

    public static bool WorldSettled(TimeSpan elapsed,bool populatedReady)=>
        elapsed>=TimeSpan.FromMilliseconds(populatedReady?500:2000);

    public static int WorldMenuIndex(IReadOnlyList<string> names,string world)
    {
        var match=-1;
        for(var i=0;i<names.Count;i++)if(string.Equals(names[i],world,StringComparison.Ordinal)){
            if(match>=0)return -1;match=i;
        }
        return match;
    }

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
