using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Workshop570Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop "+name);}
        Check("near station permits native interaction",WorkshopPolicy.StationNearby(25));
        Check("distant and invalid stations rejected",!WorkshopPolicy.StationNearby(49)&&!WorkshopPolicy.StationNearby(float.NaN));
        var row=new WorkshopSupply(5366,"Cedar Lumber",3,3,2);
        var before=new WorkshopPhase(21794,0,3,[row,new(1,"Other",10,3,3)]);
        string[] menu=["Advance to the next phase of production. (Quality: 59/100)","View company crafting log."];
        Check("last delivery closes window and advances",WorkshopPolicy.FinalDeliveryMenuReceipt(before,row,9,6,menu));
        Check("menu alone is insufficient",!WorkshopPolicy.FinalDeliveryMenuReceipt(before,row,9,9,menu));
        Check("unrelated menu rejected",!WorkshopPolicy.FinalDeliveryMenuReceipt(before,row,9,6,["Nothing."]));
        Check("unfinished other material rejected",!WorkshopPolicy.FinalDeliveryMenuReceipt(before with{Rows=[row,new(1,"Other",10,3,2)]},row,9,6,menu));
        Check("not last set rejected",!WorkshopPolicy.FinalDeliveryMenuReceipt(before,row with{SetsDelivered=1},9,6,menu));
        Check("final phase completion receipt",WorkshopPolicy.FinalDeliveryMenuReceipt(before with{Phase=2},row,9,6,["Complete the construction of the shark-class pressure hull."]));
    }
}
