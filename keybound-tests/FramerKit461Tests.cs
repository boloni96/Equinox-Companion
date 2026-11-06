using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class FramerKit461Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var calls=0;var loaded=true;uint seen=0;
        var reader=new FramerKitUnlockReader(item=>{calls++;return loaded&&item==51997?345u:null;});
        bool Native(uint id){seen=id;return id==345;}
        var result=reader.Read(51997,Native);
        if(result!=true||seen!=345||seen==1101)throw new Exception("Kit catalogue 1101 must be checked using Item 51997 AdditionalData 345.");
        if(!KeyboundUsePolicy.Confirmed(1,0,KeyboundUsePolicy.ResolveUnlock(2,result)))throw new Exception("Consumed kit must complete when the direct game unlock is positive, even if generic state is stale.");
        if(reader.Read(51997,_=>false)!=false||calls!=1)throw new Exception("Only metadata may be cached: changing character must reread ownership.");
        var unknownCalls=0;loaded=false;
        var delayed=new FramerKitUnlockReader(_=>loaded?345u:null);
        if(delayed.Read(51997,_=>{unknownCalls++;return true;})!=null||unknownCalls!=0)throw new Exception("Missing item metadata must not invent an unlock or query an arbitrary ID.");
        loaded=true;if(delayed.Read(51997,Native)!=true)throw new Exception("Unavailable metadata must be retried after loading.");
        if(KeyboundUsePolicy.Confirmed(1,0,KeyboundUsePolicy.ResolveUnlock(2,false)))throw new Exception("Consumption alone is not proof of unlocking.");
        Console.WriteLine("Framer kit game-ID mapping, delayed metadata, live ownership and consumed-kit confirmation passed.");
    }
}
