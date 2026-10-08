using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Sync93Tests
{
    [ModuleInitializer] internal static void Run()
    {
        static void Check(string name, bool ok) { if (!ok) throw new Exception(name); Console.WriteLine("PASS 93 " + name); }
        var epoch = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var events = Enumerable.Range(1, 10000).Select(i => new SyncEvent(i.ToString("x32"), "house.entered", epoch.AddSeconds(i), new("1", "Test Actor", 410, 410), null)).ToArray();
        var first = SyncQueue.Select(events, rows => rows.Length * 100);
        Check("fresh observation bypasses ten thousand old records", first.Contains(events[^1]));
        Check("history advances in the same batch", first.Contains(events[0]));
        Check("bounded unique batch", first.Length == 50 && first.DistinctBy(e => e.Id).Count() == 50);
        Check("forty recent and ten historic slots", first.Count(e => e.At <= events[9].At) == 10 && first.Count(e => e.At >= events[^40].At) == 40);
        Check("selected observations ordered chronologically", first.SequenceEqual(first.OrderBy(e => e.At)));
        var second = SyncQueue.Select(events.Except(first), rows => rows.Length * 100);
        Check("acknowledged IDs not resent", !second.Intersect(first).Any());
        Check("next oldest record progresses", second.Contains(events[10]));
        var compact = SyncQueue.Select(events, rows => rows.Length * 12000);
        Check("large records keep recent and history progress", compact.Length == 5 && compact.Contains(events[0]) && compact.Contains(events[^1]));
        var oversized = SyncQueue.Select(events, rows => rows.Any(e => e == events[^1]) ? 70000 : rows.Length * 100);
        Check("oversized observation cannot block valid actions", oversized.Length == 50 && !oversized.Contains(events[^1]));
        Check("duplicate local IDs occur once", SyncQueue.Select([events[0], events[0]], rows => 100).Length == 1);
        Check("large historic record gets reserved first slot", SyncQueue.Select(events, rows => rows[0] == events[0] ? 60000 : 100, historyFirst: true).Contains(events[0]));
        Check("large recent record gets reserved first slot", SyncQueue.Select(events, rows => rows[0] == events[^1] ? 60000 : 100).Contains(events[^1]));
        Check("empty queue", SyncQueue.Select([], rows => 100).Length == 0);
        Check("legacy receipt stays compatible", SyncReceipt.Parse("{\"accepted\":[\"a\"]}", ["a"]).Accepted.Length == 1);
        Check("duplicate receipt says already stored", SyncReceipt.Parse("{\"accepted\":[\"a\"],\"inserted\":0,\"duplicates\":1}", ["a"]).Status.Contains("1 already stored"));
        Check("new receipt distinguishes inserted", SyncReceipt.Parse("{\"accepted\":[\"a\"],\"inserted\":1,\"duplicates\":0}", ["a"]).Status.Contains("1 new"));
        Check("unrelated acknowledgement rejected", SyncReceipt.Parse("{\"accepted\":[\"b\"]}", ["a"]).Retry);
        Check("inconsistent receipt counts rejected", SyncReceipt.Parse("{\"accepted\":[\"a\"],\"inserted\":3,\"duplicates\":0}", ["a"]).Retry);
        Check("repeated acknowledgement rejected", SyncReceipt.Parse("{\"accepted\":[\"a\",\"a\"]}", ["a"]).Retry);
    }
}
