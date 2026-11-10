using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class SaddlebagDuplicates533Tests
{
    [ModuleInitializer] internal static void Run(){
        ChestStack[] saddle=[new(4000,0,100,990,false,999),new(4001,0,0,0,false,0)];
        ChestStack[] bags=[new(0,0,100,30,false,999),new(0,1,200,4,false,999)];
        var move=ChestTransferPlanner.Duplicates(saddle,bags,0)??throw new Exception("Matching saddlebag stack must receive duplicates");
        if(move.Quantity!=9||move.Destination.Container!=4000)throw new Exception("Existing partial stack must fill before empty saddlebag slots");
        if(!ChestTransferPlanner.Confirmed(move,bags[0] with{Quantity=21},saddle[0] with{Quantity=999}))throw new Exception("Partial stack receipt must leave the remaining inventory quantity");
        saddle[0]=saddle[0] with{Quantity=999};
        move=ChestTransferPlanner.Duplicates(saddle,bags,0)!;
        if(move.Destination.Container!=4001||move.Quantity!=30)throw new Exception("Duplicate overflow must use another saddlebag page");
        if(ChestTransferPlanner.Duplicates(saddle,[bags[0] with{Hq=true}],0)!=null)throw new Exception("HQ must not match an NQ saddlebag item");
        if(ChestTransferPlanner.Duplicates([saddle[0]],bags,0)!=null)throw new Exception("Full saddlebag must not swap or overwrite items");
        if(ChestTransferPlanner.Duplicates(saddle,[bags[1]],0)!=null)throw new Exception("Unrelated inventory item must remain in inventory");
        ChestStack[] gear=[new(4000,0,300,1,false,1),new(4000,1,0,0,false,0)];
        move=ChestTransferPlanner.Duplicates(gear,[new(0,0,300,1,false,1)],0)!;
        if(move.Destination.Slot!=1)throw new Exception("Nonstackable duplicates require an empty slot to preserve metadata");
    }
}
