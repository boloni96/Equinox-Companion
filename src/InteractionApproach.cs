using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong independentInteractionTarget;
    private float InteractionLimit(IGameObject target)
    {
        var crystal=target.ObjectKind==ObjectKind.Aetheryte;
        var main=crystal&&DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().GetRowOrDefault(target.BaseId)?.IsAetheryte==true;
        var attempt=crystal?(nativeWorldTrip?.ApproachAttempt??nativeAethernetTrip?.ApproachAttempt??0):
            estateTrip?.Door==target.GameObjectId?estateTrip.DoorAttempt:0;
        return InteractionApproachPolicy.Limit(target.BaseId,crystal,main,attempt);
    }
    private bool TargetWithinInteractionRange(IGameObject? target)
    {
        if(target==null||!target.IsTargetable||Objects.LocalPlayer is not {} self||!Player.IsLoaded||
            Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InFlight])return false;
        try{
        if(!InteractionApproachPolicy.WithinDistance(self.Position,target.Position,InteractionLimit(target)))return false;
        return GameWalkingCollision.Available()&&independentCollision.InteractionVisible(self.Position,target.Position);
        }catch{return false;} // Keep the existing bounded approach if collision checks are unavailable.
    }
    private bool StopAtInteractionTarget(IGameObject? target)
    {
        if(!TargetWithinInteractionRange(target))return false;
        var id=target!.GameObjectId;var position=Objects.LocalPlayer!.Position;
        StopIndependentWalk("Target within interaction range · movement released");
        independentArrived=true;
        RecordTravelPrompt("Interaction approach stopped",new{target=id,dataId=target.BaseId,distance=System.Numerics.Vector3.Distance(position,target.Position),limit=InteractionLimit(target),position=FollowTravelPosition.From(position),targetPosition=FollowTravelPosition.From(target.Position),reason="Target-specific physical distance and clear line of sight"});
        return true;
    }
}
