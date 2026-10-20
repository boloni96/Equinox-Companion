namespace EquinoxCompanion;

// Presentation state only. Resolve selected beds through the same physical-patch
// mapping as /gardening; expanding a panel never confirms a garden action.
public sealed class AdventureGardenNavigation
{
    private readonly GardenSelectionFollow follow = new();
    private readonly HashSet<string> houses = new(StringComparer.Ordinal);
    private AdventureStop? cachedStop;
    private string cachedKind = "";
    private (string Id, string Generation, int Index)? scope;
    public bool HasGardens => houses.Count > 0;
    public bool Contains(string house) => houses.Contains(house);

    public static AdventureStop? CurrentStop(AdventureBoard? board) =>
        board is not null && board.Index >= 0 && board.Index < board.Stops.Length ? board.Stops[board.Index] : null;

    public static bool HasGarden(AdventureBoard? board) => CurrentStop(board) is {} stop &&
        (stop.HouseId.Length > 0 || stop.Tasks.Any(t => AdventurePolicy.IsGardenTask(t) && t.Rule!.HouseId.Length > 0));

    public void Set(AdventureBoard? board)
    {
        var next = board is null ? ((string, string, int)?)null : (board.Id, board.Generation, board.Index);
        if (scope != next) { scope = next; follow.Reset(); }
        var stop = CurrentStop(board);
        var kind = board?.Kind ?? "";
        if (ReferenceEquals(stop, cachedStop) && kind == cachedKind) return;
        cachedStop = stop; cachedKind = kind;
        houses.Clear();
        if (stop is null) return;
        if (stop.HouseId.Length > 0) houses.Add(stop.HouseId);
        foreach (var task in stop.Tasks)
            if (AdventurePolicy.IsGardenTask(task) && task.Rule!.HouseId.Length > 0) houses.Add(task.Rule.HouseId);
    }

    public (string House, int Batch, int Bed)? Follow(GardenSnapshot? target, Address? address,
        bool outdoors, bool syncingBeds, IEnumerable<SharedGardenTarget> mappings,
        IEnumerable<SharedGardenPlan> plans, DateTimeOffset now)
    {
        if (!HasGardens || target is null || !outdoors || syncingBeds || target.Address != address ||
            target.ObservedAt > now || now - target.ObservedAt >= TimeSpan.FromSeconds(2))
        { follow.Reset(); return null; }
        return follow.Update(target, mappings, plans.Where(p => Contains(p.HouseId)), now);
    }

    public static (string House, int Batch, int Bed)? TaskTarget(AdventureTask task, AdventureStop stop) =>
        task.Rule is { Batch: >= 1 and <= 3, Bed: >= 0 and <= 8 } rule && rule.HouseId.Length > 0 &&
        (AdventurePolicy.IsGardenTask(task) || rule.HouseId == stop.HouseId)
            ? (rule.HouseId, rule.Batch, rule.Bed) : null;

    // A closed/minimized Gardening tab may be on a different batch. Adventure
    // shows all batches and uses the numbered game menu as its batch authority.
    public static bool UseStandaloneBatch(bool gardeningOpen, bool adventureAtHouse) => gardeningOpen && !adventureAtHouse;
}
