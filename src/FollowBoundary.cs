using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FollowPortalSignal? boundarySample,boundaryDeparture;
    private Vector3 boundaryPrevious,boundaryDirection;
    private DateTimeOffset boundarySampleAt,boundaryDepartureAt,nextInstanceChoice;
    private bool boundaryLoading;
    private static unsafe uint CurrentFollowInstance()=>UIState.Instance()==null?0:UIState.Instance()->PublicInstance.InstanceId;
    private unsafe bool TrySelectFollowInstance(FollowPortalSignal signal,DateTimeOffset now)
    {
        if(!config.EnableFollowThem||!config.FollowThem.UseSharedTeleports||!followSession.Armed||relayPairingKey!=config.PairingKey||signal.SentAt<followArmedAt||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,signal.Name,signal.HomeWorld)||signal.ExpiresAt<=now.ToUnixTimeMilliseconds()||signal.ArrivalInstance==0||Player.CurrentWorld.RowId!=0&&Player.CurrentWorld.RowId!=signal.CurrentWorld||(Client.TerritoryType!=0&&Client.TerritoryType!=signal.Territory&&Client.TerritoryType!=signal.ArrivalTerritory)||FollowStopTextEntryActive())return false;
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;
        var index=FollowInstancePolicy.Choice(TransportChoices(addon),signal.ArrivalInstance);
        if(index<0||addon==null||!addon->IsVisible||!addon->IsReady)return false;
        if(now<nextInstanceChoice)return true;
        if(approachOwnsMovement){try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception){return true;}approachOwnsMovement=false;}
        // A ready numbered instance menu can be shown over the loading screen.
        // Selecting it does not issue movement; loading/stationary gates must not deadlock it.
        if(travelAwaitingArrival?.Id==signal.Id){routeExecutionStarted=true;followApproach=null;}
        nextInstanceChoice=now.AddSeconds(3);
        usingSharedTravel=true;try{SelectTravelChoice(addon,index);}finally{usingSharedTravel=false;}
        RecordFollowTravel("Instance requested",new {signal.Id,instance=signal.ArrivalInstance});return true;
    }
    private unsafe void CaptureFollowInstanceChoice(AtkUnitBase* addon,int index)
    {
        if(!SharingTravel||usingSharedTravel||addon==null||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address||Objects.LocalPlayer is not {} self||outgoingTravel!=null||transportCapture!=null)return;
        var choices=TransportChoices(addon);
        if(index<0||index>=choices.Count||!choices[index].Any(c=>c is >= '\uE0B1' and <= '\uE0B9'))return;
        // Only a numbered instance list can create this instruction. The actual
        // instance is read after arrival, including when the server reroutes it.
        if(!Enumerable.Range(1,9).Any(n=>FollowInstancePolicy.Choice(choices,(uint)n)==index))return;
        var direction=boundaryDirection.LengthSquared()>.01f?Vector3.Normalize(boundaryDirection):new Vector3(MathF.Sin(self.Rotation),0,MathF.Cos(self.Rotation));
        var signal=TravelSignal("boundary",0,"",0,self.Position);
        if(signal!=null)CaptureTravel(signal with {SourceKind="boundary",Approach=FollowTravelPosition.From(self.Position+direction*3)},0);
    }
    private void UpdateFollowBoundary(DateTimeOffset now)
    {
        if(!SharingTravel||config.PairingKey.Length!=64){boundarySample=null;boundaryDeparture=null;boundaryLoading=false;return;}
        var loading=Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51];
        if(loading){
            if(!boundaryLoading&&boundarySample is {} s&&now-boundarySampleAt<TimeSpan.FromSeconds(1)&&outgoingTravel==null&&transportCapture==null&&outgoingPortal==null&&s.DutyId==0&&!SharingWorldIntent&&boundaryDirection.LengthSquared()>.01f){
                var goal=new Vector3(s.X,s.Y,s.Z)+Vector3.Normalize(boundaryDirection)*3;
                boundaryDeparture=s with {Approach=FollowTravelPosition.From(goal),SourceKind="boundary"};boundaryDepartureAt=now;
            }
            boundaryLoading=true;return;
        }
        if(!Player.IsLoaded||Objects.LocalPlayer is not {} self){boundarySample=null;boundaryDeparture=null;return;}
        if(boundaryLoading){
            boundaryLoading=false;
            if(boundaryDeparture is {} s&&now-boundaryDepartureAt<TimeSpan.FromSeconds(120)&&s.Name==Player.CharacterName&&s.HomeWorld==Player.HomeWorld.RowId&&s.CurrentWorld==Player.CurrentWorld.RowId&&s.Territory!=Client.TerritoryType){
                EnqueueOutgoingTravel(config.PairingKey,s with {SentAt=now.ToUnixTimeMilliseconds()},portalRelay.HasFollowers(config.PairingKey,s.Name,s.HomeWorld));
            }
            boundaryDeparture=null;boundarySample=null;boundaryDirection=default;
        }
        // Cheap local position observation; only a confirmed uncaptured zone
        // transition produces a relay message. No Journal changes or uploads.
        if(now-boundarySampleAt<TimeSpan.FromMilliseconds(100))return;
        if(boundarySample is {} previous&&previous.Territory==Client.TerritoryType){
            var delta=self.Position-boundaryPrevious;delta.Y=0;
            if(delta.LengthSquared() is >.0025f and <25)boundaryDirection=delta;
            else if(delta.LengthSquared()<.0025f)boundaryDirection=default;
        }
        boundaryPrevious=self.Position;boundarySampleAt=now;
        boundarySample=Conditions[ConditionFlag.InCombat]?null:TravelSignal("boundary",0,"",0,self.Position);
    }
}
