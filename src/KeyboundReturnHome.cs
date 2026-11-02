using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong eventReturnCharacter,eventReturnEstate;
    private uint eventReturnWorld;
    private DateTimeOffset eventReturnStarted,eventReturnActionAt;
    private bool eventReturnWorldRequested,eventReturnTeleportSent;
    private void BeginEventReturnHome(DateTimeOffset now){
        StopKeybound();eventAccountFarmStarted=false;CancelEventAccountMovement();
        eventReturnCharacter=Player.ContentId;eventReturnWorld=Player.HomeWorld.RowId;
        eventReturnEstate=0;eventReturnWorldRequested=eventReturnTeleportSent=false;
        eventReturnStarted=now;eventReturnActionAt=default;
        EventAccountStage("return-home","Rewards and item use confirmed · returning home before switching");
        TraceKeyboundRun(keyboundAccountRun,"return-home-started",eventAccountStatus,"account-routine");
    }
    private unsafe void UpdateEventReturnHome(DateTimeOffset now,SharedCharacter character){
        if(now-eventReturnStarted>TimeSpan.FromMinutes(30))throw new InvalidOperationException("Return home timed out; the character was not switched.");
        if(!Player.IsLoaded)return;
        if(Player.ContentId!=eventReturnCharacter)throw new InvalidOperationException("Character changed during return home.");
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){return;}
        if(FollowMovementKeysHeld())throw new InvalidOperationException("Return home stopped by your movement.");
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty])throw new InvalidOperationException("Return home blocked by combat or duty.");
        if(LifestreamBusy())return;
        if(Player.CurrentWorld.RowId!=eventReturnWorld){
            eventAccountStatus="Returning to home world · "+Player.HomeWorld.Value.Name.ToString();
            if(eventReturnWorldRequested){if(now-eventReturnActionAt>TimeSpan.FromSeconds(10))throw new InvalidOperationException("Home-world travel ended before arrival; character kept online.");return;}
            if(!Pi.GetIpcSubscriber<uint,bool>("Lifestream.ChangeWorldById").InvokeFunc(eventReturnWorld))throw new InvalidOperationException("Home-world travel could not start; character kept online.");
            eventReturnWorldRequested=true;eventReturnActionAt=now;return;
        }
        if(eventReturnWorldRequested&&now-eventReturnActionAt<TimeSpan.FromSeconds(5))return;
        if(eventReturnTeleportSent){
            var manager=HousingManager.Instance();
            var address=manager==null?null:AddressOf(manager->GetCurrentHouseId());
            var observed=manager==null?null:AddressOf(manager->GetCurrentIndoorHouseId());
            var destination=AddressOf(new HouseId{Id=eventReturnEstate});
            var exact=destination!=null&&new[]{address,observed}.Any(a=>a!=null&&a.WorldId==destination.WorldId&&a.TerritoryTypeId==destination.TerritoryTypeId&&a.Ward==destination.Ward&&a.Plot==destination.Plot);
            var arrived=exact;
            if(!arrived||Conditions[ConditionFlag.Casting]||now-eventReturnActionAt<TimeSpan.FromSeconds(3)){
                eventAccountStatus="Returning to estate · waiting for confirmed arrival";
                if(now-eventReturnActionAt>TimeSpan.FromMinutes(2))throw new InvalidOperationException("Estate arrival was not confirmed; character kept online.");return;
            }
            PrintEquinox("Event routine · "+character.Name+": rewards used and home estate reached.");
            TraceKeyboundRun(keyboundAccountRun,"return-home-confirmed",eventAccountStatus,"account-routine");
            eventAccountCompleted++;eventAccountIndex++;EventAccountStage("character","Preparing next selected character");return;
        }
        var telepo=Telepo.Instance();if(telepo==null)return;
        if(telepo->ActiveTeleportRequest||Conditions[ConditionFlag.Casting]){eventAccountStatus="Waiting for the current teleport";return;}
        telepo->UpdateAetheryteList();
        foreach(var estateType in new[]{EstateType.PersonalEstate,EstateType.FreeCompanyEstate}){
            var house=HousingManager.GetOwnedHouseId(estateType);
            if(house.Id==0||house.Id==ulong.MaxValue)continue;
            foreach(var entry in telepo->TeleportList){
                if(entry.HouseId.Id!=house.Id||entry.AetheryteId==0)continue;
                if(!telepo->Teleport(entry.AetheryteId,entry.SubIndex))throw new InvalidOperationException("The game refused the estate teleport; character kept online.");
                eventReturnEstate=house.Id;
                eventReturnTeleportSent=true;eventReturnActionAt=now;
                eventAccountStatus=estateType==EstateType.PersonalEstate?"Returning to private house":"Returning to FC house";return;
            }
        }
        throw new InvalidOperationException("Rewards complete and home world reached, but no private or FC house teleport is available. Character kept online.");
    }
}
