using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool independentTravelOwned;
    private DateTimeOffset independentTravelAt;
    private void CancelIndependentQuestTravel(){
        if(independentTravelOwned){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}}
        independentTravelOwned=false;
    }
    private unsafe bool IndependentQuestTravel(JsonElement step,uint territory,DateTimeOffset now)
    {
        if(independentTravelOwned){
            questToolsStatus+=" · aethernet travel";
            if(now-independentTravelAt>TimeSpan.FromSeconds(90))throw new InvalidOperationException("Aethernet arrival timed out.");
            if(LifestreamBusy()||now-independentTravelAt<TimeSpan.FromSeconds(3))return false;
            independentTravelOwned=false;
        }
        if(territory!=0&&Client.TerritoryType!=territory){
            if(independentTravelSent){if(now-independentTravelAt>TimeSpan.FromSeconds(90))throw new InvalidOperationException("Teleport arrival not confirmed.");return false;}
            var destination=independentBook!.TravelId(IndependentStepPolicy.Text(step,"AetheryteShortcut"));
            if(destination==0){StopIndependentQuest("Travel to "+DistrictName(territory)+" manually; no supported teleport in this step.",true);return false;}
            var telepo=Telepo.Instance();if(telepo==null)return false;telepo->UpdateAetheryteList();
            foreach(var entry in telepo->TeleportList)if(entry.AetheryteId==destination){
                if(!telepo->Teleport(destination,entry.SubIndex))throw new InvalidOperationException("Teleport was refused.");
                independentTravelSent=true;independentTravelAt=now;return false;
            }
            StopIndependentQuest("Required aetheryte is not unlocked.",true);return false;
        }
        if(!independentAethernetSent&&step.TryGetProperty("AethernetShortcut",out var route)&&route.ValueKind==JsonValueKind.Array&&route.GetArrayLength()==2){
            var destination=independentBook!.TravelId(route[1].GetString()??"");
            // Use existing travel integration only when standing beside a crystal;
            // local approach and all NPC walking are handled by Companion.
            var self=Objects.LocalPlayer;
            var crystal=self==null?null:Objects.FirstOrDefault(o=>o.ObjectKind==Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte&&System.Numerics.Vector3.Distance(o.Position,self.Position)<8);
            independentAethernetSent=true;
            if(destination!=0&&crystal!=null){
                if(LifestreamBusy())throw new InvalidOperationException("Another travel operation is running.");
                if(!Pi.GetIpcSubscriber<uint,bool>("Lifestream.AethernetTeleportById").InvokeFunc(destination))throw new InvalidOperationException("Aethernet request was refused.");
                independentTravelOwned=true;independentTravelAt=now;return false;
            }
        }
        return true;
    }
}
