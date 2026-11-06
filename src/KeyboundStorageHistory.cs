namespace EquinoxCompanion;
public static class KeyboundStorageHistory
{
    public static HashSet<uint> Read(IEnumerable<SyncEvent> rows,string character,IReadOnlySet<uint> rewards)
    {
        var result=new HashSet<uint>();
        foreach(var e in rows){
            if(e.Kind!="storage.observed"||e.Actor.ContentId!=character||e.Storage is not {} storage||
                !(storage.Key.StartsWith("bag:")||storage.Key.StartsWith("retainer:")||storage.Key is "armoire" or "dresser"))continue;
            foreach(var item in storage.Items)if(rewards.Contains(item))result.Add(item);
        }
        return result;
    }
}
