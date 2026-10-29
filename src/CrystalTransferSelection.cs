namespace EquinoxCompanion;
internal static class CrystalTransferSelection
{
    internal static uint[] Items(int family,int element)=>Enumerable.Range(2,18).Select(x=>(uint)x)
        .Where(id=>(family==0||(id-2)/6==family-1)&&(element==0||(id-2)%6==element-1)).ToArray();
    internal static Dictionary<uint,int> Goals(bool withdraw,int family,int element,int amount,int keep,IEnumerable<ChestStack> bags)
    {
        var held=bags.Where(x=>x.Container==2001).GroupBy(x=>x.Item).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        return Items(family,element).ToDictionary(id=>id,id=>withdraw?Math.Min(9999,held.GetValueOrDefault(id)+Math.Clamp(amount,1,9999)):Math.Max(0,held.GetValueOrDefault(id)-Math.Max(0,keep)));
    }
}
