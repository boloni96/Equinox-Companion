namespace EquinoxCompanion;
internal static class LocalStoragePolicy
{
    internal static bool Saddle(uint type)=>type is 4000 or 4001 or 4100 or 4101;
    internal static bool Storeroom(uint type)=>type is 27000 or 27200 or >=27001 and <=27011;
    internal static bool CanMove(uint source,uint destination)=>source<=3&&(Saddle(destination)||Storeroom(destination))||destination<=3&&(Saddle(source)||Storeroom(source));
    internal static IEnumerable<StorageStack> Copies(StorageStack stack)=>stack.Positions?.Any(p=>p.Bound.HasValue||p.Tradable.HasValue)==true
        ?stack.Positions.GroupBy(p=>(p.Bound,p.Tradable)).Select(g=>stack with{Quantity=g.Sum(p=>p.Quantity),Positions=g.ToArray(),Bound=g.Key.Bound,Tradable=g.Key.Tradable})
        :[stack];
    internal static bool Filter(StorageStack s,int filter)=>filter switch{0=>true,1=>s.Tradable==true,2=>s.Tradable==false,3=>s.Bound==true,4=>s.Bound==false,_=>false};
    internal static bool View(string key,int view)=>view switch{
        2=>true,3=>key is "bag:4000" or "bag:4001" or "bag:4100" or "bag:4101",
        4=>key.StartsWith("house:"),5=>key.StartsWith("bag:")&&uint.TryParse(key.AsSpan(4),out var t)&&t is >=3200 and <=3500,
        6=>key=="bag:1000",7=>key.StartsWith("armoire")||key.StartsWith("dresser"),8=>key.StartsWith("fc:"),_=>false};
}
