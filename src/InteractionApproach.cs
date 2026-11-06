using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using NativeObject=FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong independentInteractionTarget;
    private unsafe bool TargetWithinInteractionRange(IGameObject? target)
    {
        if(target==null||!target.IsTargetable||Objects.LocalPlayer is not {} self||!Player.IsLoaded||
            Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InFlight])return false;
        try{
        var events=EventFramework.Instance();if(events==null)return false;
        // Normal object interaction; no chat errors while checking each movement frame.
        if(!events->CheckInteractRange((NativeObject*)self.Address,(NativeObject*)target.Address,0,false))return false;
        return GameWalkingCollision.Available()&&independentCollision.InteractionVisible(self.Position,target.Position);
        }catch{return false;} // Keep the existing bounded approach if the native check is unavailable.
    }
    private bool StopAtInteractionTarget(IGameObject? target)
    {
        if(!TargetWithinInteractionRange(target))return false;
        var id=target!.GameObjectId;var position=Objects.LocalPlayer!.Position;
        StopIndependentWalk("Target within interaction range · movement released");
        independentArrived=true;
        RecordTravelPrompt("Interaction approach stopped",new{target=id,dataId=target.BaseId,position,targetPosition=target.Position,reason="Native interaction range and clear line of sight"});
        return true;
    }
}
