using System.Text.Json;
using EquinoxCompanion;

static class GardenProjectionTests
{
    public static void Run(Action<string, string?, string?> check)
    {
        var at = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var actor = new Actor("100", "Garden Test", 410, 410, "Rafflesia");
        var address = new Address("game-house", 410, 641, 5, 10, 0, false, false, "Rafflesia", "Shirogane");
        var beds = Enumerable.Range(1,8).Select(i => new SharedGardenBed(i,"","","actual","Curiel Root","",at,at,5,false)).ToArray();
        var first = new SharedGardenPlan("house", "Home", "Rafflesia", "Shirogane", 5, 10, 1, "", at, beds, address.HouseId);
        var second = first with { Batch = 2 };
        var other = first with { HouseId = "other", World = "Seraph" };
        SharedGardenPlan Project(SharedGardenPlan p, SyncEvent[] events) => GardenLive.Apply(p, events, x=>x, _=>5);
        var cache = new GardenProjectionCache();
        var events = new List<SyncEvent>();
        cache.SetActions(events);
        var original = cache.Get(first,Project);var sibling=cache.Get(second,Project);var outsider=cache.Get(other,Project);
        for(var i=0;i<500;i++){cache.Get(first,Project);cache.Get(second,Project);cache.Get(other,Project);}
        check("garden unchanged 1500 draws reuse three projections",cache.ProjectionCount.ToString(),"3");
        foreach(var kind in new[]{"garden.tended","garden.fertilized","garden.planted","garden.ready","garden.empty","garden.status","garden.dead"})
        {
            var before = cache.ProjectionCount;
            var e = new SyncEvent("test-"+kind,kind,at.AddMinutes(events.Count+1),actor,address,1,1,
                Plant:kind=="garden.planted"?new PlantDetails(1,"Curiel Root",2,"Potting Soil"):null,
                Crop:kind=="garden.status"?new CropDetails("Curiel Root",false,"garden-menu-and-system-message",Status:"growing"):null);
            events.Add(e);cache.SetActions(events);
            var got=cache.Get(first with {Beds=[..first.Beds]},Project);
            check(kind+" cache matches full replay",JsonSerializer.Serialize(got),JsonSerializer.Serialize(Project(first,events.ToArray())));
            check(kind+" sibling batch unchanged",ReferenceEquals(sibling,cache.Get(second,Project)).ToString(),"True");
            check(kind+" other world unchanged",ReferenceEquals(outsider,cache.Get(other,Project)).ToString(),"True");
            check(kind+" recalculates one batch",(cache.ProjectionCount-before).ToString(),"1");
        }
        var changed=first with {Beds=first.Beds.Select(b=>b with {ActualSoil="New soil"}).ToArray()};
        check("changed remote beds with same plan date refresh",cache.Get(changed,Project).Beds[1].ActualSoil,"New soil");
        var renamed=changed with {HouseName="Renamed"};check("remote name refresh",cache.Get(renamed,Project).HouseName,"Renamed");
        var definition=new GardenPlanDefinition(new("Curiel Root","grow",Choices:new(){{"seed","Curiel Root"}}),[new(1,1,"Curiel Root","Potting Soil",Results:["Curiel Root"])]);
        var defined=first with {Definition=definition};var definedResult=cache.Get(defined,Project);
        var decoded=JsonSerializer.Deserialize<SharedGardenPlan>(JsonSerializer.Serialize(defined))!;
        cache.SetActions(events.Select(e=>JsonSerializer.Deserialize<SyncEvent>(JsonSerializer.Serialize(e))!));
        check("equal newly downloaded plan reuses projection",ReferenceEquals(definedResult,cache.Get(decoded,Project)).ToString(),"True");
        var editedDefinition=defined with {Definition=definition with {Draft=definition.Draft with {Target="Mirror Apple"}}};
        check("changed plan definition invalidates projection",ReferenceEquals(definedResult,cache.Get(editedDefinition,Project)).ToString(),"False");
        var reset=GardenPlanEditing.WithDefinition(first,null,at.AddHours(2));
        check("reset plan matches full replay",JsonSerializer.Serialize(cache.Get(reset,Project)),JsonSerializer.Serialize(Project(reset,events.ToArray())));
        cache.SetActions([]);check("removed local history invalidates projection",JsonSerializer.Serialize(cache.Get(first,Project)),JsonSerializer.Serialize(Project(first,[])));
        var remapped=events[0] with {Patch=2};cache.SetActions([remapped]);
        check("new mapping updates destination batch",cache.Get(second,Project).Beds[0].Watered?.ToString("O"),remapped.At.ToString("O"));
        check("new mapping removes old batch application",cache.Get(first,Project).Beds[0].Watered?.ToString("O"),at.ToString("O"));
        cache.SetActions([remapped with {Address=address with {HouseId="different-house"}}]);
        check("same address wrong game house excluded",cache.ActionsFor(second).Length.ToString(),"0");
        cache.SetActions([remapped]);check("physical patch controls matching",cache.Get(first with {PhysicalPatch=2},Project).Beds[0].Watered?.ToString("O"),remapped.At.ToString("O"));
        cache.Retain([first]);var beforeRetain=cache.ProjectionCount;cache.Get(second,Project);
        check("removed batch cache discarded",(cache.ProjectionCount-beforeRetain).ToString(),"1");
        var stamp=GardenListStamp.Of(events);events.RemoveAt(0);check("pruning invalidates list stamp",(stamp!=GardenListStamp.Of(events)).ToString(),"True");
        check("replacing same length list invalidates stamp",(GardenListStamp.Of(events)!=GardenListStamp.Of(events.ToList())).ToString(),"True");

        // Representative large history: all results must equal the previous replay.
        var plans=Enumerable.Range(1,30).Select(i=>first with {HouseId="h"+i,Plot=i,GameHouseId="g"+i}).ToArray();
        var history=Enumerable.Range(0,6000).Select(i=>new SyncEvent("history"+i,"garden.tended",at.AddSeconds(i+1),actor,address with {Plot=i%30+1,HouseId="g"+(i%30+1)},1,i%8+1)).ToArray();
        var bulk=new GardenProjectionCache();bulk.SetActions(history);
        foreach(var p in plans)check("large history equality "+p.HouseId,JsonSerializer.Serialize(bulk.Get(p,Project)),JsonSerializer.Serialize(Project(p,history)));
        var beforeChange=bulk.ProjectionCount;
        bulk.SetActions([..history,new("one-more","garden.tended",at.AddHours(3),actor,address with {Plot=1,HouseId="g1"},1,1)]);
        foreach(var p in plans)bulk.Get(p,Project);
        check("6000 actions 30 batches one change one replay",(bulk.ProjectionCount-beforeChange).ToString(),"1");
        for(var frame=0;frame<100;frame++)foreach(var p in plans)bulk.Get(p,Project);
        check("3000 idle batch draws zero additional replays",(bulk.ProjectionCount-beforeChange).ToString(),"1");
    }
}
