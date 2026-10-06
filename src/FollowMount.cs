using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private int mountAttempts;
    private DateTimeOffset nextMountAttempt;
    private unsafe bool TryFollowMount(IPlayerCharacter target,DateTimeOffset now)
    {
        var leaderMounted=((Character*)target.Address)->Mount.MountId!=0;
        if(config.FollowThem.FollowDismount&&!leaderMounted&&Conditions[ConditionFlag.Mounted]){
            if(Conditions[ConditionFlag.InFlight]||Conditions[ConditionFlag.Diving]||FollowTransitionBusy())return false;
            followSession.Pause();RequestFollowMovementStop();
            if(now<nextMountAttempt||!travelStepReady)return true;
            nextMountAttempt=now.AddSeconds(3);
            var manager=ActionManager.Instance();
            if(manager!=null&&manager->GetActionStatus(ActionType.GeneralAction,23)==0){
                manager->UseAction(ActionType.GeneralAction,23);followStatus="WAITING — Dismount requested.";
            }
            return true;
        }
        if(!config.FollowThem.FollowMount||((Character*)target.Address)->Mount.MountId==0||Conditions[ConditionFlag.Mounted]){mountAttempts=0;return false;}
        if(Conditions[ConditionFlag.InCombat]||FollowTransitionBusy())return false;
        var action=ActionManager.Instance();if(action==null)return false;
        followSession.Pause();RequestFollowMovementStop();
        followStuck.Pause();
        if(mountAttempts>=3){followStatus="WAITING — Could not mount. Mount manually or wait for the selected character to dismount.";return true;}
        if(now<nextMountAttempt)return true;
        nextMountAttempt=now.AddSeconds(5);
        // Native Mount Roulette availability; no guessed mount unlock or repeated chat commands.
        if(action->GetActionStatus(ActionType.GeneralAction,9)!=0){followStatus="WAITING — Mounting is unavailable here.";return true;}
        mountAttempts++;var accepted=action->UseAction(ActionType.GeneralAction,9);
        followStatus=accepted?"WAITING — Mounting to follow your selected character.":"WAITING — The game refused mounting.";
        return true;
    }
}
