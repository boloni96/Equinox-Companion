using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe bool RequestNativePublicTeleport(uint id){
        if(!Player.IsLoaded||nativeWorldTrip!=null)return false;
        var telepo=Telepo.Instance();if(telepo==null||telepo->ActiveTeleportRequest)return false;
        telepo->UpdateAetheryteList();
        foreach(var entry in telepo->TeleportList)if(entry.AetheryteId==id&&entry.SubIndex==0&&entry.Ward==0&&entry.Plot==0){
            usingSharedTravel=true;try{var accepted=telepo->Teleport(id,0);RecordTravelPrompt("Native area teleport",new{id,accepted});return accepted;}finally{usingSharedTravel=false;}
        }return false;
    }
}
