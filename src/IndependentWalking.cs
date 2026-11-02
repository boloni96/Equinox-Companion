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
    private float independentSpeed=1;
    private bool independentGameGateBlocked;
    private DateTimeOffset independentGatePauseSince;
    private Vector3[]? independentPendingPath;
    private float? independentHeading;
    private DateTimeOffset independentSteeringAt;
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
            independentGameGateBlocked=!enabled;
            if(!enabled){independentNativeGateRejects++;independentInputBlock="Game movement is temporarily disabled";return;}
            if(!WalkingInputLease.CanSupplyGround(*left,*forward,*turn)){
                independentNativeManualReads++;independentInputBlock="Another movement input has priority";
                independentLease.Revoke();return;
            }
            // Apply direction relative to the current control mode; never rotate the camera.
            var player=Objects.LocalPlayer;if(player==null||independentHeading is not {} heading)return;
            var cameras=CameraManager.Instance();var camera=cameras==null?null:(FFXIVClientStructs.FFXIV.Client.Game.Camera*)cameras->GetActiveCamera();
            if(camera==null)return;
            var legacy=MarketGameConfig.UiControl.TryGetUInt("MoveMode",out var mode)&&mode==1;
            var reference=legacy?camera->DirH+MathF.PI:player.Rotation;
            independentAxes=new(MathF.Sin(heading-reference)*independentSpeed,MathF.Cos(heading-reference)*independentSpeed);
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
        independentGameGateBlocked=false;independentGatePauseSince=default;independentHeading=null;independentMoving=false;independentPulseUntil=default;independentLease.Revoke();independentForwardHook?.Disable();
        independentSearch=null;independentPath=null;independentPendingPath=null;independentArrived=false;independentWalkStatus=reason;independentWalkFailure="";
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
        var cached=RecallIndependentRoute(self.Position,destination,range);
        if(cached!=null){independentPath=cached;independentWaypoint=1;independentCheckedWaypoint=-1;independentPhaseTimer.SetPlanning(false,now);independentWalkStatus="Using a saved route · checking the next section";return false;}
        // The opening is fully checked; the remainder starts at its fixed endpoint.
        var opening=VerifiedWalkingOpening.Build(independentCollision,self.Position,destination,range);
        if(opening.Length>1){independentPath=opening;independentWaypoint=1;independentCheckedWaypoint=-1;}
        independentSearch=CreateIndependentSearch(opening.Length>1?opening[^1]:self.Position,destination,range);
        independentWalkStatus=opening.Length>1?"Opening route ready · calculating ahead":"Checking collision geometry and finding a route";return false;
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
            if(IndependentMovementWindow()||FollowStopTextEntryActive()||Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]||HelperInCutscene()||Conditions[ConditionFlag.Casting]){independentWalkStatus="Paused · close the conversation or wait for the cast";independentInputBlock=independentWalkStatus;independentProgressAt=now;return;}
            var self=Objects.LocalPlayer;if(self==null)return;
            if(independentGameGateBlocked){
                if(independentGatePauseSince==default)independentGatePauseSince=now;
                if(now-independentGatePauseSince>TimeSpan.FromSeconds(60))throw new InvalidOperationException("The game kept movement locked for 60 seconds. Finish the current scene or prompt and restart.");
                independentSpeed=0;independentProgressAt=now;
                // Recheck the native gate with zero movement until the game releases it.
                independentLease.Arm(DateTimeOffset.UtcNow);
                independentWalkStatus="Paused · game movement locked; waiting for scene or transition";return;
            }
            independentGatePauseSince=default;
            if(independentSearch!=null){
                independentMoving=false;independentSearch.Advance(milliseconds:6,edgeBudget:192);
                independentLastExpanded=independentSearch.Expanded;independentPlanningSeconds=(DateTimeOffset.UtcNow-independentPlanningSince).TotalSeconds;
                independentWalkStatus=$"Finding walkable route · {independentSearch.Expanded} areas checked";
                if(independentSearch.Error.Length>0)throw new InvalidOperationException(independentSearch.Error);
                if(independentSearch.Path==null&&independentPath==null)return;
                if(independentSearch.Path!=null){
                TraceKeyboundRun(independentNavigationRun,"route-found",$"{independentSearch.Path.Length} points; {independentSearch.Expanded} nodes checked","companion-navigation",false);
                independentPhaseTimer.SetPlanning(false,DateTimeOffset.UtcNow);
                var found=independentSearch.Path;
                if(independentPath!=null)independentPendingPath=found;
                else {independentPath=found;independentWaypoint=1;independentCheckedWaypoint=-1;}
                RememberIndependentRoute(independentPendingPath!=null?independentPath!.Concat(found.Skip(1)).ToArray():found);
                independentLastRoutePoints=found.Length;independentSearch=null;
                independentLastProgress=self.Position;independentProgressAt=DateTimeOffset.UtcNow;
                }
            }
            if(CollisionRouteSearch.Horizontal(self.Position,independentGoal)<=independentRange&&MathF.Abs(self.Position.Y-independentGoal.Y)<=1.2f){
                StopIndependentWalk("Arrived · movement released");independentArrived=true;return;
            }
            if(independentPath!=null&&CollisionRouteSearch.Horizontal(self.Position,independentPath[^1])<.3f&&MathF.Abs(self.Position.Y-independentPath[^1].Y)<.5f){
                if(independentPendingPath!=null){independentPath=independentPendingPath;independentPendingPath=null;independentWaypoint=1;independentCheckedWaypoint=-1;}
                else if(independentSearch!=null){independentProgressAt=now;independentWalkStatus="Verified section complete · calculating the next section";return;}
            }
            if(independentPath!=null)independentWaypoint=WalkingSteering.AdvanceWaypoint(self.Position,independentPath,independentWaypoint,independentCollision.ComfortableTraverse);
            if(independentPath==null||independentWaypoint>=independentPath.Length)throw new InvalidOperationException("Route ended without confirmed arrival.");
            var target=WalkingSteering.PursuitTarget(self.Position,independentPath,independentWaypoint,independentCollision.ComfortableTraverse);
            if(Vector3.Distance(self.Position,independentLastProgress)>.3f){independentProgressAt=now;independentLastProgress=self.Position;independentPositionChanged=true;}
            var checkCollision=now>=independentCollisionAt||independentCheckedWaypoint!=independentWaypoint;
            if(checkCollision){
                independentCollisionAt=now.AddMilliseconds(150);independentCheckedWaypoint=independentWaypoint;
            }
            var stalled=now-independentProgressAt>TimeSpan.FromSeconds(independentInputOverrides>0?1.5:4);
            if(stalled||checkCollision&&!independentCollision.Traverse(self.Position,target)){
                independentMoving=false;independentForwardHook?.Disable();
                if(independentInputOverrides==0&&now-independentProgressAt>TimeSpan.FromSeconds(4))throw new InvalidOperationException($"Movement not delivered · ground reads {independentForwardReads}, game blocked {independentNativeGateRejects}, other input {independentNativeManualReads}, expired {independentExpiredReads}. {independentInputBlock}");
                if(++independentReplans>2)throw new InvalidOperationException("Movement blocked after two route checks; stopped.");
                var blockedAt=stalled?Vector3.Lerp(self.Position,target,MathF.Min(1,.6f/MathF.Max(.01f,Vector3.Distance(self.Position,target)))):independentCollision.LastBlockedAt??target;
                ForgetIndependentRoute();independentPendingPath=null;independentBlockedTargets.Add(blockedAt);independentPhaseTimer.SetPlanning(true,now);independentPlanningSince=now;
                independentSearch=CreateIndependentSearch(self.Position,independentGoal,independentRange);independentPath=null;independentProgressAt=now;
                TraceKeyboundRun(independentNavigationRun,"route-replan",$"Attempt {independentReplans}; avoiding obstacle {blockedAt}; target {target}; position {self.Position}; stalled {stalled}; input reads {independentInputReads}, overrides {independentInputOverrides}","companion-navigation",false);
                independentWalkStatus="Path changed or blocked · finding another route";return;
            }
            var cameras=CameraManager.Instance();
            var camera=cameras==null?null:(FFXIVClientStructs.FFXIV.Client.Game.Camera*)cameras->GetActiveCamera();
            if(camera==null)throw new InvalidOperationException("Movement camera unavailable.");
            var legacy=MarketGameConfig.UiControl.TryGetUInt("MoveMode",out var mode)&&mode==1;
            var delta=target-self.Position;var heading=MathF.Atan2(delta.X,delta.Z);
            independentHeading??=self.Rotation;
            if(independentHeading is {} previousHeading){
                var smooth=WalkingSteering.SmoothHeading(previousHeading,heading,(float)(now-independentSteeringAt).TotalSeconds);
                if(MathF.Abs(MathF.IEEERemainder(smooth-heading,2*MathF.PI))>.01f){
                    var probe=self.Position+new Vector3(MathF.Sin(smooth),0,MathF.Cos(smooth))*.6f;
                    if(independentCollision.Ground(probe,out var floor)&&independentCollision.ComfortableTraverse(self.Position,floor))heading=smooth;
                    else {independentHeading=smooth;independentSteeringAt=now;independentProgressAt=now;independentWalkStatus="Turning toward the clear route";return;}
                }
            }
            independentHeading=heading;independentSteeringAt=now;
            var turnError=MathF.Abs(MathF.IEEERemainder(MathF.Atan2(delta.X,delta.Z)-heading,2*MathF.PI));
            if(turnError>1.05f){independentProgressAt=now;independentWalkStatus="Aligning with the route";return;}
            var speed=turnError>.5f?.4f:1f;independentSpeed=speed;
            var reference=legacy?camera->DirH+MathF.PI:self.Rotation;
            independentAxes=new(MathF.Sin(heading-reference)*speed,MathF.Cos(heading-reference)*speed);
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
