using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class GardenReceipt415Tests
{
    [ModuleInitializer] internal static void Run(){
        var now=DateTimeOffset.UtcNow;
        var address=new Address("0000000000000001",410,641,15,15,0,false,false,"Rafflesia","Shirogane");
        var actor=new Actor("123","Gardener",410,410,"Rafflesia","Rafflesia");
        var plan=new SharedGardenPlan("house","House","Rafflesia","Shirogane",15,15,2,"",now,[],address.HouseId,PhysicalPatch:1);
        var mapping=new SharedGardenTarget(address.HouseId,"Rafflesia","Shirogane",15,15,1,7,99,1,2,3,now);
        var raw=new SyncEvent("receipt","garden.empty.unmapped",now.AddSeconds(1),actor,address,GardenTarget:new(99,1,2,3));
        var resolve=GardenTargetMap.CreateResolver([mapping]);
        bool Matches(SyncEvent e,int bed=7)=>GardenTargetMap.ActionReceipt(e,resolve,plan,bed,"garden.empty","123",now);
        if(!Matches(raw))throw new Exception("Mapped empty receipt must confirm the harvested physical bed, including reordered batches.");
        if(Matches(raw,8)||Matches(raw with{At=now.AddSeconds(-1)})||Matches(raw with{Actor=actor with{ContentId="999"}})||
            Matches(raw with{Address=address with{Plot=16}})||Matches(raw with{GardenTarget=new(99,2,2,3)}))
            throw new Exception("Wrong bed, old receipt, other gardener, estate or moved object cannot confirm harvesting.");
        Console.WriteLine("Mapped garden action receipts passed.");
    }
}
