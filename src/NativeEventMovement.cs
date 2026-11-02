using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly NativeEventWalkWatch eventWalkWatch=new();
    private DateTimeOffset eventWalkStarted,eventSaucerTravelAt;
    private bool eventSaucerTravelOwned;
    private bool OptionalQuestRunning(){try{return QuestProviderRunning();}catch{return false;}}
    private void CancelEventAccountMovement(){
        StopIndependentWalk();
        if(eventAccountMoveOwned||eventSaucerTravelOwned){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}}
        eventAccountMoveOwned=eventSaucerTravelOwned=false;eventSaucerTravelAt=default;
    }
    private bool EventAccountWalk(Vector3 destination,float range,DateTimeOffset now)=>IndependentWalk(destination,range,now);
    private unsafe bool EventReachSaucer(DateTimeOffset now){
        if(Client.TerritoryType==144){if(!eventSaucerTravelOwned)eventSaucerTravelAt=default;return true;}
        eventAccountStatus="Travelling to the Gold Saucer";
        if(eventSaucerTravelAt!=default){if(now-eventSaucerTravelAt>TimeSpan.FromMinutes(2))throw new InvalidOperationException("Gold Saucer arrival was not confirmed.");return false;}
        var telepo=Telepo.Instance();if(telepo==null)return false;
        telepo->UpdateAetheryteList();
        foreach(var t in telepo->TeleportList)if(t.AetheryteId==62){
            if(!telepo->Teleport(t.AetheryteId,t.SubIndex))throw new InvalidOperationException("Gold Saucer teleport was refused.");
            eventSaucerTravelAt=now;return false;
        }
        throw new InvalidOperationException("Unlock Gold Saucer teleport on this character first.");
    }
    private bool EventReachEntrance(DateTimeOffset now){
        var self=Objects.LocalPlayer;if(self==null)return false;
        if(eventSaucerTravelOwned){
            eventAccountStatus="Using the Gold Saucer aethernet to Entrance & Card Squares";
            if(now-eventSaucerTravelAt>TimeSpan.FromSeconds(90))throw new InvalidOperationException("Entrance aethernet travel timed out.");
            if(LifestreamBusy()||now-eventSaucerTravelAt<TimeSpan.FromSeconds(3))return false;
            // Lifestream can finish before loading/position settle. Keep observing the
            // accepted request, without dispatching another teleport or failing early.
            if(Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]||Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]||Vector3.Distance(self.Position,new(-64.74408f,2.8839111f,54.33728f))>20)return false;
            eventSaucerTravelOwned=false;return true;
        }
        // Only request the aethernet while actually near the main plaza crystal.
        if(Vector3.Distance(self.Position,new(-.015319824f,3.4942627f,-.015319824f))>15)return true;
        if(LifestreamBusy())throw new InvalidOperationException("Another travel task is running.");
        if(!Pi.GetIpcSubscriber<uint,bool>("Lifestream.AethernetTeleportById").InvokeFunc(63))throw new InvalidOperationException("Entrance aethernet travel was refused.");
        eventSaucerTravelOwned=true;eventSaucerTravelAt=now;return false;
    }
}
