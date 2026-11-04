using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    // Native flight-input ABI documented by awgil/ffxiv_navmesh Movement/OverrideMovement.cs.
    // Adapter and navigation are implemented here; no vnavmesh plugin is required.
    [StructLayout(LayoutKind.Explicit,Size=0x18)]
    private struct NativeFlightInput {
        [FieldOffset(0)]public float Forward;[FieldOffset(4)]public float Left;
        [FieldOffset(8)]public float Up;[FieldOffset(12)]public float Turn;
    }
    private unsafe delegate void FlightInputRead(nint controller,NativeFlightInput* input);
    private Hook<FlightInputRead>? independentFlightHook;
    private readonly WalkingInputLease flightLease=new();
    private readonly GameFlightCollision flightCollision=new();
    private readonly WalkingRecoveryBudget flightRecovery=new();
    private FlightRouteSearch? flightSearch;
    private Vector3[]? flightPath;private int flightWaypoint;
    private Vector3 flightGoal,flightLanding,flightTarget,flightLastProgress;
    private float flightRange;private ulong flightCharacter;private uint flightTerritory;
    private DateTimeOffset flightStarted,flightActionAt,flightProgressAt;
    private string flightPhase="";private bool flightManual,flightFailed;
    private int flightActions;private float? flightHeading;
    private DateTimeOffset flightHeadingAt;
    private bool FlightActive=>flightPhase.Length>0;
    private unsafe void IndependentFlightInput(nint controller,NativeFlightInput* input){
        independentFlightHook!.Original(controller,input);
        if(input==null||controller==0||flightLease.Check(DateTimeOffset.UtcNow)!=WalkingInputDecision.Allowed)return;
        if(!FlightNavigationPolicy.CanSupply(input->Forward,input->Left,input->Up,input->Turn)){flightManual=true;flightLease.Revoke();return;}
        try{
            if(!Conditions[ConditionFlag.InFlight]||Objects.LocalPlayer is not {} player||flightHeading is not {} heading)return;
            var cameras=CameraManager.Instance();var camera=cameras==null?null:(FFXIVClientStructs.FFXIV.Client.Game.Camera*)cameras->GetActiveCamera();if(camera==null)return;
            var legacy=MarketGameConfig.UiControl.TryGetUInt("MoveMode",out var mode)&&mode==1;
            var reference=legacy?camera->DirH+MathF.PI:player.Rotation;
            var delta=flightTarget-player.Position;var horizontal=new Vector2(delta.X,delta.Z).Length();
            var speed=Math.Clamp(delta.Length()/3,.15f,1);
            input->Left=MathF.Sin(heading-reference)*speed;input->Forward=MathF.Cos(heading-reference)*speed;
            input->Up=MathF.Atan2(delta.Y,horizontal);
        }catch{flightFailed=true;flightLease.Revoke();}
    }
    private void StopIndependentFlight(){
        flightLease.Revoke();independentFlightHook?.Disable();flightPhase="";flightSearch=null;flightPath=null;flightHeading=null;flightManual=flightFailed=false;
    }
    private unsafe bool StartIndependentFlight(Vector3 goal,float range,DateTimeOffset now){
        var self=Objects.LocalPlayer;if(self==null)return false;
        if(!independentCollision.Ground(goal,out var ground))return false;
        // Flight needs a supported landing and a clear final descent. Indoor targets
        // remain ground routes; do not take off toward an unverified roof/interior.
        if(!flightCollision.Clear(ground+Vector3.UnitY*4,ground))return false;
        if(!Conditions[ConditionFlag.InFlight]&&!flightCollision.Clear(self.Position,self.Position+Vector3.UnitY*4))return false;
        StopIndependentWalk("Preparing flight");
        independentGoal=flightGoal=goal;flightLanding=ground;flightRange=range;flightCharacter=Player.ContentId;flightTerritory=Client.TerritoryType;
        independentCharacter=flightCharacter;independentTerritory=flightTerritory;independentArrived=false;independentLastWalkFailure=independentWalkFailure="";
        flightStarted=flightProgressAt=now;flightActionAt=default;flightActions=0;flightRecovery.Reset();flightLastProgress=self.Position;
        flightPhase=Conditions[ConditionFlag.InFlight]?"plan":"mount";independentWalkStatus="Preparing flight · checking mount and take-off";return true;
    }
    private unsafe void TickIndependentFlight(DateTimeOffset now){
        flightLease.Revoke();independentMoving=false;
        try{
            if(!Player.IsLoaded||Player.ContentId!=flightCharacter||Client.TerritoryType!=flightTerritory)throw new InvalidOperationException("Flight stopped: character or territory changed.");
            if(now-flightStarted>TimeSpan.FromMinutes(10))throw new InvalidOperationException("Flight timed out.");
            if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.Diving])throw new InvalidOperationException("Flight stopped for combat, duty, loading or diving.");
            if(IndependentOtherTravelBusy())throw new InvalidOperationException("Another travel operation started during flight.");
            if(flightFailed)throw new InvalidOperationException("Flight input callback failed; input released.");
            if(QuestManualMovementHeld(now)||flightManual){
                flightManual=false;flightProgressAt=now;flightPath=null;flightSearch=null;flightHeading=null;
                if(Conditions[ConditionFlag.InFlight])flightPhase="plan";
                independentWalkStatus="Flight paused for your movement · resumes when released";return;
            }
            if(HelperInCutscene()||IndependentMovementWindow()||FollowStopTextEntryActive()||Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]||Conditions[ConditionFlag.Casting]){flightProgressAt=now;independentWalkStatus="Flight waiting for the game";return;}
            var self=Objects.LocalPlayer;if(self==null)return;
            var action=ActionManager.Instance();if(action==null)return;
            if(flightPhase=="mount"){
                if(Conditions[ConditionFlag.Mounted]){flightPhase="takeoff";flightActions=0;flightActionAt=default;return;}
                independentWalkStatus="Mounting for flight";
                if(now<flightActionAt)return;if(flightActions++>=3)throw new InvalidOperationException("Mounting was not confirmed.");
                if(action->GetActionStatus(ActionType.GeneralAction,9)!=0)throw new InvalidOperationException("Mounting is unavailable here.");
                action->UseAction(ActionType.GeneralAction,9);flightActionAt=now.AddSeconds(5);return;
            }
            if(flightPhase=="takeoff"){
                if(Conditions[ConditionFlag.InFlight]){flightPhase="plan";flightActionAt=default;return;}
                if(!Control.CanFly)throw new InvalidOperationException("Flight is not unlocked or allowed here.");
                independentWalkStatus="Taking off · waiting for flight confirmation";
                if(now<flightActionAt)return;if(flightActions++>=3)throw new InvalidOperationException("Take-off was not confirmed.");
                action->UseAction(ActionType.GeneralAction,2);flightActionAt=now.AddSeconds(1);return;
            }
            if(flightPhase=="dismount"){
                if(Conditions[ConditionFlag.InFlight])throw new InvalidOperationException("Landing is not confirmed; dismount was not requested.");
                if(!Conditions[ConditionFlag.Mounted]){var goal=flightGoal;StopIndependentFlight();independentWalkStatus="Landed · continuing on foot";return;}
                if(now<flightActionAt)return;if(flightActions++>=3)throw new InvalidOperationException("Dismounting was not confirmed.");
                if(action->GetActionStatus(ActionType.GeneralAction,23)==0)action->UseAction(ActionType.GeneralAction,23);flightActionAt=now.AddSeconds(2);return;
            }
            if(!Conditions[ConditionFlag.InFlight]){
                if(flightPhase=="land"&&FlightNavigationPolicy.Arrived(self.Position,flightLanding,1.5f)){flightPhase="dismount";flightActionAt=default;flightActions=0;return;}
                throw new InvalidOperationException("Flight ended before the landing point was reached.");
            }
            if(flightPhase=="plan"){
                flightSearch??=new(flightCollision,self.Position,flightLanding+Vector3.UnitY*4);
                flightSearch.Advance();independentLastExpanded=flightSearch.Expanded;independentWalkStatus=$"Calculating flight corridor · {flightSearch.Expanded} areas checked";
                if(flightSearch.Error.Length>0)throw new InvalidOperationException(flightSearch.Error);
                if(flightSearch.Path==null)return;flightPath=flightSearch.Path;flightWaypoint=1;flightSearch=null;flightPhase="cruise";flightLastProgress=self.Position;flightProgressAt=now;
            }
            if(flightPhase=="cruise"){
                while(flightPath!=null&&flightWaypoint<flightPath.Length&&Vector3.Distance(self.Position,flightPath[flightWaypoint])<1)flightWaypoint++;
                if(flightPath==null||flightWaypoint>=flightPath.Length){flightPhase="land";flightProgressAt=now;return;}
                flightTarget=flightPath[flightWaypoint];independentWalkStatus=$"Flying · point {flightWaypoint}/{flightPath.Length-1}";
            }else {flightTarget=flightLanding;independentWalkStatus="Landing · checking ground contact";}
            if(Vector3.Distance(self.Position,flightLastProgress)>.4f){flightLastProgress=self.Position;flightProgressAt=now;}
            if(!flightCollision.Clear(self.Position,flightTarget)||now-flightProgressAt>TimeSpan.FromSeconds(4)){
                if(flightPhase=="land")throw new InvalidOperationException("Landing corridor became blocked; flight input released.");
                if(!flightRecovery.Try(self.Position))throw new InvalidOperationException("Flight remained blocked after four route checks.");
                flightPath=null;flightSearch=null;flightPhase="plan";flightProgressAt=now;return;
            }
            var delta=flightTarget-self.Position;var desired=MathF.Atan2(delta.X,delta.Z);
            flightHeading=WalkingSteering.SmoothHeading(flightHeading??desired,desired,(float)(now-flightHeadingAt).TotalSeconds);flightHeadingAt=now;
            // A turn may not cut outside the verified corridor.
            var pitch=MathF.Atan2(delta.Y,new Vector2(delta.X,delta.Z).Length());
            var probe=self.Position+new Vector3(MathF.Sin(flightHeading.Value)*MathF.Cos(pitch),MathF.Sin(pitch),MathF.Cos(flightHeading.Value)*MathF.Cos(pitch))*MathF.Min(4,delta.Length());
            if(!flightCollision.Clear(self.Position,probe)){flightHeading=desired;return;}
            independentFlightHook??=Interop.HookFromSignature<FlightInputRead>("E8 ?? ?? ?? ?? 0F B6 0D ?? ?? ?? ?? B8",IndependentFlightInput);
            if(!independentFlightHook.IsEnabled)independentFlightHook.Enable();
            flightLease.Arm(DateTimeOffset.UtcNow);independentMoving=independentPositionChanged=true;
        }catch(Exception e){StopIndependentFlight();independentWalkFailure=independentLastWalkFailure=independentWalkStatus=e.Message;Log.Warning(e,"Independent flight stopped");}
    }
}
