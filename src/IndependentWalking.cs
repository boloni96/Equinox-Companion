using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Conditions;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private CollisionRouteSearch? independentSearch;
    private NavigationSearchDiagnostic? independentLastSearchDiagnostic;
    private EntranceForwardProgress? independentEntranceForward;
    private readonly GameWalkingCollision independentCollision=new();
    private readonly WalkingGroundReadiness independentGroundReadiness=new();
    private unsafe delegate void GroundMovementRead(nint controller,float* left,float* forward,float* turn,byte* strafe,byte* flags,byte additive);
    private delegate byte GroundMovementEnabled(nint controller);
    private Hook<GroundMovementRead>? independentForwardHook;
    private GroundMovementEnabled? independentGroundEnabled1,independentGroundEnabled2;
    private Vector3 independentGoal,independentPrevious,independentLastProgress;
    private Vector3[]? independentPath;
    private int independentWaypoint,independentReplans;
    private readonly WalkingRecoveryBudget independentRecovery=new();
    private readonly List<Vector3> independentWalkedPoints=[];
    private ulong independentCharacter;
    private uint independentTerritory;
    private float independentRange;
    private Vector2 independentAxes;
    private float independentSpeed=1;
    private DateTimeOffset independentFacingAt;
    private bool independentFacingOnly,independentLegacyMode;
    private bool independentGameGateBlocked;
    private DateTimeOffset independentGatePauseSince;
    private Vector3[]? independentPendingPath;
    private float? independentHeading;
    private DateTimeOffset independentSteeringAt;
    private int independentLastRoutePoints,independentLastExpanded;
    private double independentPlanningSeconds;
    private readonly NavigationPhaseTimer independentPhaseTimer=new();
    private bool independentNearbyApproach;
    private readonly List<Vector3> independentBlockedTargets=[];
    private readonly RecentWalkingBlocks independentRecentBlocks=new();
    private bool independentBacktracking;
    private DateTimeOffset independentBacktrackUntil;
    private CollisionRouteSearch CreateIndependentSearch(Vector3 start,Vector3 goal,float range){
        var street=NavigationStreetCorridors.Guide(Client.TerritoryType,start,goal);
        if(street!=null)RecordTravelPrompt("Navigation street corridor",new{run=independentNavigationRun,territory=Client.TerritoryType,points=street.Select(FollowTravelPosition.From).ToArray(),source="Mist subdivision lower-to-upper street · every step requires native geometry validation"});
        return new(new AvoidStalledTargets(new GameWalkingCollision(cacheGround:true,roadGuide:independentRoadGuide),independentBlockedTargets.ToArray()),start,goal,range,independentBlockedTargets.Count>0&&Vector3.Distance(start,goal)<12?.5f:1f,roads:independentRoadRoute,streetGuide:street);
    }
    private bool IndependentTraversableConnector(Vector3 from,Vector3 to)=>AvoidStalledTargets.ClearOfBlocked(from,to,independentBlockedTargets)&&independentCollision.Traverse(from,to);
    private bool IndependentClearConnector(Vector3 from,Vector3 to)=>AvoidStalledTargets.ClearOfBlocked(from,to,independentBlockedTargets)&&independentCollision.ComfortableTraverse(from,to);
    private bool independentMoving,independentArrived,independentDirectOnly;
    private DateTimeOffset independentPulseUntil,independentSince,independentPlanningSince,independentProgressAt,independentCollisionAt;
    private int independentCheckedWaypoint=-1,independentInputReads,independentInputOverrides;
    private string independentNavigationRun="";
    private readonly WalkingInputLease independentLease=new();
    private int independentNativeGateRejects,independentNativeManualReads,independentNativeExceptions;
    private int independentForwardReads,independentExpiredReads,independentInactiveReads;
    private bool independentPositionChanged;
    private string independentInputBlock="",independentLastWalkFailure="";
    private string independentWalkStatus="Idle",independentWalkFailure="";
    private readonly QuestMovementPause questMovementPause=new();
    private bool independentUserPaused,independentSupportPaused;
    private bool QuestOwnsMovement=>independentQuestId!=0||nativeEventActive||eventAccountActive;
    private bool QuestManualMovementHeld(DateTimeOffset now)=>questMovementPause.Observe(now,FollowMovementKeysHeld());
    private unsafe void IndependentGroundInput(nint controller,float* left,float* forward,float* turn,byte* strafe,byte* flags,byte additive)
    {
        independentForwardHook!.Original(controller,left,forward,turn,strafe,flags,additive);
        independentInputReads++;
        if(controller==0||left==null||forward==null||turn==null||additive!=0)return;
        independentForwardReads++;
        if(QuestOwnsMovement&&!WalkingInputLease.CanSupplyGround(*left,*forward,*turn)){
            questMovementPause.Observe(DateTimeOffset.UtcNow,true);
            independentLease.Revoke();return;
        }
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
            independentLegacyMode=legacy;
            if(legacy){
                var reference=camera->DirH+MathF.PI;
                independentAxes=WalkingSteering.HeadingAxes(heading,reference,independentSpeed);
                independentFacingOnly=false;independentFacingAt=default;
            }else{
                // Turn visually without suppressing speed or moving along the
                // old facing. Project the checked direction into current controls.
                // All game/manual-input checks above still take priority.
                var now=DateTimeOffset.UtcNow;
                var dt=independentFacingAt==default?1f/60:(float)(now-independentFacingAt).TotalSeconds;
                independentFacingAt=now;
                var facing=WalkingSteering.SmoothHeading(player.Rotation,heading,dt);
                ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address)->SetRotation(facing);
                independentFacingOnly=false;
                independentAxes=WalkingSteering.HeadingAxes(heading,player.Rotation,independentSpeed);
            }
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
        if(independentSearch!=null)independentLastSearchDiagnostic=independentSearch.DiagnosticSnapshot();
        if(independentSearch!=null||independentPath!=null||independentEntranceForward!=null)
            RecordTravelPrompt("Navigation ended",new{run=independentNavigationRun,reason,planning=independentSearch?.DiagnosticSnapshot(),goal=FollowTravelPosition.From(independentGoal),independentInputReads,independentInputOverrides,independentExpiredReads,independentInactiveReads,independentNativeGateRejects,independentNativeManualReads,independentReplans,independentPlanningSeconds,independentWalkStatus,controlMode=independentLegacyMode?"Legacy":"Standard",axes=new{x=independentAxes.X,y=independentAxes.Y},independentHeading,independentFacingOnly});
        if(independentSearch!=null||independentPath!=null||independentEntranceForward!=null)TraceKeyboundRun(independentNavigationRun,"navigation-stopped",reason,"companion-navigation",false);
        independentBacktracking=false;independentInteractionTarget=0;independentEntranceForward=null;independentGroundReadiness.Reset();independentSupportPaused=false;independentFacingAt=default;independentFacingOnly=false;
        StopIndependentFlight();
        independentUserPaused=false;independentGameGateBlocked=false;independentGatePauseSince=default;independentHeading=null;independentMoving=false;independentPulseUntil=default;independentLease.Revoke();independentForwardHook?.Disable();
        independentSearch=null;independentPath=null;independentPendingPath=null;independentArrived=false;independentDirectOnly=false;independentWalkStatus=reason;independentWalkFailure="";
        if(resetFailure)independentLastWalkFailure="";
    }
    private unsafe bool IndependentWalk(Vector3 destination,float range,DateTimeOffset now,bool allowFlight=true,bool preferFlight=false,bool directOnly=false,float directDistance=4,Dalamud.Game.ClientState.Objects.Types.IGameObject? interactionTarget=null)
    {
        if(Objects.LocalPlayer is not {} self)return false;
        if(interactionTarget!=null&&GameWalkingCollision.Available()&&StopAtInteractionTarget(interactionTarget))return true;
        independentInteractionTarget=interactionTarget?.GameObjectId??0;
        if(FlightActive){if(Vector3.DistanceSquared(flightGoal,destination)<1)return false;StopIndependentWalk("Flight destination changed");}
        if(followStopPending||followStopUnconfirmed||nativeFollowRequested)throw new InvalidOperationException("Waiting for game follow to stop before navigating.");
        if(!GameWalkingCollision.Available()){independentWalkStatus="Waiting for collision geometry to load";return false;}
        if(IndependentOtherTravelBusy())throw new InvalidOperationException("Another travel task is running; local walking was not started.");
        if(independentWalkFailure.Length>0&&Vector3.DistanceSquared(independentGoal,destination)<1)throw new InvalidOperationException(independentWalkFailure);
        if(independentArrived&&Vector3.DistanceSquared(independentGoal,destination)<1&&CollisionRouteSearch.Horizontal(self.Position,destination)<=range&&(directOnly||MathF.Abs(self.Position.Y-destination.Y)<1.2f))return true;
        if(independentSearch!=null||independentPath!=null||independentEntranceForward!=null){
            if(Vector3.DistanceSquared(independentGoal,destination)<1&&independentTerritory==Client.TerritoryType)return false;
            StopIndependentWalk("Destination changed");
        }
        if(FlightNavigationPolicy.Prefer(Control.CanFly,allowFlight,preferFlight||Conditions[ConditionFlag.InFlight],Vector3.Distance(self.Position,destination))&&StartIndependentFlight(destination,range,now))return false;
        if(Conditions[ConditionFlag.InFlight])throw new InvalidOperationException("No supported landing corridor at this destination; flight remains under your control.");
        // Geometry can report loaded before support at the teleport landing is queryable.
        // Retry fresh collision reads without sending movement or caching a failed origin.
        if(!directOnly&&!independentCollision.Ground(self.Position,out _)){
            independentWalkStatus="Waiting for walkable ground at the arrival point (up to 10s)";
            if(independentGroundReadiness.MayWait(self.Position,Client.TerritoryType,Player.ContentId,now))return false;
            throw new InvalidOperationException($"No walkable ground beneath the character after 10s · territory {Client.TerritoryType} · position {self.Position}.");
        }
        independentGroundReadiness.Reset();
        if(!PrepareNavigationMap(now,self.Position,destination))return false;
        independentLastSearchDiagnostic=null;independentNavigationRun=Guid.NewGuid().ToString("N");independentInputReads=independentInputOverrides=0;
        independentForwardReads=independentExpiredReads=independentInactiveReads=0;independentNativeGateRejects=independentNativeManualReads=independentNativeExceptions=0;
        independentInputBlock=independentLastWalkFailure="";independentPositionChanged=false;independentLastRoutePoints=independentLastExpanded=0;independentPlanningSeconds=0;independentLease.Revoke();
        TraceKeyboundRun(independentNavigationRun,"navigation-started",$"Destination {destination}; range {range}",independentQuestId!=0?"quest:"+independentQuestRun:"event",false);
        independentBacktracking=false;independentBlockedTargets.Clear();independentBlockedTargets.AddRange(independentRecentBlocks.Get(Client.TerritoryType,Player.CurrentWorld.RowId,now));independentRecovery.Reset();independentWalkedPoints.Clear();independentWalkedPoints.Add(self.Position);independentPhaseTimer.Begin(now);
        independentNearbyApproach=directDistance>4&&Vector3.Distance(self.Position,destination)<=16;
        independentInteractionTarget=interactionTarget?.GameObjectId??0;
        independentDirectOnly=directOnly;independentGoal=destination;independentRange=range;independentCharacter=Player.ContentId;independentTerritory=Client.TerritoryType;
        independentWalkFailure="";independentArrived=false;independentReplans=0;independentSince=independentPlanningSince=independentProgressAt=now;
        independentPrevious=independentLastProgress=self.Position;
        // Short verified approaches (including the shared three-yalm estate step)
        // do not need an area search or a detour to satisfy preferred wall clearance.
        var direct=directOnly?VerifiedWalkingOpening.Entrance(independentCollision,self.Position,destination):VerifiedWalkingOpening.ShortDirect(independentCollision,self.Position,destination,directDistance);
        if(!directOnly&&direct.Length>1&&!WalkingClearance.PreservesRoom(self.Position,destination,independentCollision.WallPenalty))direct=[];
        if(direct.Length>1&&Enumerable.Range(1,direct.Length-1).Any(i=>!AvoidStalledTargets.ClearOfBlocked(direct[i-1],direct[i],independentBlockedTargets)))direct=[];
        if(direct.Length>1){
            independentPath=direct;independentWaypoint=1;independentCheckedWaypoint=-1;
            independentPhaseTimer.SetPlanning(false,now);independentWalkStatus="Short approach ready";return false;
        }
        if(directOnly){
            independentEntranceForward=new(self.Position,destination,now);
            independentPhaseTimer.SetPlanning(false,now);
            TraceKeyboundRun(independentNavigationRun,"entrance-forward-attempt",$"Collision model rejected entry; normal game movement from {self.Position} toward {destination}; max 3 yalms, stall limit 1.5s","estate-entry",false);
            independentWalkStatus="Trying the short entrance step · watching actual movement";return false;
        }
        var cached=independentBlockedTargets.Count==0?RecallIndependentRoute(self.Position,destination,range):null;
        if(cached!=null){independentPath=cached;independentWaypoint=1;independentCheckedWaypoint=-1;independentPhaseTimer.SetPlanning(false,now);independentWalkStatus="Using a saved route · checking the next section";return false;}
        // The opening is fully checked; the remainder starts at its fixed endpoint.
        var opening=RecallIndependentPrefix(self.Position,destination)??(independentBlockedTargets.Count==0?VerifiedWalkingOpening.Build(independentCollision,self.Position,destination,range):[]);
        if(opening.Length>1){independentPath=opening;independentWaypoint=1;independentCheckedWaypoint=-1;}
        independentSearch=CreateIndependentSearch(opening.Length>1?opening[^1]:self.Position,destination,range);
        independentWalkStatus=opening.Length>1?"Opening route ready · calculating ahead":"Checking collision geometry and finding a route";return false;
    }
    private unsafe void UpdateIndependentWalk(DateTimeOffset now)
    {
        if(GameWalkingCollision.Available()&&independentInteractionTarget!=0&&Player.ContentId==independentCharacter&&Client.TerritoryType==independentTerritory&&
            StopAtInteractionTarget(Objects.FirstOrDefault(x=>x.GameObjectId==independentInteractionTarget)))return;
        if(FlightActive){TickIndependentFlight(now);return;}
        if(independentSearch==null&&independentPath==null&&independentEntranceForward==null)return;
        try{
            independentPulseUntil=default;independentLease.Revoke();independentMoving=false;
            if(QuestOwnsMovement&&QuestManualMovementHeld(now)){
                independentUserPaused=true;independentNativeManualReads=0;independentHeading=null;
                independentProgressAt=independentSince=now;independentPhaseTimer.Begin(now);
                independentWalkStatus="Paused for your movement · resumes when released";return;
            }
            if(independentUserPaused){
                independentUserPaused=false;independentNativeManualReads=0;
                if(Objects.LocalPlayer is {} moved){
                    independentBacktracking=false;independentSearch=CreateIndependentSearch(moved.Position,independentGoal,independentRange);
                    independentPath=independentPendingPath=null;independentWaypoint=0;independentCheckedWaypoint=-1;
                    independentLastProgress=independentPrevious=moved.Position;independentReplans=0;
                    independentProgressAt=independentPlanningSince=independentSince=now;independentPhaseTimer.Begin(now);
                    independentWalkStatus="Resuming from your position · checking route";
                }
            }
            if(independentNativeManualReads>0)throw new InvalidOperationException("Navigation stopped: another movement input took priority.");
            if(independentNativeExceptions>0)throw new InvalidOperationException("Ground movement callback failed: "+independentInputBlock);
            if(IndependentOtherTravelBusy())throw new InvalidOperationException("Another travel task took control; local walking stopped.");
            if(!Player.IsLoaded||Player.ContentId!=independentCharacter||Client.TerritoryType!=independentTerritory)throw new InvalidOperationException("Navigation stopped: character or territory changed.");
            if(!QuestOwnsMovement&&FollowMovementKeysHeld())throw new InvalidOperationException("Navigation stopped by your movement.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Mounted]||Conditions[ConditionFlag.InFlight])throw new InvalidOperationException("Navigation stopped for loading, combat, duty or mounted movement. Dismount for ground walking.");
            if(now-independentSince>TimeSpan.FromMinutes(15))throw new InvalidOperationException("Navigation exceeded its fifteen-minute total limit.");
            if(independentSearch!=null&&independentNearbyApproach&&now-independentPlanningSince>TimeSpan.FromSeconds(5))throw new InvalidOperationException($"Nearby approach blocked: no route found within 5 seconds ({independentSearch.Expanded} areas checked). Move to a clear side of the aetheryte and retry.");
            if(independentSearch!=null&&independentReplans>0&&now-independentPlanningSince>TimeSpan.FromSeconds(15))throw new InvalidOperationException("Blocked-route recovery found no alternative within 15 seconds; stopped without continuing to push the corner.");
            if(independentSearch!=null&&independentPhaseTimer.Expired(now))throw new InvalidOperationException(independentSearch!=null?"Route calculation timed out after 90 seconds.":"Walking timed out after 90 seconds.");
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
            if(independentEntranceForward is {} entrance){
                var progress=entrance.Observe(self.Position,now);
                if(progress==EntranceProgress.Arrived){
                    StopIndependentWalk("Forward entrance segment complete");independentArrived=true;return;
                }
                if(progress!=EntranceProgress.Walking)
                    throw new InvalidOperationException($"Entrance forward step stopped: {progress}; position {self.Position}. No circling or repeated push was started.");
                independentHeading=entrance.Heading;independentSpeed=.5f;
                EnsureIndependentGroundInput();
                if(!independentForwardHook!.IsEnabled)independentForwardHook.Enable();
                independentMoving=true;independentLease.Arm(DateTimeOffset.UtcNow);independentPulseUntil=independentLease.Until;
                independentWalkStatus="Walking forward into the plot · watching actual progress";return;
            }
            // A transient stair/landing support miss must not poison route memory
            // or immediately start a search whose origin is known to be unreadable.
            if(!independentCollision.Ground(self.Position,out _)){
                if(!independentSupportPaused){
                    independentGroundReadiness.Reset();independentSupportPaused=true;
                    RecordTravelPrompt("Navigation support unavailable",new{run=independentNavigationRun,position=FollowTravelPosition.From(self.Position),reason=independentCollision.GroundFailure});
                }
                independentProgressAt=now;
                if(!independentGroundReadiness.MayWait(self.Position,independentTerritory,independentCharacter,now,2)){
                    ForgetIndependentRoute(self.Position);
                    throw new InvalidOperationException("Ground support remained unavailable for 2 seconds: "+independentCollision.GroundFailure);
                }
                independentWalkStatus="Rechecking ground support · movement paused";return;
            }
            if(independentSupportPaused){
                independentSupportPaused=false;independentGroundReadiness.Reset();independentProgressAt=now;
                if(independentSearch?.Error=="No walkable ground beneath the character.")independentSearch=CreateIndependentSearch(self.Position,independentGoal,independentRange);
                RecordTravelPrompt("Navigation support restored",new{run=independentNavigationRun,position=FollowTravelPosition.From(self.Position)});
            }
            if(independentSearch!=null){
                independentMoving=false;independentSearch.Advance(milliseconds:independentNearbyApproach?1.5:3,edgeBudget:independentNearbyApproach?48:192);
                independentLastExpanded=independentSearch.Expanded;independentPlanningSeconds=(DateTimeOffset.UtcNow-independentPlanningSince).TotalSeconds;
                independentWalkStatus=$"Finding walkable route · {independentSearch.Expanded} areas checked";
                if(independentSearch.Error.Length>0)throw new InvalidOperationException(independentSearch.Error);
                if(independentSearch.Path==null&&independentPath==null)return;
                if(independentSearch.Path!=null){
                TraceKeyboundRun(independentNavigationRun,"route-found",$"{independentSearch.Path.Length} points; {independentSearch.Expanded} nodes checked","companion-navigation",false);
                independentPhaseTimer.SetPlanning(false,DateTimeOffset.UtcNow);
                var found=independentSearch.Path;
                if(independentPath!=null){
                    var joined=WalkingSteering.JoinContinuation(independentPath,found);
                    if(joined!=null){independentPath=joined;independentPendingPath=null;}
                    else independentPendingPath=found;
                }
                else {independentPath=found;independentWaypoint=1;independentCheckedWaypoint=-1;}
                // Save after observed arrival, not merely after planning succeeds.
                independentLastRoutePoints=found.Length;independentSearch=null;
                independentLastProgress=self.Position;independentProgressAt=DateTimeOffset.UtcNow;
                }
            }
            if(independentBacktracking){
                if(independentPath!=null&&Vector3.Distance(self.Position,independentPath[^1])<=.45f){
                    independentMoving=false;independentLease.Revoke();independentForwardHook?.Disable();independentBacktracking=false;
                    independentPath=null;independentWalkedPoints.Clear();independentWalkedPoints.Add(self.Position);independentPlanningSince=now;independentPhaseTimer.SetPlanning(true,now);
                    independentSearch=CreateIndependentSearch(self.Position,independentGoal,independentRange);
                    RecordTravelPrompt("Navigation backtrack complete",new{run=independentNavigationRun,position=FollowTravelPosition.From(self.Position)});
                    independentWalkStatus="Back on the travelled path · finding an alternative";return;
                }
                if(now>independentBacktrackUntil)throw new InvalidOperationException("Could not back away from the obstruction within 6 seconds; movement stopped.");
            }
            if(CollisionRouteSearch.Horizontal(self.Position,independentGoal)<=independentRange&&MathF.Abs(self.Position.Y-independentGoal.Y)<=1.2f){
                independentWalkedPoints.Add(self.Position);RememberIndependentRoute(independentWalkedPoints.ToArray());
                StopIndependentWalk("Arrived · movement released");independentArrived=true;return;
            }
            if(independentPath!=null&&CollisionRouteSearch.Horizontal(self.Position,independentPath[^1])<.3f&&MathF.Abs(self.Position.Y-independentPath[^1].Y)<.5f){
                if(independentPendingPath!=null){independentPath=independentPendingPath;independentPendingPath=null;independentWaypoint=1;independentCheckedWaypoint=-1;}
                else if(independentSearch!=null){independentProgressAt=now;independentWalkStatus="Verified section complete · calculating the next section";return;}
            }
            if(independentPath!=null)independentWaypoint=WalkingSteering.AdvanceWaypoint(self.Position,independentPath,independentWaypoint,IndependentTraversableConnector,independentPrevious);
            if(independentPath==null||independentWaypoint>=independentPath.Length)throw new InvalidOperationException("Route ended without confirmed arrival.");
            var target=WalkingSteering.PursuitTarget(self.Position,independentPath,independentWaypoint,IndependentClearConnector,IndependentTraversableConnector);
            if(Vector3.Distance(self.Position,independentLastProgress)>.3f){independentProgressAt=now;independentLastProgress=self.Position;independentPositionChanged=true;
                if(!independentBacktracking&&(independentWalkedPoints.Count==0||Vector3.Distance(independentWalkedPoints[^1],self.Position)>=.75f))independentWalkedPoints.Add(self.Position);}
            var checkCollision=now>=independentCollisionAt||independentCheckedWaypoint!=independentWaypoint;
            if(checkCollision){
                independentCollisionAt=now.AddMilliseconds(150);independentCheckedWaypoint=independentWaypoint;
            }
            var stalled=now-independentProgressAt>TimeSpan.FromSeconds(independentInputOverrides>0?1.5:4);
            var noConnector=Vector3.DistanceSquared(self.Position,target)<.0001f;
            if(noConnector||stalled||!AvoidStalledTargets.ClearOfBlocked(self.Position,target,independentBlockedTargets)||checkCollision&&!independentCollision.Traverse(self.Position,target)){
                independentMoving=false;independentForwardHook?.Disable();
                if(independentInputOverrides==0&&now-independentProgressAt>TimeSpan.FromSeconds(4))throw new InvalidOperationException($"Movement not delivered · ground reads {independentForwardReads}, game blocked {independentNativeGateRejects}, other input {independentNativeManualReads}, expired {independentExpiredReads}. {independentInputBlock}");
                if(independentBacktracking)throw new InvalidOperationException("The checked backtrack became blocked; movement stopped.");
                if(independentDirectOnly)throw new InvalidOperationException("The short plot-entry step became blocked; stopped without circling the plot.");
                independentReplans++;
                if(!independentRecovery.Try(self.Position))throw new InvalidOperationException("Route remained blocked after four replans without sufficient progress; stopped.");
                var blockedAt=noConnector?WalkingSteering.BoundedTarget(self.Position,independentPath[independentWaypoint]):stalled?Vector3.Lerp(self.Position,target,MathF.Min(1,.6f/MathF.Max(.01f,Vector3.Distance(self.Position,target)))):independentCollision.LastBlockedAt??target;
                ForgetIndependentRoute(blockedAt);independentPendingPath=null;independentBlockedTargets.Add(blockedAt);
                if(stalled)independentRecentBlocks.Add(Client.TerritoryType,Player.CurrentWorld.RowId,blockedAt,now);
                RecordNavigationObstruction(self.Position,target,blockedAt,stalled);independentPhaseTimer.SetPlanning(true,now);independentPlanningSince=now;
                independentSearch=CreateIndependentSearch(self.Position,independentGoal,independentRange);independentPath=null;independentProgressAt=now;
                TraceKeyboundRun(independentNavigationRun,"route-replan",$"Attempt {independentReplans}; avoiding obstacle {blockedAt}; target {target}; position {self.Position}; stalled {stalled}; no clearance-safe connector {noConnector}; input reads {independentInputReads}, overrides {independentInputOverrides}","companion-navigation",false);
                if(stalled){
                    var retreat=WalkingBacktrack.Build(self.Position,independentWalkedPoints,IndependentClearConnector);
                    if(retreat.Length>1){
                        independentSearch=null;independentPath=retreat;independentWaypoint=1;independentCheckedWaypoint=-1;
                        independentBacktracking=true;independentBacktrackUntil=now.AddSeconds(6);independentHeading=null;
                        independentLastProgress=self.Position;independentProgressAt=now;independentPhaseTimer.SetPlanning(false,now);
                        RecordTravelPrompt("Navigation backtrack started",new{run=independentNavigationRun,points=retreat.Select(FollowTravelPosition.From).ToArray()});
                        independentWalkStatus="Backing away along the checked travelled path";return;
                    }
                }
                independentWalkStatus="Path changed or blocked · finding another route";return;
            }
            var cameras=CameraManager.Instance();
            var camera=cameras==null?null:(FFXIVClientStructs.FFXIV.Client.Game.Camera*)cameras->GetActiveCamera();
            if(camera==null)throw new InvalidOperationException("Movement camera unavailable.");
            var legacy=MarketGameConfig.UiControl.TryGetUInt("MoveMode",out var mode)&&mode==1;
            var delta=target-self.Position;var heading=MathF.Atan2(delta.X,delta.Z);
            // Pursuit has already chosen a checked path chord. Smoothing this
            // world heading again drifted off that chord and modulated speed.
            independentHeading=heading;independentSteeringAt=now;
            independentSpeed=1;
            var reference=legacy?camera->DirH+MathF.PI:self.Rotation;
            independentAxes=WalkingSteering.HeadingAxes(heading,reference,independentSpeed);
            EnsureIndependentGroundInput();
            // Start the lease AFTER collision work and hook setup, not at frame start.
            if(!independentForwardHook!.IsEnabled)independentForwardHook.Enable();
            independentMoving=true;independentLease.Arm(DateTimeOffset.UtcNow);independentPulseUntil=independentLease.Until;
            independentWalkStatus=independentPositionChanged?$"Walking · point {independentWaypoint}/{independentPath.Length-1}":"Ready to move · waiting for movement confirmation";independentPrevious=self.Position;
        }catch(Exception e){StopIndependentWalk(e.Message);independentWalkFailure=independentLastWalkFailure=e.Message;Log.Warning(e,"Independent navigation stopped");}
    }
    private bool IndependentMovementWindow()=>QuestConversationVisible()||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("SystemMenu");
    private bool IndependentOtherTravelBusy(){try{return LifestreamBusy();}catch{return false;}}
    private void DisposeIndependentWalk(){StopIndependentWalk("Plugin unloaded");independentForwardHook?.Dispose();independentForwardHook=null;independentFlightHook?.Dispose();independentFlightHook=null;}
}


