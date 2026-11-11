namespace EquinoxCompanion;
internal sealed record StorageWithdrawalPage(string Key,uint[] Items);
internal static class StorageWithdrawalPlan
{
    internal static bool Supported(string key){
        var parts=key.Split(':');
        if(!uint.TryParse(parts[^1],out var type))return false;
        return parts.Length==2&&parts[0]=="bag"&&(LocalStoragePolicy.Saddle(type)||LocalStoragePolicy.Armoury(type))||
            parts.Length==3&&parts[0]=="house"&&LocalStoragePolicy.Storeroom(type);
    }
    internal static StorageWithdrawalPage[] Build(IEnumerable<(string Key,uint Item)> rows)=>rows
        .Where(x=>x.Item>0&&Supported(x.Key)).GroupBy(x=>x.Key)
        .Select(g=>new StorageWithdrawalPage(g.Key,g.Select(x=>x.Item).Distinct().ToArray())).ToArray();
}
