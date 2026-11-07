using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class OrganizerRecovery492Tests
{
    [ModuleInitializer] internal static void Run(){
        var source=new ChestStack(10000,22,28710,1,false,1);
        var empty=new ChestStack(1,1,0,0,false,0);
        var move=new ChestMove(source,empty,1);
        if(OrganizerTransferRecovery.Check(move,source,empty,TimeSpan.FromSeconds(7))!=OrganizerTransferState.Waiting)throw new Exception("Wait for delayed inventory updates");
        if(OrganizerTransferRecovery.Check(move,source,empty,TimeSpan.FromSeconds(8))!=OrganizerTransferState.Unchanged)throw new Exception("Unchanged move must not wait sixty seconds");
        var received=empty with{Item=28710,Quantity=1,Limit=1};
        if(OrganizerTransferRecovery.Check(move,source with{Item=0,Quantity=0},received,TimeSpan.FromSeconds(10))!=OrganizerTransferState.Confirmed)throw new Exception("Late confirmation has precedence");
        if(OrganizerTransferRecovery.Check(move,source with{Item=0,Quantity=0},empty,TimeSpan.FromSeconds(8))!=OrganizerTransferState.Changed)throw new Exception("One-sided delta must stop without retry");
        var another=new ChestMove(source with{Item=42},empty,1);
        RoutedMove[] plan=[new(1,move,true),new(1,another,true),new(2,new(received,source with{Item=0,Quantity=0},1),false),new(2,another,false)];
        var remaining=OrganizerTransferRecovery.DeferItem(plan,0);
        if(remaining.Length!=2||remaining.Any(x=>x.Move.Source.Item==28710)||!remaining[0].Withdraw||remaining[1].Withdraw)throw new Exception("Remove failed item's dependent delivery, retain unrelated cargo intent");
        Console.WriteLine("492 delayed confirmation, unchanged transfer, ambiguous delta and dependent delivery recovery passed.");
    }
}
