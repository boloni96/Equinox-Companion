using System.Numerics;
namespace EquinoxCompanion;

public sealed record CofferPoint(ulong Id, uint BaseId, Vector3 Position, bool Opened, bool Present);
public sealed class CofferMemory
{
    private readonly Dictionary<ulong, CofferPoint> points = [];
    public IReadOnlyCollection<CofferPoint> Points => points.Values;
    public void Clear() => points.Clear();
    public bool Observe(IEnumerable<CofferPoint> snapshot)
    {
        var before = points.Values.ToArray();
        foreach (var id in points.Keys.ToArray())
        {
            if (!points[id].Opened) points.Remove(id);
            else points[id] = points[id] with { Present = false };
        }
        foreach (var point in snapshot)
        {
            var opened = point.Opened;
            if (points.TryGetValue(point.Id, out var prior) && prior.BaseId == point.BaseId && Vector3.DistanceSquared(prior.Position, point.Position) < .25f)
                opened |= prior.Opened;
            points[point.Id] = point with { Opened = opened, Present = true };
        }
        // A visit cannot accumulate unbounded state; keep current objects before historic locations.
        foreach (var point in points.Values.OrderByDescending(p => p.Present).Skip(100).ToArray()) points.Remove(point.Id);
        return before.Length != points.Count || before.Any(p => !points.TryGetValue(p.Id,out var current) || current != p);
    }
}

public static class CofferMapProjection
{
    public static bool SameMap(uint territory, uint map, uint selectedTerritory, uint selectedMap) =>
        territory != 0 && map != 0 && territory == selectedTerritory && map == selectedMap;
    // World X/Z -> normalized location in the game's 2048-pixel map texture.
    public static Vector2 TexturePosition(Vector3 point, short offsetX, short offsetY, float sizeFactor) =>
        (new Vector2(point.X + offsetX, point.Z + offsetY) * sizeFactor + new Vector2(1024)) / 2048;
}
