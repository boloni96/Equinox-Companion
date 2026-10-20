using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class GardenMarkerClip223Tests
{
    [ModuleInitializer] public static void Run()
    {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("GardenMarkerClip223: " + why); }
        var paint = new Vector4(95, 95, 149, 149); // Includes the icon border's stroke and antialiasing.
        var regions = new Vector4[GardenMarkerClip.MaxRegions];
        bool Contains(Vector4 r, float x, float y) => x >= r.X && x < r.Z && y >= r.Y && y < r.W;
        bool Visible(int count, float x, float y) => regions.Take(count).Any(r => Contains(r, x, y));
        var count = GardenMarkerClip.VisibleRegions(paint, [], regions);
        Check(count == 1 && regions[0] == paint, "no tooltip keeps the complete outline");
        count = GardenMarkerClip.VisibleRegions(paint, [new(200, 200, 300, 300)], regions);
        Check(count == 1 && regions[0] == paint, "unrelated tooltip cannot hide borders");
        count = GardenMarkerClip.VisibleRegions(paint, [new(130, 148, 300, 300)], regions);
        Check(Visible(count, 110, 98) && Visible(count, 98, 120) && Visible(count, 146, 120), "tooltip touching the bottom margin preserves the other edges");
        Check(!Visible(count, 140, 148.5f), "covered fragment stays below the native tooltip");
        count = GardenMarkerClip.VisibleRegions(paint, [new(125, 90, 300, 300)], regions);
        Check(Visible(count, 98, 120) && Visible(count, 110, 98) && Visible(count, 110, 146), "half-covered item keeps its visible outline");
        Check(!Visible(count, 146, 120), "covered right edge never draws over game text");
        Check(GardenMarkerClip.VisibleRegions(paint, [new(90, 90, 300, 300)], regions) == 0, "fully covered icon remains behind the popup");
        count = GardenMarkerClip.VisibleRegions(paint, [], regions);
        Check(count == 1 && regions[0] == paint, "closing hover restores every border immediately");
        count = GardenMarkerClip.VisibleRegions(paint, [new(float.NaN, 0, 10, 10)], regions);
        Check(count == 1, "invalid cover geometry cannot hide a valid border");
        Check(GardenMarkerClip.VisibleRegions(default, [], regions) == 0, "invalid image geometry cannot draw");

        // Verify the partition against point membership, independently of the
        // rectangle-splitting algorithm, including overlapping native windows.
        var random = new Random(223);
        for (var sample = 0; sample < 40; sample++)
        {
            var covers = Enumerable.Range(0, 3).Select(_ => {
                var x = random.Next(-5, 20); var y = random.Next(-5, 20);
                return new Vector4(x, y, x + random.Next(1, 15), y + random.Next(1, 15));
            }).ToArray();
            count = GardenMarkerClip.VisibleRegions(new(0, 0, 20, 20), covers, regions);
            for (var x = 0; x < 20; x++) for (var y = 0; y < 20; y++)
            {
                var px = x + .5f; var py = y + .5f;
                var expected = !covers.Any(c => Contains(c, px, py));
                var found = regions.Take(count).Count(r => Contains(r, px, py));
                Check(found == (expected ? 1 : 0), "visible pixels must appear exactly once and covered pixels never");
            }
        }
        Console.WriteLine($"PASS 223 partial tooltip clipping checks: {checks}");
    }
}
