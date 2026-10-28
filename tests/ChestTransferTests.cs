using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class ChestTransferTests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS chest "+name);}
        ChestStack[] chest=[new(20000,0,100,80,false,99),new(20000,1,100,20,true,99),new(20000,2,0,0,false,0)];
        ChestStack[] bags=[new(0,0,100,10,false,99),new(0,1,0,0,false,0)];
        var move=ChestTransferPlanner.Withdraw(chest,bags,new Dictionary<uint,int>{{100,15}},RetrievalQuality.NqOnly)!;
        Check("withdraw only missing amount",move.Quantity==5&&move.Source.Slot==0&&move.Destination.Slot==0);
        move=ChestTransferPlanner.Withdraw(chest,bags,new Dictionary<uint,int>{{100,15}},RetrievalQuality.HqOnly)!;
        Check("HQ does not count NQ held or merge with NQ",move.Quantity==15&&move.Source.Hq&&move.Destination.Slot==1);
        Check("full bags cannot swap another item",ChestTransferPlanner.Withdraw(chest,[new(0,0,200,99,false,99)],new Dictionary<uint,int>{{100,15}},RetrievalQuality.Any)==null);
        move=ChestTransferPlanner.Duplicates(chest,bags,4)!;
        Check("duplicate deposit reserves bag quantity",move.Quantity==6&&move.Source.Container==0&&move.Destination.Container==20000);
        Check("no cross-quality duplicates",ChestTransferPlanner.Duplicates([chest[1]],bags,0)==null);
        Check("non-depositable stack ignored",ChestTransferPlanner.Duplicates(chest,[bags[0] with{CanDeposit=false}],0)==null);
        move=ChestTransferPlanner.Merge([chest[0],chest[0] with{Slot=2,Quantity=30}])!;
        Check("merge stops at stack capacity",move.Quantity==19&&move.Source.Slot==2&&move.Destination.Slot==0);
        Check("dispatch is not confirmation",!ChestTransferPlanner.Confirmed(move,move.Source,move.Destination));
        Check("both changes confirm transfer",ChestTransferPlanner.Confirmed(move,move.Source with{Quantity=11},move.Destination with{Quantity=99}));
        var crystal= new ChestStack(12001,0,2,8000,false,9999);
        var crystalBag=new ChestStack(2001,0,2,9990,false,9999);
        move=ChestTransferPlanner.Transfer([crystal],[crystalBag],new HashSet<uint>{2},RetrievalQuality.Any)!;
        Check("crystals use remaining dedicated capacity",move.Quantity==9&&move.Destination.Container==2001);
        Check("HQ only never substitutes crystals",ChestTransferPlanner.Transfer([crystal],[crystalBag],new HashSet<uint>{2},RetrievalQuality.HqOnly)==null);
        move=ChestTransferPlanner.Transfer([chest[0]],[new(0,0,100,95,false,99)],new HashSet<uint>{100},RetrievalQuality.Any)!;
        Check("partial bag stack supports another trip",move.Quantity==4);
        Check("category selection excludes unrelated items",ChestTransferPlanner.Transfer([chest[0]],bags,new HashSet<uint>{200},RetrievalQuality.Any)==null);
        move=ChestTransferPlanner.Merge([chest[0],chest[0] with{Slot=2,Quantity=30}])!;
        Check("one-sided change not confirmation",!ChestTransferPlanner.Confirmed(move,move.Source with{Quantity=11},move.Destination));
    }
}
