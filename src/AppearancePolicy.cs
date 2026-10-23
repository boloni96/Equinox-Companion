namespace EquinoxCompanion;

// Local UI preferences: never part of paired gameplay state.
public sealed record AppearancePreference(bool GoddessBlue = false, int ChoiceRevision = 0);
public static class AppearancePolicy
{
    public const int Revision = 1;
    public static bool NeedsChoice(AppearancePreference? choice) => choice is null || choice.ChoiceRevision < Revision;
    public static AppearancePreference Choose(bool goddessBlue) => new(goddessBlue, Revision);
    public static AppearancePreference Dismiss(AppearancePreference? choice) => new(choice?.GoddessBlue ?? false, Revision);
    public static string SelectSection(string? selected, IEnumerable<string> available)
    {
        var ids = available.ToArray();
        return selected is not null && ids.Contains(selected) ? selected : ids.FirstOrDefault() ?? "housing";
    }
}

public readonly record struct CompactNavigation(float SelectorWidth, bool WrapActions);
public static class CompactLayout
{
    // Measurements are in the active font's pixels, not fixed screen pixels.
    public static CompactNavigation Navigation(float available, float actions, float gap, float fontSize)
    {
        available = Math.Max(1, available);
        var minimum = Math.Max(1, fontSize * 6);
        var cap = Math.Max(minimum, fontSize * 10);
        var room = available - actions - gap;
        var wrap = room < minimum;
        return new(Math.Min(cap, Math.Max(1, wrap ? available : room)), wrap);
    }
}

public readonly record struct AdventureProgress(int Done, int Total, int StopsDone, int Stops)
{
    public static AdventureProgress Count(AdventureBoard board)
    {
        // Task ids are stable across travel regrouping; shared tasks count once.
        // Conflicting duplicates must not claim completion.
        var tasks = board.Stops.SelectMany(s => s.Tasks).GroupBy(t => t.Id).ToArray();
        return new(tasks.Count(g => g.All(t => t.Done)), tasks.Length,
            board.Stops.Count(AdventurePolicy.Complete), board.Stops.Length);
    }
}
