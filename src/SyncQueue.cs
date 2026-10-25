namespace EquinoxCompanion;

// Recent observations get four slots for every history slot. Neither lane can
// starve, and payload trimming retains this balance instead of dropping history.
public static class SyncQueue
{
    public static SyncEvent[] Select(IEnumerable<SyncEvent> source, Func<SyncEvent[], int> bytes, bool historyFirst = false)
    {
        var rows=source.ToArray();
        if(!rows.Any(e=>e.Kind=="market.retainers"))return SelectLane(rows,bytes,historyFirst,50,60000);
        var sizes=new Dictionary<string,int>();
        int Size(SyncEvent[] item){if(!sizes.TryGetValue(item[0].Id,out var size))sizes[item[0].Id]=size=bytes(item);return size;}
        var gameplay=SelectLane(rows.Where(e=>e.Kind!="market.retainers"),Size,historyFirst,50,60000);
        var market=SelectLane(rows.Where(e=>e.Kind=="market.retainers"),Size,historyFirst,50-gameplay.Length,60000-gameplay.Sum(e=>Size([e])));
        return [..gameplay,..market];
    }
    private static SyncEvent[] SelectLane(IEnumerable<SyncEvent> source,Func<SyncEvent[],int> bytes,bool historyFirst,int maxCount,int maxBytes)
    {
        var ordered = source.DistinctBy(e => e.Id).OrderBy(e => e.At).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
        var selected = new List<SyncEvent>();
        int oldest = 0, newest = ordered.Length - 1, slot = historyFirst ? 4 : 0, usedBytes = 0;
        while (oldest <= newest && selected.Count < maxCount)
        {
            var item = slot++ % 5 == 4 ? ordered[oldest++] : ordered[newest--];
            // Sum singleton envelopes conservatively. Serialize each candidate once,
            // not the growing batch fifty times on the game update thread.
            var itemBytes = bytes([item]);
            if (usedBytes + itemBytes > maxBytes) continue;
            usedBytes += itemBytes;
            selected.Add(item);
        }
        // Send each selected batch in observation order; registration resolves first.
        return selected.OrderBy(e => e.Kind == "character.registered" ? 0 : 1).ThenBy(e => e.At).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
    }
}
