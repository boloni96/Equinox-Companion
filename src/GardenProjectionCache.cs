using System.Text.Json;

namespace EquinoxCompanion;

// Game observations are immutable records. Replace the action snapshot when the
// input lists/mappings change; unchanged frames do not traverse event history.
public sealed class GardenProjectionCache
{
    private sealed record Entry(SharedGardenPlan Source, SyncEvent[] Actions, SharedGardenPlan Result, long Generation);
    private readonly Dictionary<(string House, int Batch), Entry> entries = [];
    private Dictionary<(string World, string District, int Ward, int Plot, int Patch), SyncEvent[]> groups = [];
    private long generation;
    public int ProjectionCount { get; private set; }
    private static string Norm(string? value) => (value ?? "").Trim().Replace("The ", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

    public void SetActions(IEnumerable<SyncEvent> actions)
    {
        groups = actions.Where(e => e.Address is not null && e.Patch is not null)
            .OrderBy(e => e.At)
            .GroupBy(e => (Norm(e.Address!.WorldName), Norm(e.Address.DistrictName), e.Address.Ward, e.Address.Plot, e.Patch!.Value))
            .ToDictionary(g => g.Key, g => g.ToArray());
        generation++;
    }

    public SyncEvent[] ActionsFor(SharedGardenPlan plan)
    {
        var key = (Norm(plan.World), Norm(plan.District), plan.Ward, plan.Plot, plan.PhysicalPatch > 0 ? plan.PhysicalPatch : plan.Batch);
        if (!groups.TryGetValue(key, out var group)) return [];
        // Keep the existing full-address and game-house identity checks.
        return group.Where(e => SharedGardenLocation.Match(e.Address, [plan]) == plan.HouseId).ToArray();
    }

    private static bool SamePlan(SharedGardenPlan a, SharedGardenPlan b) =>
        ReferenceEquals(a, b) || (a with { Beds = Array.Empty<SharedGardenBed>(), Definition = null }) == (b with { Beds = Array.Empty<SharedGardenBed>(), Definition = null }) && a.Beds.SequenceEqual(b.Beds) &&
        (ReferenceEquals(a.Definition,b.Definition) || JsonSerializer.Serialize(a.Definition) == JsonSerializer.Serialize(b.Definition));

    public SharedGardenPlan Get(SharedGardenPlan plan, Func<SharedGardenPlan, SyncEvent[], SharedGardenPlan> project)
    {
        var key = (plan.HouseId, plan.Batch);
        entries.TryGetValue(key, out var old);
        if (old is not null && old.Generation == generation && ReferenceEquals(old.Source, plan)) return old.Result;
        var actions = ActionsFor(plan);
        if (old is not null && SamePlan(old.Source, plan) && old.Actions.SequenceEqual(actions))
        {
            entries[key] = old with { Source = plan, Generation = generation };
            return old.Result;
        }
        var result = project(plan, actions);
        entries[key] = new(plan, actions, result, generation);
        ProjectionCount++;
        return result;
    }

    public void Retain(IEnumerable<SharedGardenPlan> plans)
    {
        var keys = plans.Select(p => (p.HouseId, p.Batch)).ToHashSet();
        foreach (var key in entries.Keys.Where(k => !keys.Contains(k)).ToArray()) entries.Remove(key);
    }
}

// Current producers append immutable observations or remove acknowledged history.
// List identity/count/tail also catches pruning, replacing a list and session reset.
public readonly record struct GardenListStamp(object? List, int Count, object? Last)
{
    public static GardenListStamp Of<T>(IReadOnlyList<T>? list) => new(list, list?.Count ?? 0, list is { Count: > 0 } ? list[^1] : null);
}
