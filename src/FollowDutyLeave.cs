using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FollowPortalSignal? dutySource,pendingDutyLeave;
    private string dutyLeaveKey="";
    private long dutyLeaveSession;
    private DateTimeOffset dutyLeaveAccepted,dutyLootClearAt;
    private unsafe bool DutyLootPending()
    {
        var loot=Loot.Instance();if(loot==null)return true;
        foreach(var item in loot->Items)if(item.ItemId!=0&&item.ChestObjectId is not (0 or 0xE0000000)&&item.RollResult==RollResult.UnAwarded)return true;
        return false;
    }
    private unsafe FollowPortalSignal NormalizeDutyDeparture(FollowPortalSignal signal)
    {
        var game=GameMain.Instance();
        return signal.DutyId>0&&Player.IsLoaded&&game!=null&&game->CurrentContentFinderConditionId==0&&Client.TerritoryType!=signal.Territory
            ?signal with {TravelKind="leaveDuty",HandlerType=0,Approach=null}:signal;
    }
    private unsafe void QueueDutyLeave(FollowPortalSignal s,DateTimeOffset now)
    {
        var game=GameMain.Instance();
        if(!config.FollowThem.LeaveDuties||!followSession.Armed||!Player.IsLoaded||game==null||game->CurrentContentFinderConditionId!=s.DutyId||s.DutyId==0||s.CurrentWorld!=Player.CurrentWorld.RowId||s.Territory!=Client.TerritoryType||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,s.Name,s.HomeWorld)||s.EntityId!=lastLeaderEntity||s.SentAt<followArmedAt||s.SentAt<now.ToUnixTimeMilliseconds()-120000||s.ExpiresAt<=now.ToUnixTimeMilliseconds())return;
        pendingDutyLeave=s;dutyLeaveKey=config.PairingKey;dutyLeaveSession=followArmedAt;dutyLeaveAccepted=now;dutyLootClearAt=default;lastPortalSignalId=s.Id;
        PauseFollowForTravel();TravelDiagnostic("Leader left the duty. Waiting for loot to finish before leaving.");
    }
    private unsafe void UpdateFollowDutyLeave(DateTimeOffset now)
    {
        var game=GameMain.Instance();
        if(SharingTravel&&Player.IsLoaded&&Objects.LocalPlayer is {} self&&game!=null){
            var current=game->CurrentContentFinderConditionId;
            if(dutySource is {} source&&current==0&&source.Territory!=Client.TerritoryType&&source.Name==Player.CharacterName&&source.HomeWorld==Player.HomeWorld.RowId&&portalSendTask==null&&config.PairingKey.Length==64){
                portalSendTask=SendPortalToAudience(config.PairingKey,source with {SentAt=now.ToUnixTimeMilliseconds()},portalRelay.HasFollowers(config.PairingKey,source.Name,source.HomeWorld));
            }
            var observed=current==0?null:TravelSignal("leaveDuty",0,"",0,self.Position);
            dutySource=observed==null?null:observed with {DutyId=(uint)current,Approach=null};
        }else if(!SharingTravel)dutySource=null;
        if(pendingDutyLeave is not {} pending)return;
        if(!config.EnableFollowThem||!config.FollowThem.LeaveDuties||!followSession.Armed||followArmedAt!=dutyLeaveSession||config.PairingKey!=dutyLeaveKey||!Player.IsLoaded||game==null||game->CurrentContentFinderConditionId!=pending.DutyId||Client.TerritoryType!=pending.Territory||Player.CurrentWorld.RowId!=pending.CurrentWorld||now-dutyLeaveAccepted>TimeSpan.FromMinutes(10)){pendingDutyLeave=null;return;}
        var unresolved=DutyLootPending();
        if(unresolved){dutyLootClearAt=default;TravelDiagnostic("Waiting for loot rolls and awards to finish; staying in the duty.");return;}
        if(dutyLootClearAt==default)dutyLootClearAt=now;
        if(now-dutyLootClearAt<TimeSpan.FromSeconds(5)||!travelStepReady||Conditions[ConditionFlag.InCombat])return;
        if(FollowTransitionBusy()||!EventFramework.CanLeaveCurrentContent())return;
        pendingDutyLeave=null;
        EventFramework.LeaveCurrentContent(false);
        TravelDiagnostic("Requested duty exit after loot finished.");
    }
}
