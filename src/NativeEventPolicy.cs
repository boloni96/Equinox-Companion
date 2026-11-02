using System.Numerics;
namespace EquinoxCompanion;

internal enum NativeEventStep { Accept, EnterSquare, MeetMaster, Complete, Wait, Unsupported, Finished }
internal static class NativeEventPolicy
{
    internal const uint Quest=5466, FullQuest=65536+Quest;
    internal static NativeEventStep Step(bool complete,bool accepted,byte sequence)=>complete?NativeEventStep.Finished:
        !accepted?NativeEventStep.Accept:sequence switch {0=>NativeEventStep.Wait,1=>NativeEventStep.EnterSquare,2=>NativeEventStep.MeetMaster,255=>NativeEventStep.Complete,_=>NativeEventStep.Unsupported};
    internal static bool EntranceShortcut(NativeEventStep step,bool walking)=>step==NativeEventStep.Accept&&!walking;
    internal static bool Scene(uint eventId,uint source,uint intended)=>eventId==FullQuest&&source==intended&&source is 1058756 or 1058754 or 1058752;
    internal static bool NearbyLeg(Vector3 start,Vector3 end)=>float.IsFinite(start.X)&&float.IsFinite(start.Y)&&float.IsFinite(start.Z)&&
        float.IsFinite(end.X)&&float.IsFinite(end.Y)&&float.IsFinite(end.Z)&&Vector3.Distance(start,end)<=80&&Math.Abs(start.Y-end.Y)<=6;
    internal static string Text(string value)=>string.Concat(value.Where(c=>!char.IsWhiteSpace(c)&&!char.IsControl(c)));
    internal static bool EntranceConfirmation(NativeEventStep step,uint npc,uint territory,bool owned,string place,string prompt)=>
        step==NativeEventStep.EnterSquare&&npc==1058754&&territory==144&&owned&&place.Length>0&&Prompt("Proceed to "+place+"?",prompt);
    internal static bool KeyboardStep(NativeEventStep step,uint npc,uint territory)=>
        step is NativeEventStep.MeetMaster or NativeEventStep.Complete && npc==1058752 && territory==1360;
    internal static bool KeyboardConfirmation(string expected,string actual)=>Prompt(expected,actual)||Prompt("A keyboard is required to participate in Keybound Brawler. The Gamepad and On-screen Keyboard cannot be used, so please ensure that you have a keyboard connected before registering for a game.",actual);
    internal static bool Prompt(string expected,string actual)=>expected.Length>0&&Text(expected)==Text(actual);
}

// This watches progress towards a destination, so walking against a wall or
// circling without getting closer cannot keep a step alive indefinitely.
internal sealed class NativeEventWalkWatch
{
    private float best;
    private DateTimeOffset started,progress;
    internal void Start(float distance,DateTimeOffset now){best=distance;started=progress=now;}
    internal string Check(float distance,DateTimeOffset now){
        if(distance<best-.3f){best=distance;progress=now;}
        if(now-started>TimeSpan.FromSeconds(90))return "Walking exceeded 90 seconds without arrival.";
        return now-progress>TimeSpan.FromSeconds(8)?"Walking made no progress for 8 seconds; the route may be blocked.":"";
    }
}
