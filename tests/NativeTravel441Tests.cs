using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class NativeTravel441Tests
{
    [ModuleInitializer] internal static void Run(){
        if(NativeTravelPolicy.Arrival(42,43,100,100,false)||NativeTravelPolicy.Arrival(42,42,100,99,false)||NativeTravelPolicy.Arrival(42,42,100,100,true)||!NativeTravelPolicy.Arrival(42,42,100,100,false))throw new Exception("Travel completion must verify identity, destination and loading.");
        if(!NativeTravelPolicy.ReturnFirst(2,1,10,20)||NativeTravelPolicy.ReturnFirst(1,1,10,20)||NativeTravelPolicy.ReturnFirst(2,1,10,10))throw new Exception("Cross-DC routes return visiting characters home first; same-DC visits do not.");
        var prompt="Leonis Verelle Rafflesia will now be transported to Coeurl in the Crystal data center.";
        if(!NativeTravelPolicy.TransferPrompt(prompt,"Leonis Verelle","Coeurl","Crystal")||NativeTravelPolicy.TransferPrompt(prompt,"Other Character","Coeurl","Crystal")||NativeTravelPolicy.TransferPrompt(prompt,"Leonis Verelle","Sophia","Materia")||NativeTravelPolicy.TransferPrompt(prompt,"","Coeurl","Crystal"))throw new Exception("DC confirmation must name the expected character, world and DC.");
        if(!NativeTravelPolicy.WorldPrompt("Travel to Maduin?","Maduin")||NativeTravelPolicy.WorldPrompt("Travel to Golem?","Maduin")||NativeTravelPolicy.WorldPrompt("Cancel travel to Maduin?","Maduin"))throw new Exception("World travel confirmation must match the exact requested action.");
        foreach(var stage in Enum.GetValues<NativeTravelStage>())if(!NativeTravelPolicy.Progress(stage).Contains('/'))throw new Exception("Every travel stage needs indicator progress.");
        var gate=new ShoppingTravelGate();var now=DateTimeOffset.UtcNow;
        if(gate.Ready(now,true)||gate.Ready(now.AddSeconds(1),true)||!gate.Ready(now.AddSeconds(2),true))throw new Exception("Shopping must wait for stable arrival before city teleport.");
        if(!gate.MayRequest(now)||gate.MayRequest(now.AddMilliseconds(500))||!gate.MayRequest(now.AddSeconds(2)))throw new Exception("Refused city teleports must remain retryable without spamming the game.");
        gate.Ready(now.AddSeconds(3),false);
        if(gate.Ready(now.AddSeconds(4),true)||!gate.Ready(now.AddSeconds(6),true))throw new Exception("Loading resets shopping readiness.");
        Console.WriteLine("Native world travel identity, destination and return routing passed.");
    }
}
