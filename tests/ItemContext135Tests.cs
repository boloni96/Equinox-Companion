using EquinoxCompanion;
internal static class ItemContext135Tests
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Run()
    {
        foreach(var (raw, expected) in new (ulong, uint)[] { (0,0), (123,123), (500123,123), (1000123,123), (1500123,123), (500000,0), (1000000,0), (2000000,0), (2000123,0), (ulong.MaxValue,0) })
        {
            if(ItemContextPolicy.Normalize(raw) != expected) throw new Exception($"Item context normalization failed for {raw}");
        }
        Console.WriteLine("PASS 135 normal, HQ, collectible, empty and event item context IDs");
    }
}
