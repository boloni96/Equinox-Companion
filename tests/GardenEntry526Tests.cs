using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class GardenEntry526Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var now=DateTimeOffset.UtcNow;
        var address=new Address("0000000000000001",410,641,15,15,0,false,false,"Rafflesia","Shirogane");
        var actor=new Actor("123","Gardener",410,410,"Rafflesia","Rafflesia");
        var plan=new SharedGardenPlan("house","House","Rafflesia","Shirogane",15,15,2,"",now,[],address.HouseId,PhysicalPatch:1);
        var receipt=new SyncEvent("receipt","garden.tended",now.AddSeconds(1),actor,address,Patch:1,Bed:1);
        bool Valid(SyncEvent e)=>GardenTargetMap.ActionReceipt(e,x=>x,plan,1,"garden.tended","123",now);
        if(!Valid(receipt)||Valid(receipt with{Bed=2})||Valid(receipt with{At=now.AddSeconds(-1)})||Valid(receipt with{Kind="garden.status"})||Valid(receipt with{Actor=actor with{ContentId="999"}}))
            throw new Exception("Only fresh successful tending of this exact bed by this character may satisfy an early action");
        var landing=new Vector3(10,0,10);
        var entry=new OutdoorPlotEntry(Vector3.UnitZ,landing);
        if(!entry.GeometryRoute||entry.Goal!=landing)throw new Exception("Garden plot entry lost its geometry destination");
        if(entry.Confirm(landing,false))throw new Exception("Coordinates alone must not confirm the estate");
        if(entry.Confirm(new Vector3(10,0,6),true))throw new Exception("Street-edge identity must not finish plot entry immediately");
        if(!entry.Confirm(landing,true))throw new Exception("Verified destination plot should finish entry");
    }
}
