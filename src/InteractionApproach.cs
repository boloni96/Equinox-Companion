using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong independentInteractionTarget;
    private bool TargetWithinInteractionRange(IGameObject? target)
    {
        if(target==null||!target.IsTargetable||Objects.LocalPlayer is not {} self||!Player.IsLoaded||
            Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InFlight])return false;
        try{
        if(!InteractionApproachPolicy.WithinDistance(self.Position,target.Position,target.ObjectKind==ObjectKind.Aetheryte))return false;
        return GameWalkingCollision.Available()&&independentCollision.InteractionVisible(self.Position,target.Position);
        }catch{return false;} // Keep the existing bounded approach if collision checks are unavailable.
    }
    private bool StopAtInteractionTarget(IGameObject? target)
    {
        if(!TargetWithinInteractionRange(target))return false;
        var id=target!.GameObjectId;var position=Objects.LocalPlayer!.Position;
        StopIndependentWalk("Target within interaction range · movement released");
        independentArrived=true;
        RecordTravelPrompt("Interaction approach stopped",new{target=id,dataId=target.BaseId,position,targetPosition=target.Position,reason="Conservative physical distance and clear line of sight"});
        return true;
    }
}
