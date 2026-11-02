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
    private string independentWalkStatus="Idle",independentWalkFailure="";
    private unsafe byte IndependentForward(nint input,int id)
    {
        var original=independentForwardHook!.Original(input,id);
        if(id!=(int)InputId.MOVE_FORE||input!=(nint)UIInputData.Instance())return original;
        independentInputReads++;
        try{
            if(independentMoving&&DateTimeOffset.UtcNow<independentPulseUntil&&Player.IsLoaded&&Player.ContentId==independentCharacter&&Client.TerritoryType==independentTerritory&&
               !FollowMovementKeysHeld()&&!FollowStopTextEntryActive()&&!IndependentMovementWindow()&&!Conditions[ConditionFlag.OccupiedInEvent]&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]&&!Conditions[ConditionFlag.InCombat]&&!Conditions[ConditionFlag.Casting]&&!Conditions[ConditionFlag.OccupiedInQuestEvent]&&!Conditions[ConditionFlag.WatchingCutscene]&&!Conditions[ConditionFlag.WatchingCutscene78]){independentInputOverrides++;return 1;}
        }catch{/* Never interrupt the game's input callback. */}
        return original;
    }
    private void StopIndependentWalk(string reason="Stopped")
    {
        if(independentSearch!=null||independentPath!=null)TraceKeyboundRun(independentNavigationRun,"navigation-stopped",reason,"companion-navigation",false);
        independentMoving=false;independentPulseUntil=default;independentForwardHook?.Disable();
        independentSearch=null;independentPath=null;independentArrived=false;independentWalkStatus=reason;independentWalkFailure="";
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
            independentPulseUntil=default;
            if(IndependentOtherTravelBusy())throw new InvalidOperationException("Another travel task took control; local walking stopped.");
            if(!Player.IsLoaded||Player.ContentId!=independentCharacter||Client.TerritoryType!=independentTerritory)throw new InvalidOperationException("Navigation stopped: character or territory changed.");
            if(FollowMovementKeysHeld())throw new InvalidOperationException("Navigation stopped by your movement.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Mounted]||Conditions[ConditionFlag.InFlight])throw new InvalidOperationException("Navigation stopped for loading, combat, duty or mounted movement. Dismount for ground walking.");
            if(now-independentSince>TimeSpan.FromSeconds(90))throw new InvalidOperationException("Navigation timed out after 90 seconds.");
            if(!GameWalkingCollision.Available()){independentWalkStatus="Waiting for collision geometry to load";return;}
            if(IndependentMovementWindow()||FollowStopTextEntryActive()||Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78]||Conditions[ConditionFlag.Casting]){independentWalkStatus="Movement paused for a window or cast";return;}
            var self=Objects.LocalPlayer;if(self==null)return;
            if(independentSearch!=null){
                independentMoving=false;independentSearch.Advance();
                independentWalkStatus=$"Finding walkable route · {independentSearch.Expanded} areas checked";
                if(independentSearch.Error.Length>0)throw new InvalidOperationException(independentSearch.Error);
                if(independentSearch.Path==null)return;
                TraceKeyboundRun(independentNavigationRun,"route-found",$"{independentSearch.Path.Length} points; {independentSearch.Expanded} nodes checked","companion-navigation",false);
                independentPath=independentSearch.Path;independentSearch=null;independentWaypoint=1;independentCheckedWaypoint=-1;
                independentLastProgress=self.Position;independentProgressAt=now;
            }
            if(CollisionRouteSearch.Horizontal(self.Position,independentGoal)<=independentRange&&MathF.Abs(self.Position.Y-independentGoal.Y)<=1.2f){
                StopIndependentWalk("Arrived · movement released");independentArrived=true;return;
            }
            while(independentPath!=null&&independentWaypoint<independentPath.Length&&Vector3.Distance(self.Position,independentPath[independentWaypoint])<.3f)independentWaypoint++;
            if(independentPath==null||independentWaypoint>=independentPath.Length)throw new InvalidOperationException("Route ended without confirmed arrival.");
            var target=independentPath[independentWaypoint];
            if(Vector3.Distance(self.Position,independentLastProgress)>.3f){independentProgressAt=now;independentLastProgress=self.Position;}
            var checkCollision=now>=independentCollisionAt||independentCheckedWaypoint!=independentWaypoint;
            if(checkCollision){independentCollisionAt=now.AddMilliseconds(150);independentCheckedWaypoint=independentWaypoint;}
            if(now-independentProgressAt>TimeSpan.FromSeconds(4)||checkCollision&&!independentCollision.Traverse(self.Position,target)){
                independentMoving=false;independentForwardHook?.Disable();
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
            independentForwardHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdDown,IndependentForward);
            independentMoving=true;independentPulseUntil=now.AddMilliseconds(180);
            if(!independentForwardHook.IsEnabled)independentForwardHook.Enable();
            independentWalkStatus=$"Walking · point {independentWaypoint}/{independentPath.Length-1}";independentPrevious=self.Position;
        }catch(Exception e){StopIndependentWalk(e.Message);independentWalkFailure=e.Message;Log.Warning(e,"Independent navigation stopped");}
    }
    private bool IndependentMovementWindow()=>QuestConversationVisible()||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("SystemMenu");
    private bool IndependentOtherTravelBusy(){try{return LifestreamBusy();}catch{return false;}}
    private void DisposeIndependentWalk(){StopIndependentWalk("Plugin unloaded");independentForwardHook?.Dispose();independentForwardHook=null;}
}
