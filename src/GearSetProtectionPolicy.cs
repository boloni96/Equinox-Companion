namespace EquinoxCompanion;
internal sealed record SavedGearSet(string Name,bool Exists,uint[] Items);
internal static class GearSetProtectionPolicy
{
    internal static Dictionary<uint,string> Build(IEnumerable<SavedGearSet> sets)=>
        sets.Where(s=>s.Exists).SelectMany(s=>s.Items.Where(id=>id%1000000>0).Select(id=>(Item:id%1000000,s.Name)))
            .GroupBy(x=>x.Item).ToDictionary(g=>g.Key,g=>string.Join(", ",g.Select(x=>x.Name).Distinct()));
    internal static bool Protected(bool enabled,bool ready,uint item,IReadOnlyDictionary<uint,string> items)=>
        enabled&&item%1000000>0&&(!ready||items.ContainsKey(item%1000000));
}
