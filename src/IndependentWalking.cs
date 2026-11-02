using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private CollisionRouteSearch? independentSearch;
    private readonly GameWalkingCollision independentCollision=new();
    private Hook<FollowInputRead>? independentForwardHook;
    private Vector3 independentGoal,independentPrevious,independentLastProgress;
    private Vector3[]? independentPath;
    private int independentWaypoint,independentReplans;
    private ulong independentCharacter;
    private uint independentTerritory;
    private float independentRange;
    private bool independentMoving,independentArrived;
    private DateTimeOffset independentPulseUntil,independentSince,independentProgressAt,independentCollisionAt;
    private int independentCheckedWaypoint=-1,independentInputReads,independentInputOverrides;
    private string independentNavigationRun="";
    private readonly WalkingInputLease independentLease=new();
    private readonly int[] independentInputCodes=new int[256];
    private int independentForwardReads,independentExpiredReads,independentInactiveReads;
    private bool independentPositionChanged;
    private string independentInputBlock="",independentLastWalkFailure="";
    private string independentWalkStatus="Idle",independentWalkFailure="";
    private unsafe byte IndependentForward(nint input,int id)
    {
        var original=independentForwardHook!.Original(input,id);
        // Movement is consumed by the game control input manager, not UIInputData.
        if(input!=(nint)InputManager.Instance())return original;
        independentInputReads++;
        if((uint)id<(uint)independentInputCodes.Length)independentInputCodes[id]++;
        if(id!=(int)InputCode.MOVE_FORE)return original;
        independentForwardReads++;
        // UI/condition checks run on Framework.Update, never inside a native input
        // callback (ImGui may have no context there). The short lease fails closed.
        var decision=independentLease.Check(DateTimeOffset.UtcNow);
        if(decision==WalkingInputDecision.Expired){independentExpiredReads++;independentInputBlock="Movement pulse expired before the game read it";}
        else if(decision==WalkingInputDecision.Inactive){independentInactiveReads++;}
        else{independentInputOverrides++;independentInputBlock="";return 1;}
        return original;
    }
    private void StopIndependentWalk(string reason="Stopped",bool resetFailure=false)
    {
        if(independentSearch!=null||independentPath!=null)TraceKeyboundRun(independentNavigationRun,"navigation-stopped",reason,"companion-navigation",false);
        independentMoving=false;independentPulseUntil=default;independentLease.Revoke();independentForwardHook?.Disable();
        independentSearch=null;independentPath=null;independentArrived=false;independentWalkStatus=reason;independentWalkFailure="";
        if(resetFailure)independentLastWalkFailure="";
    }
    private bool IndependentWalk(Vector3 destination,float range,DateTimeOffset now)
    {
        if(Objects.LocalPlayer is not {} self)return false;
        if(followStopPending||followStopUnconfirmed||nativeFollowRequested)throw new InvalidOperationException("Waiting for game follow to stop before navigating.");
        if(!GameWalkingCollision.Available()){independentWalkStatus="Waiting for collision geometry to load";return false;}
        if(IndependentOtherTravelBusy())throw new InvalidOperationException("Another travel task is running; local walking was not started.");
        if(independentWalkFailure.Length>0&&Vector3.DistanceSquared(independentGoal,destination)<1)throw new InvalidOperationException(independentWalkFailure);
        if(independentArrived&&Vector3.DistanceSquared(independentGoal,destination)<1&&CollisionRouteSearch.Horizontal(self.Position,destination)<=range&&MathF.Abs(self.Position.Y-destination.Y)<1.2f)return true;
        if(independentSearch!=null||independentPath!=null){
            if(Vector3.DistanceSquared(independentGoal,destination)<1&&independentTerritory==Client.TerritoryType)return false;
            StopIndependentWalk("Destination changed");
        }
        independentNavigationRun=Guid.NewGuid().ToString("N");independentInputReads=independentInputOverrides=0;
        independentForwardReads=independentExpiredReads=independentInactiveReads=0;Array.Clear(independentInputCodes);
        independentInputBlock=independentLastWalkFailure="";independentPositionChanged=false;independentLease.Revoke();
        TraceKeyboundRun(independentNavigationRun,"navigation-started",$"Destination {destination}; range {range}",independentQuestId!=0?"quest:"+independentQuestRun:"event",false);
        independentGoal=destination;independentRange=range;independentCharacter=Player.ContentId;independentTerritory=Client.TerritoryType;
        independentWalkFailure="";independentArrived=false;independentReplans=0;independentSince=independentProgressAt=now;
        independentPrevious=independentLastProgress=self.Position;
        independentSearch=new(independentCollision,self.Position,destination,range);
        independentWalkStatus="Checking collision geometry and finding a route";return false;
    }
    private unsafe void UpdateIndependentWalk(DateTimeOffset now)
    {
        if(independentSearch==null&&independentPath==null)return;
        try{
            independentPulseUntil=default;independentLease.Revoke();independentMoving=false;
            if(IndependentOtherTravelBusy())throw new InvalidOperationException("Another travel task took control; local walking stopped.");
            if(!Player.IsLoaded||Player.ContentId!=independentCharacter||Client.TerritoryType!=independentTerritory)throw new InvalidOperationException("Navigation stopped: character or territory changed.");
            if(FollowMovementKeysHeld())throw new InvalidOperationException("Navigation stopped by your movement.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Mounted]||Conditions[ConditionFlag.InFlight])throw new InvalidOperationException("Navigation stopped for loading, combat, duty or mounted movement. Dismount for ground walking.");
            if(now-independentSince>TimeSpan.FromSeconds(90))throw new InvalidOperationException("Navigation timed out after 90 seconds.");
            if(!GameWalkingCollision.Available()){independentWalkStatus="Waiting for collision geometry to load";return;}
            if(IndependentMovementWindow()||FollowStopTextEntryActive()||Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78]||Conditions[ConditionFlag.Casting]){independentWalkStatus="Paused · close the conversation or wait for the cast";independentInputBlock=independentWalkStatus;independentProgressAt=now;return;}
            var self=Objects.LocalPlayer;if(self==null)return;
            if(independentSearch!=null){
                independentMoving=false;independentSearch.Advance();
                independentWalkStatus=$"Finding walkable route · {independentSearch.Expanded} areas checked";
                if(independentSearch.Error.Length>0)throw new InvalidOperationException(independentSearch.Error);
                if(independentSearch.Path==null)return;
                TraceKeyboundRun(independentNavigationRun,"route-found",$"{independentSearch.Path.Length} points; {independentSearch.Expanded} nodes checked","companion-navigation",false);
                independentPath=independentSearch.Path;independentSearch=null;independentWaypoint=1;independentCheckedWaypoint=-1;
                independentLastProgress=self.Position;independentProgressAt=DateTimeOffset.UtcNow;
            }
            if(CollisionRouteSearch.Horizontal(self.Position,independentGoal)<=independentRange&&MathF.Abs(self.Position.Y-independentGoal.Y)<=1.2f){
                StopIndependentWalk("Arrived · movement released");independentArrived=true;return;
            }
            while(independentPath!=null&&independentWaypoint<independentPath.Length&&Vector3.Distance(self.Position,independentPath[independentWaypoint])<.3f)independentWaypoint++;
            if(independentPath==null||independentWaypoint>=independentPath.Length)throw new InvalidOperationException("Route ended without confirmed arrival.");
            var target=independentPath[independentWaypoint];
            if(Vector3.Distance(self.Position,independentLastProgress)>.3f){independentProgressAt=now;independentLastProgress=self.Position;independentPositionChanged=true;}
            var checkCollision=now>=independentCollisionAt||independentCheckedWaypoint!=independentWaypoint;
            if(checkCollision){independentCollisionAt=now.AddMilliseconds(150);independentCheckedWaypoint=independentWaypoint;}
            if(now-independentProgressAt>TimeSpan.FromSeconds(4)||checkCollision&&!independentCollision.Traverse(self.Position,target)){
                independentMoving=false;independentForwardHook?.Disable();
                if(independentInputOverrides==0&&now-independentProgressAt>TimeSpan.FromSeconds(4))throw new InvalidOperationException($"Movement not delivered · forward reads {independentForwardReads}, expired {independentExpiredReads}, inactive {independentInactiveReads} (all controls {independentInputReads}). {independentInputBlock}");
                if(++independentReplans>2)throw new InvalidOperationException("Movement blocked after two route checks; stopped.");
                independentSearch=new(independentCollision,self.Position,independentGoal,independentRange);independentPath=null;independentProgressAt=now;
                TraceKeyboundRun(independentNavigationRun,"route-replan",$"Attempt {independentReplans}; input reads {independentInputReads}, overrides {independentInputOverrides}","companion-navigation",false);
                independentWalkStatus="Path changed or blocked · finding another route";return;
            }
            var cameras=CameraManager.Instance();
            var camera=cameras==null?null:(FFXIVClientStructs.FFXIV.Client.Game.Camera*)cameras->GetActiveCamera();
            if(camera==null)throw new InvalidOperationException("Movement camera unavailable.");
            var delta=target-self.Position;var yaw=MathF.Atan2(delta.X,delta.Z);
            ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)self.Address)->SetRotation(yaw);
            camera->DirH=yaw-MathF.PI;
            independentForwardHook??=Interop.HookFromAddress<FollowInputRead>(InputManager.MemberFunctionPointers.GetInputStatus,IndependentForward);
            // Start the lease AFTER collision work and hook setup, not at frame start.
            independentMoving=true;independentLease.Arm(DateTimeOffset.UtcNow);independentPulseUntil=independentLease.Until;
            if(!independentForwardHook.IsEnabled)independentForwardHook.Enable();
            independentWalkStatus=independentPositionChanged?$"Walking · point {independentWaypoint}/{independentPath.Length-1}":"Ready to move · waiting for movement confirmation";independentPrevious=self.Position;
        }catch(Exception e){StopIndependentWalk(e.Message);independentWalkFailure=independentLastWalkFailure=e.Message;Log.Warning(e,"Independent navigation stopped");}
    }
    private bool IndependentMovementWindow()=>QuestConversationVisible()||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("SystemMenu");
    private bool IndependentOtherTravelBusy(){try{return LifestreamBusy();}catch{return false;}}
    private void DisposeIndependentWalk(){StopIndependentWalk("Plugin unloaded");independentForwardHook?.Dispose();independentForwardHook=null;}
}
