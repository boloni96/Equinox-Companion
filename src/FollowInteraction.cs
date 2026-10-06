using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string interactionSignal="";
    private int interactionAttempts;
    private DateTimeOffset interactionNext;
    private unsafe void RetryTravelInteraction(FollowPortalSignal signal,DateTimeOffset now)
    {
        if(interactionSignal!=signal.Id){interactionSignal=signal.Id;interactionAttempts=0;interactionNext=default;}
        if(now<interactionNext||interactionAttempts>=3||!travelStepReady||followStopPending||FollowTransitionBusy()||Objects.LocalPlayer is not {} self)return;
        foreach(var name in new[]{"SelectString","SelectYesno","Talk","TelepotTown","HousingSelectBlock","HousingSelectRoom","MansionSelectRoom"})if(VisibleFollowAddon(name))return;
        var source=Objects.FirstOrDefault(x=>x.BaseId==signal.BaseId&&x.IsTargetable&&(signal.SourceKind.Length==0||x.ObjectKind.ToString()==signal.SourceKind)&&Vector3.DistanceSquared(x.Position,new(signal.X,signal.Y,signal.Z))<1&&Vector3.Distance(self.Position,x.Position)<=x.HitboxRadius+3);
        if(source==null)return;
        interactionAttempts++;interactionNext=now.AddSeconds(2);
        relayInteracting=true;
        try{TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)source.Address,true);}
        finally{relayInteracting=false;}
        RecordFollowTravel("Interact",new {signal.Id,signal.TravelKind,source=source.Name.TextValue,attempt=interactionAttempts});
    }
    private unsafe bool AdvanceTravelTalk(FollowPortalSignal signal)
    {
        if(signal.SourceKind!="EventNpc"||Targets.Target is not {} target||target.BaseId!=signal.BaseId||Vector3.DistanceSquared(target.Position,new(signal.X,signal.Y,signal.Z))>=1)return false;
        var talk=(AtkUnitBase*)GardenGui.GetAddonByName("Talk").Address;
        if(talk==null||!talk->IsVisible||!talk->IsReady||AtkStage.Instance()==null)return false;
        var evt=new AtkEvent{Listener=(AtkEventListener*)talk,Target=&AtkStage.Instance()->AtkEventTarget,State=new(){StateFlags=(AtkEventStateFlags)132}};
        var data=new AtkEventData();
        talk->ReceiveEvent(AtkEventType.MouseDown,0,&evt,&data);
        talk->ReceiveEvent(AtkEventType.MouseClick,0,&evt,&data);
        talk->ReceiveEvent(AtkEventType.MouseUp,0,&evt,&data);
        return true;
    }
}
