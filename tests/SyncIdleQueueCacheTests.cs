using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class SyncIdleQueueCacheTests
{
    [ModuleInitializer]internal static void Run()
    {
        var now=DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        var stamp=new SyncIdleQueueStamp(1,10,20,30,30000,29000,23,"pair-a","scope-a",true,true,true,true,true,true);
        var cache=new SyncIdleQueueCache();
        void Check(bool good,string label){if(!good)throw new Exception("Idle sync queue: "+label);}
        Check(!cache.Unchanged(stamp,now),"first pass must evaluate");cache.Remember(stamp,now);
        Check(cache.Unchanged(stamp,now.AddSeconds(1)),"unchanged fast garden poll reuses empty result");
        foreach(var changed in new[]{stamp with {Revision=2},stamp with {Houses=11},stamp with {Tending=21},stamp with {Planting=31},stamp with {Discoveries=30001},stamp with {Sent=29001},stamp with {Protocol=24},stamp with {PairingKey="pair-b"},stamp with {RegistrationScope="scope-b"},stamp with {TrackGardens=false},stamp with {CharacterDetails=false},stamp with {Collections=false},stamp with {AutoRetainer=false},stamp with {Activities=false},stamp with {HouseDetails=false}})
            Check(!cache.Unchanged(changed,now.AddSeconds(1)),"mutation invalidates");
        Check(!cache.Unchanged(stamp,now.AddSeconds(30)),"time validation bounded");
        Check(!cache.Unchanged(stamp,now.AddSeconds(-1)),"clock change rechecks");
        cache.Invalidate();Check(!cache.Unchanged(stamp,now.AddSeconds(1)),"observation wake always rechecks");
    }
}
