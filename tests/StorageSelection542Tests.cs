using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class StorageSelection542Tests
{
    [ModuleInitializer] internal static void Run(){
        var selection=new StorageInventorySelection();
        selection.Set("bag:4000",42,true);selection.Set("bag:4001",42,true);selection.Set("bag:4001",99,true);
        selection.Set("bag:4000",42,false);
        if(selection.Items("bag:4000").Any()||!selection.Contains("bag:4001",42))throw new Exception("Removing one location must preserve the same item in another");
        selection.Set("bag:4000",42,true);selection.Set("bag:4000",42,true);
        if(!selection.Items("bag:4000").SequenceEqual(new uint[]{42}))throw new Exception("A withdrawal must use only its source selection, without repeated hover duplicates");
        selection.Clear("bag:4000");if(selection.Items("bag:4001").Count()!=2)throw new Exception("Scoped clear must preserve other locations");
        selection.Clear();if(selection.Items("bag:4001").Any())throw new Exception("All-locations clear must remove all selections");
    }
}
