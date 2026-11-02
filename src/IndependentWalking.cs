using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Conditions;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private CollisionRouteSearch? independentSearch;
    private readonly GameWalkingCollision independentCollision=new();
    private unsafe delegate void GroundMovementRead(nint controller,float* left,float* forward,float* turn,byte* strafe,byte* flags,byte additive);
    private delegate byte GroundMovementEnabled(nint controller);
    private Hook<GroundMovementRead>? independentForwardHook;
    private GroundMovementEnabled? independentGroundEnabled1,independentGroundEnabled2;
    private Vector3 independentGoal,independentPrevious,independentLastProgress;
    private Vector3[]? independentPath;
    private int independentWaypoint,independentReplans;
    private ulong independentCharacter;
    private uint independentTerritory;
    private float independentRange;
    private Vector2 independentAxes;
    private int independentLastRoutePoints,independentLastExpanded;
    private double independentPlanningSeconds;
    private readonly NavigationPhaseTimer independentPhaseTimer=new();
    private readonly List<Vector3> independentBlockedTargets=[];
    private CollisionRouteSearch CreateIndependentSearch(Vector3 start,Vector3 goal,float range)=>
        new(new AvoidStalledTargets(new GameWalkingCollision(cacheGround:true),independentBlockedTargets.ToArray()),start,goal,range);
    private bool independentMoving,independentArrived;
    private DateTimeOffset independentPulseUntil,independentSince,independentPlanningSince,independentProgressAt,independentCollisionAt;
    private int independentCheckedWaypoint=-1,independentInputReads,independentInputOverrides;
    private string independentNavigationRun="";
    private readonly WalkingInputLease independentLease=new();
    private int independentNativeGateRejects,independentNativeManualReads,independentNativeExceptions;
    private int independentForwardReads,independentExpiredReads,independentInactiveReads;
    private bool independentPositionChanged;
    private string independentInputBlock="",independentLastWalkFailure="";
    private string independentWalkStatus="Idle",independentWalkFailure="";
    private unsafe void IndependentGroundInput(nint controller,float* left,float* forward,float* turn,byte* strafe,byte* flags,byte additive)
    {
        independentForwardHook!.Original(controller,left,forward,turn,strafe,flags,additive);
        independentInputReads++;
        if(controller==0||left==null||forward==null||turn==null||additive!=0)return;
        independentForwardReads++;
        var decision=independentLease.Check(DateTimeOffset.UtcNow);
        if(decision==WalkingInputDecision.Expired){independentExpiredReads++;independentInputBlock="Movement pulse expired";return;}
        if(decision==WalkingInputDecision.Inactive){independentInactiveReads++;return;}
        try{
            // Respect the game's native movement gates and any real/other-plugin input.
            var enabled=independentGroundEnabled1!(controller)!=0&&independentGroundEnabled2!(controller)!=0;
            if(!enabled){independentNativeGateRejects++;independentInputBlock="Game movement is temporarily disabled";return;}
            if(!WalkingInputLease.CanSupplyGround(*left,*forward,*turn)){
                independentNativeManualReads++;independentInputBlock="Another movement input has priority";
                independentLease.Revoke();return;
            }
            // Apply direction relative to the current control mode; never rotate the camera.
            *left=independentAxes.X;*forward=independentAxes.Y;independentInputOverrides++;independentInputBlock="";
        }catch(Exception e){independentNativeExceptions++;independentInputBlock=e.GetType().Name;independentLease.Revoke();}
    }
    private unsafe void EnsureIndependentGroundInput()
    {
        if(independentForwardHook!=null)return;
        // Native ABI/signature references: awgil/ffxiv_navmesh, Movement/OverrideMovement.cs.
        // Independently implemented ground-only adapter; no plugin dependency.
        if(!QuickLootScanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 75 10 38 43 3C",out var gate1)||
           !QuickLootScanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 75 03 88 47 3F",out var gate2))
            throw new InvalidOperationException("Game ground-movement checks are unavailable for this version.");
        independentGroundEnabled1=Marshal.GetDelegateForFunctionPointer<GroundMovementEnabled>(gate1);
        independentGroundEnabled2=Marshal.GetDelegateForFunctionPointer<GroundMovementEnabled>(gate2);
        independentForwardHook=Interop.HookFromSignature<GroundMovementRead>("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D",IndependentGroundInput);
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
        independentForwardReads=independentExpiredReads=independentInactiveReads=0;independentNativeGateRejects=independentNativeManualReads=independentNativeExceptions=0;
        independentInputBlock=independentLastWalkFailure="";independentPositionChanged=false;independentLastRoutePoints=independentLastExpanded=0;independentPlanningSeconds=0;independentLease.Revoke();
        TraceKeyboundRun(independentNavigationRun,"navigation-started",$"Destination {destination}; range {range}",independentQuestId!=0?"quest:"+independentQuestRun:"event",false);
        independentBlockedTargets.Clear();independentPhaseTimer.Begin(now);
        independentGoal=destination;independentRange=range;independentCharacter=Player.ContentId;independentTerritory=Client.TerritoryType;
        independentWalkFailure="";independentArrived=false;independentReplans=0;independentSince=independentPlanningSince=independentProgressAt=now;
        independentPrevious=independentLastProgress=self.Position;
        independentSearch=CreateIndependentSearch(self.Position,destination,range);
        independentWalkStatus="Checking collision geometry and finding a route";return false;
    }
    private unsafe void UpdateIndependentWalk(DateTimeOffset now)
    {
        if(independentSearch==null&&independentPath==null)return;
        try{
            independentPulseUntil=default;independentLease.Revoke();independentMoving=false;
            if(independentNativeManualReads>0)throw new InvalidOperationException("Navigation stopped: another movement input took priority.");
            if(independentNativeExceptions>0)throw new InvalidOperationException("Ground movement callback failed: "+independentInputBlock);
            if(IndependentOtherTravelBusy())throw new InvalidOperationException("Another travel task took control; local walking stopped.");
            if(!Player.IsLoaded||Player.ContentId!=independentCharacter||Client.TerritoryType!=independentTerritory)throw new InvalidOperationException("Navigation stopped: character or territory changed.");
            if(FollowMovementKeysHeld())throw new InvalidOperationException("Navigation stopped by your movement.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Mounted]||Conditions[ConditionFlag.InFlight])throw new InvalidOperationException("Navigation stopped for loading, combat, duty or mounted movement. Dismount for ground walking.");
            if(now-independentSince>TimeSpan.FromMinutes(5))throw new InvalidOperationException("Navigation exceeded its five-minute total limit.");
            if(independentPhaseTimer.Expired(now))throw new InvalidOperationException(independentSearch!=null?"Route calculation timed out after 90 seconds.":"Walking timed out after 90 seconds.");
            if(!GameWalkingCollision.Available()){independentWalkStatus="Waiting for collision geometry to load";return;}
            if(IndependentMovementWindow()||FollowStopTextEntryActive()||Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78]||Conditions[ConditionFlag.Casting]){independentWalkStatus="Paused · close the conversation or wait for the cast";independentInputBlock=independentWalkStatus;independentProgressAt=now;return;}
            var self=Objects.LocalPlayer;if(self==null)return;
            if(independentSearch!=null){
                independentMoving=false;independentSearch.Advance(milliseconds:3,edgeBudget:96);
                independentLastExpanded=independentSearch.Expanded;independentPlanningSeconds=(DateTimeOffset.UtcNow-independentPlanningSince).TotalSeconds;
                independentWalkStatus=$"Finding walkable route · {independentSearch.Expanded} areas checked";
                if(independentSearch.Error.Length>0)throw new InvalidOperationException(independentSearch.Error);
                if(independentSearch.Path==null)return;
                TraceKeyboundRun(independentNavigationRun,"route-found",$"{independentSearch.Path.Length} points; {independentSearch.Expanded} nodes checked","companion-navigation",false);
                independentPhaseTimer.SetPlanning(false,DateTimeOffset.UtcNow);
                independentPath=independentSearch.Path;independentLastRoutePoints=independentPath.Length;independentSearch=null;independentWaypoint=1;independentCheckedWaypoint=-1;
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
            if(checkCollision){
                independentWaypoint=WalkingSteering.LookAhead(self.Position,independentPath,independentWaypoint,independentCollision.Traverse);
                target=independentPath[independentWaypoint];
                independentCollisionAt=now.AddMilliseconds(150);independentCheckedWaypoint=independentWaypoint;
            }
            var stalled=now-independentProgressAt>TimeSpan.FromSeconds(independentInputOverrides>0?1.5:4);
            if(stalled||checkCollision&&!independentCollision.Traverse(self.Position,target)){
                independentMoving=false;independentForwardHook?.Disable();
                if(independentInputOverrides==0&&now-independentProgressAt>TimeSpan.FromSeconds(4))throw new InvalidOperationException($"Movement not delivered · ground reads {independentForwardReads}, game blocked {independentNativeGateRejects}, other input {independentNativeManualReads}, expired {independentExpiredReads}. {independentInputBlock}");
                if(++independentReplans>2)throw new InvalidOperationException("Movement blocked after two route checks; stopped.");
                var blockedAt=stalled?Vector3.Lerp(self.Position,target,MathF.Min(1,.6f/MathF.Max(.01f,Vector3.Distance(self.Position,target)))):independentCollision.LastBlockedAt??target;
                independentBlockedTargets.Add(blockedAt);independentPhaseTimer.SetPlanning(true,now);independentPlanningSince=now;
                independentSearch=CreateIndependentSearch(self.Position,independentGoal,independentRange);independentPath=null;independentProgressAt=now;
                TraceKeyboundRun(independentNavigationRun,"route-replan",$"Attempt {independentReplans}; avoiding obstacle {blockedAt}; target {target}; position {self.Position}; stalled {stalled}; input reads {independentInputReads}, overrides {independentInputOverrides}","companion-navigation",false);
                independentWalkStatus="Path changed or blocked · finding another route";return;
            }
            var cameras=CameraManager.Instance();
            var camera=cameras==null?null:(FFXIVClientStructs.FFXIV.Client.Game.Camera*)cameras->GetActiveCamera();
            if(camera==null)throw new InvalidOperationException("Movement camera unavailable.");
            var legacy=MarketGameConfig.UiControl.TryGetUInt("MoveMode",out var mode)&&mode==1;
            independentAxes=WalkingSteering.Axes(self.Position,target,legacy?camera->DirH+MathF.PI:self.Rotation);
            EnsureIndependentGroundInput();
            // Start the lease AFTER collision work and hook setup, not at frame start.
            if(!independentForwardHook!.IsEnabled)independentForwardHook.Enable();
            independentMoving=true;independentLease.Arm(DateTimeOffset.UtcNow);independentPulseUntil=independentLease.Until;
            independentWalkStatus=independentPositionChanged?$"Walking · point {independentWaypoint}/{independentPath.Length-1}":"Ready to move · waiting for movement confirmation";independentPrevious=self.Position;
        }catch(Exception e){StopIndependentWalk(e.Message);independentWalkFailure=independentLastWalkFailure=e.Message;Log.Warning(e,"Independent navigation stopped");}
    }
    private bool IndependentMovementWindow()=>QuestConversationVisible()||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("SystemMenu");
    private bool IndependentOtherTravelBusy(){try{return LifestreamBusy();}catch{return false;}}
    private void DisposeIndependentWalk(){StopIndependentWalk("Plugin unloaded");independentForwardHook?.Dispose();independentForwardHook=null;}
}
