using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Performance435Tests
{
    [ModuleInitializer]internal static void Run()
    {
        var at=DateTimeOffset.UtcNow;var actor=new Actor("42","Test",0,0);
        var rows=new[]{new SyncEvent("s","storage.observed",at,actor,null,Storage:new("bag:0","Bags",[100,200])),
            new SyncEvent("f","storage.observed",at,actor,null,Storage:new("fc:1","FC",[300])),
            new SyncEvent("o","storage.observed",at,actor with {ContentId="43"},null,Storage:new("bag:0","Bags",[400]))};
        if(!KeyboundStorageHistory.Read(rows,"42",new HashSet<uint>{100,300,400}).SetEquals(new uint[]{100}))throw new Exception("Reward history must exclude other characters, shared storage and unrelated items.");
        var dir=Path.Combine(Path.GetTempPath(),"equinox-notice-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try{
            File.WriteAllText(Path.Combine(dir,"timings.json"),"[]");File.WriteAllText(Path.Combine(dir,"crops.json"),"[]");
            var character=new SharedCharacter("c","Test","Rafflesia","","","Main",[]);
            var care=new SharedGardenCare("h","game","House","Rafflesia","Mist",1,1,1,["c"],[new(1,false,false,at.AddHours(-13),at.AddHours(-1),null)]);
            var roster=new SharedRoster(1,at,[new("p","Person",[character])],GardenCare:[care]);
            (string Text,ushort Color)[] Build(SyncEvent[] history,TendingRecord[] tends,bool enabled=true)=>GardenNoticeBuilder.Build(roster,[],tends,history,tends.Select(t=>new SyncEvent(t.EventId,"garden.tended",t.ConfirmedAt,t.Actor,t.Address,t.Patch,t.Bed)).ToArray(),[],new(),dir,at,enabled,true);
            if(Build(rows,[]).Length!=1)throw new Exception("Due garden must notify with unrelated inventory history present.");
            if(Build(rows,[],false).Length!=0)throw new Exception("Tending notification option must be respected.");
            var address=new Address("game",0,0,1,1,0,false,false);
            if(Build([new("empty","garden.empty",at,actor,address,1,1)],[]).Length!=0)throw new Exception("Confirmed empty bed must not notify.");
            if(Build(rows,[new("tend",at,at,actor,address,1,1)]).Length!=0)throw new Exception("Recent local care must override stale roster due time.");
        }finally{Directory.Delete(dir,true);}
        Console.WriteLine("PASS435 background garden summaries, local care, empty beds, notification flags and character-scoped reward history");
    }
}
