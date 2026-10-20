using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class AdventureGarden218Tests
{
    [ModuleInitializer] public static void Run()
    {
        void Check(bool value,string why){if(!value)throw new Exception("AdventureGarden218: "+why);}
        var now=DateTimeOffset.Parse("2026-10-20T19:00:00Z");
        var job=new AdventureTask("plant","Plan & plant","Gardening",false,false,Rule:new("planplant","h",2));
        var visit=new AdventureTask("visit","Enter house","Housing",false,false,Rule:new("visit","other"));
        var stop=new AdventureStop("World","","",0,[job,visit],"Siren");
        var other=stop with {Tasks=[job with {Rule=new("planplant","other",1)}]};
        var board=new AdventureBoard("route","generation","2026-10-20","multi","Garden",[stop,other],0,1);
        var navigation=new AdventureGardenNavigation();navigation.Set(board);
        Check(navigation.HasGardens&&navigation.Contains("h"),"collapsed multi garden participates before drawing headers");
        Check(!navigation.Contains("other"),"visit-only house and another stop cannot activate planting markers");
        Check(AdventureGardenNavigation.TaskTarget(job,stop)==("h",2,0),"batch-level plan action opens batch two without inventing a bed");
        Check(AdventureGardenNavigation.TaskTarget(job with {Rule=new("care","h",2,8)},stop)==("h",2,8),"bed-level tasks preserve exact bed");
        Check(AdventureGardenNavigation.TaskTarget(visit,stop) is null,"house visit labels keep normal copy behaviour");
        Check(!AdventureGardenNavigation.UseStandaloneBatch(false,true),"adventure alone never reads stale standalone batch");
        Check(!AdventureGardenNavigation.UseStandaloneBatch(true,true),"both guides use numbered menu within current adventure garden");
        Check(AdventureGardenNavigation.UseStandaloneBatch(true,false),"standalone guide retains wrong-batch protection");
        Check(!AdventureGardenNavigation.HasGarden(board with {Index=2}),"finished route has no active planting scope");

        var actor=new Actor("12345","Example Gardener",57,57,"Siren","Siren");
        var address=new Address("0000000000000001",57,339,1,1,0,false,false,"Siren","Mist");
        var target=new GardenSnapshot(now,actor,address,"0000000000000002","",null,null,false,[],new(2003757,2,"EventObj",10,0,20,7));
        var mapping=new SharedGardenTarget(address.HouseId,"Siren","Mist",1,1,1,8,7,10,0,20,now);
        var plan=new SharedGardenPlan("h","Garden","Siren","Mist",1,1,2,"",now,[],address.HouseId,PhysicalPatch:1);
        Check(navigation.Follow(target,address,true,false,[mapping],[plan],now)==("h",2,8),"physical patch one reveals display batch two using existing mapping");
        navigation.Set(board with {Revision=2,Stops=[stop with {Tasks=[job with {Done=true},visit]},other]});
        Check(navigation.Follow(target,address,true,false,[mapping],[plan],now.AddSeconds(1)) is null,"sync and automatic completion do not repeatedly reopen or scroll");
        navigation.Set(board with {Index=1});
        Check(navigation.Follow(target,address,true,false,[mapping],[plan],now) is null,"selected garden outside current stop remains untouched");
        navigation.Set(board);
        Check(navigation.Follow(target,address,true,true,[mapping],[plan],now) is null,"bed calibration never steers its own navigation");
        Check(navigation.Follow(target,address,true,false,[],[plan],now) is null,"unknown physical mapping cannot choose a batch");
        Check(navigation.Follow(target,address,true,false,[mapping],[plan],now.AddMilliseconds(300))==("h",2,8),"mapping received later reveals selected batch");
        navigation.Set(null);Check(!navigation.HasGardens,"closing adventure clears its marker scope");
        navigation.Set(board);
        Check(navigation.Follow(target,address,true,false,[mapping],[plan],now.AddSeconds(3)) is null,"stale location cannot reveal garden");
        Check(navigation.Follow(target,address with {Plot=2},true,false,[mapping],[plan],now) is null,"another plot cannot reveal garden");
        Console.WriteLine("PASS 218 adventure garden scope, batch links, physical mapping, late mapping, calibration and no repeated scrolling");
    }
}
