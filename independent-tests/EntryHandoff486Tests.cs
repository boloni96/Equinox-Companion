using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class EntryHandoff486Tests
{
    [ModuleInitializer] internal static void Run(){
        var entry=new OutdoorPlotEntry(Vector3.UnitZ);entry.Next(Vector3.Zero);
        if(entry.Confirm(new(0,0,.8f),true))throw new Exception("Early estate identity must not end entry at the street edge.");
        if(!entry.FinishingEntry)throw new Exception("Once identified, finish this segment without extending it.");
        if(entry.Confirm(new(0,0,3),false))throw new Exception("Position alone must not confirm the estate.");
        if(entry.Confirm(new(0,3,3),true))throw new Exception("Another floor must not confirm entry.");
        if(!entry.Confirm(new(0,0,2.8f),true))throw new Exception("Finished segment plus matching estate should hand off to gardening.");
        if(!entry.Confirm(new(0,0,3.5f),true))throw new Exception("Normal frame overshoot must not trigger another segment or turn back.");
        if(entry.Confirm(new(1,0,3),true))throw new Exception("Sideways movement must not count as clearing the entry segment.");
        var fresh=new OutdoorPlotEntry(Vector3.UnitX);
        if(fresh.Confirm(Vector3.Zero,true)||fresh.Goal!=new Vector3(3,0,0))throw new Exception("Starting at the identity boundary still requires an entry segment.");
        if(!AethernetTravelPolicy.PreferWalking(Vector3.Zero,new(40,0,0),new(-20,0,0),new(45,0,0)))throw new Exception("Do not walk away toward a shard for a nearby plot.");
        if(AethernetTravelPolicy.PreferWalking(Vector3.Zero,new(200,0,0),new(2,0,0),new(198,0,0)))throw new Exception("Keep shard travel when it substantially reduces walking.");
        Console.WriteLine("486 premature estate identity, entry completion, floor and identity checks passed.");
    }
}
