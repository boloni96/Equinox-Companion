using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class DuplicateExclusions540Tests
{
    [ModuleInitializer] internal static void Run(){
        ChestStack[] bags=[new(0,0,100,10,false,999),new(0,1,100,10,true,999),new(0,2,101,5,false,999)];
        ChestStack[] store=[new(4000,0,100,1,false,999),new(4000,1,100,1,true,999),new(4000,2,101,1,false,999)];
        var exclusions=new HashSet<uint>{100};
        var move=ChestTransferPlanner.Duplicates(store,bags,0,exclusions:exclusions);
        if(move?.Source.Item!=101)throw new Exception("Duplicate exclusions must skip HQ and NQ, then continue with other items");
        exclusions.Add(101);
        if(ChestTransferPlanner.Duplicates(store,bags,0,exclusions:exclusions)!=null)throw new Exception("All excluded means no duplicate transfers");
        exclusions.Clear();
        if(ChestTransferPlanner.Duplicates(store,bags,0,exclusions:exclusions)?.Source.Item!=100)throw new Exception("Removing an exclusion must allow the item again");
    }
}
