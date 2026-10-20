using System.Numerics;

namespace EquinoxCompanion;

// Scissor regions for one border. A tooltip touching one edge must not hide the
// whole item; redraw the same outline only in portions outside native windows.
public static class GardenMarkerClip
{
    public const int MaxCovers = 3;
    public const int MaxRegions = 64; // At most four fragments per cover.

    private static bool Valid(Vector4 r) => float.IsFinite(r.X) && float.IsFinite(r.Y) &&
        float.IsFinite(r.Z) && float.IsFinite(r.W) && r.Z > r.X && r.W > r.Y;

    public static int VisibleRegions(Vector4 paint, ReadOnlySpan<Vector4> covers, Span<Vector4> output)
    {
        if (!Valid(paint) || covers.Length > MaxCovers || output.Length < MaxRegions) return 0;
        output[0] = paint;
        var count = 1;
        Span<Vector4> next = stackalloc Vector4[MaxRegions];
        foreach (var cover in covers)
        {
            if (!Valid(cover)) continue;
            var written = 0;
            for (var i = 0; i < count; i++)
            {
                var r = output[i];
                var left = Math.Max(r.X, cover.X); var top = Math.Max(r.Y, cover.Y);
                var right = Math.Min(r.Z, cover.Z); var bottom = Math.Min(r.W, cover.W);
                if (left >= right || top >= bottom) { next[written++] = r; continue; }
                if (r.Y < top) next[written++] = new(r.X, r.Y, r.Z, top);
                if (bottom < r.W) next[written++] = new(r.X, bottom, r.Z, r.W);
                if (r.X < left) next[written++] = new(r.X, top, left, bottom);
                if (right < r.Z) next[written++] = new(right, top, r.Z, bottom);
            }
            next[..written].CopyTo(output); count = written;
            if (count == 0) break;
        }
        return count;
    }
}
