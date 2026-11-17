using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class Workshop573Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop chest "+name);}
        Check("one finished part deposited",WorkshopPolicy.DepositReceived(3,2,0));
        Check("no bag change rejected",!WorkshopPolicy.DepositReceived(3,3,0));
        Check("unconfirmed chest goal rejected",!WorkshopPolicy.DepositReceived(3,2,1));
        Check("unrelated larger bag change rejected",!WorkshopPolicy.DepositReceived(3,1,0));
        var q=new WorkshopQueueSave{PendingDeposit=21794,Completed=1,Items=[new(21794,"Hull",1)]};
        var saved=JsonSerializer.Deserialize<WorkshopQueueSave>(JsonSerializer.Serialize(q))!;
        Check("pending finished deposit survives reload",saved.PendingDeposit==21794&&saved.Completed==1);
        WorkshopPolicy.Clear(saved,1,"workshop");Check("clear removes pending deposit",saved.PendingDeposit==0);
    }
}
