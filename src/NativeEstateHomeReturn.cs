using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed class EstateHomeReturn
    {
        public required ulong Character;
        public required ushort HomeWorld;
        public required (int Property,int Mode,string Label) Destination;
        public readonly GardenArrivalGate Arrival=new();
        public DateTimeOffset Next,ReadinessDeadline;
    }
    private EstateHomeReturn? estateHomeReturn;

    private void BeginEstateHomeReturn((int Property,int Mode,string Label) destination)
    {
        var run=new EstateHomeReturn{Character=Player.ContentId,HomeWorld=(ushort)Player.HomeWorld.RowId,Destination=destination};
        if(!StartNativeWorldTravel(run.HomeWorld,"estate") || nativeWorldTrip==null)throw new InvalidOperationException(nativeTravelStatus);
        estateHomeReturn=run;
        estateTravelStatus=$"Returning to {WorldName(run.HomeWorld)} for {destination.Label}";
        RecordTravelPrompt("Estate home return requested",new{run.Character,run.HomeWorld,source=Player.CurrentWorld.RowId,destination.Label,destination.Mode,destination.Property});
        FollowChatNotice("TRAVEL — "+estateTravelStatus+".");
    }

    private unsafe void UpdateEstateHomeReturn(DateTimeOffset now)
    {
        if(estateHomeReturn is not {} run||now<run.Next)return;
        run.Next=now.AddMilliseconds(500);
        if(nativeWorldTrip!=null){estateTravelStatus=nativeTravelStatus+" · "+run.Destination.Label;return;}
        if(run.ReadinessDeadline==default)run.ReadinessDeadline=now.AddMinutes(1);
        if(now>=run.ReadinessDeadline){StopEstateTravel("Home return ended before the character and estate teleport became ready for "+run.Destination.Label+".");return;}
        if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){run.Arrival.Reset();return;}
        if(!EstateTravelCommand.CanResume(run.Character,Player.ContentId,run.HomeWorld,Player.CurrentWorld.RowId)){
            StopEstateTravel("Home return ended before arrival on the expected character. "+nativeTravelStatus);return;
        }
        estateTravelStatus="Home world reached · waiting for "+run.Destination.Label;
        if(Objects.LocalPlayer is not {} self || !run.Arrival.Ready(Conditions[ConditionFlag.Casting],self.Position,now) ||
            HelperInCutscene() || QuestConversationVisible() || VisibleFollowAddon("SelectYesno"))return;
        var owned=EstateDestinationHouse(run.Destination.Property);
        var address=AddressOf(owned);
        if(address==null || address.WorldId!=run.HomeWorld)return;
        var manager=HousingManager.Instance();
        if(manager==null)return;
        var alreadyOutside=manager->GetCurrentHousingTerritoryType()==HousingTerritoryType.Outdoor && SameEstate(AddressOf(manager->GetCurrentHouseId()),address);
        if(!alreadyOutside && !SameEstate(ReadEstateInterior(manager),address)){
            var telepo=Telepo.Instance();
            if(telepo==null || telepo->ActiveTeleportRequest)return;
            telepo->UpdateAetheryteList();
            var ready=false;
            foreach(var entry in telepo->TeleportList)if(entry.HouseId.Id==owned.Id && entry.AetheryteId!=0){ready=true;break;}
            if(!ready){run.Next=now.AddSeconds(2);return;}
        }
        estateHomeReturn=null;
        RecordTravelPrompt("Continuing estate command after home arrival",new{run.Character,run.HomeWorld,run.Destination.Label,run.Destination.Mode});
        StartEstateTravelCommand(run.Destination);
    }
    private unsafe bool SelectEstateLoginWorld(ushort world)
    {
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("_CharaSelectWorldServer").Address;
        var module=RaptureAtkModule.Instance();
        if(addon==null||!addon->IsReady||module==null||module->AtkArrayDataHolder.StringArrayCount<=1||module->AtkArrayDataHolder.StringArrays==null)return false;
        var strings=module->AtkArrayDataHolder.StringArrays[1];
        if(strings==null||strings->StringArray==null)return false;
        var expected=WorldName(world);if(string.IsNullOrWhiteSpace(expected))return false;
        for(var i=0;i<Math.Min(strings->Size,16);i++){
            var p=strings->StringArray[i].Value;if(p==null)break;
            if(TravelMenuText(p)==expected){
                var previous=usingSharedTravel;
                usingSharedTravel=true;
                try{LoginCallback(addon,25,0,i);}finally{usingSharedTravel=previous;}
                return true;
            }
        }
        return false;
    }
}
