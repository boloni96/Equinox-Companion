using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class GardenDelay419Tests
{
    [ModuleInitializer] internal static void Run(){
        var at=DateTimeOffset.Parse("2026-11-05T21:30:11Z");
        var actor=new Actor("123","Gardener",410,410,"Rafflesia","Rafflesia");
        var address=new Address("0000000000000001",410,641,15,10,0,false,false,"Rafflesia","Shirogane");
        var target=new GardenSnapshot(at,actor,address,"bed6","",null,null,false,[]);
        var tend=new TendIntent("tend",at,target,1,6);
        void Check(bool ok,string label){if(!ok)throw new Exception("Delayed garden confirmation: "+label);}
        Check(tend.Confirm(4017,at.AddSeconds(4.48),target)?.Bed==6,"delayed Bed 6 success");
        Check(tend.Confirm(4017,at.AddSeconds(19),target)?.EventId=="tend","response within runner wait");
        Check(tend.Confirm(4017,at.AddSeconds(21),target)==null,"expired response");
        Check(tend.Confirm(4017,at.AddSeconds(4.48),target with{TargetId="bed7"})==null,"wrong bed");
        Check(tend.Confirm(4017,at.AddSeconds(4.48),target with{Address=address with{Plot=11}})==null,"wrong house");
        Check(tend.Confirm(4017,at.AddSeconds(4.48),target with{Actor=actor with{ContentId="456"}})==null,"wrong actor");
        Check(tend.Confirm(4016,at.AddSeconds(4.48),target)==null,"wrong action");
        var harvest=new HarvestIntent("harvest",at,target,1,6,4810);
        var late=at.AddSeconds(12);
        Check(harvest.Confirm(751,4810,late,target)?.Bed==6,"delayed harvest item");
        Check(harvest.ConfirmChat(4810,late,target with{ObservedAt=late,TargetId=null})?.Bed==6,"delayed harvest drops target");
        Check(harvest.ConfirmChat(999,late,target with{ObservedAt=late})==null,"unrelated loot");
        Check(harvest.Confirm(751,4810,at.AddSeconds(21),target)==null,"expired harvest");
        Console.WriteLine("Delayed garden confirmation regressions passed.");
    }
}
