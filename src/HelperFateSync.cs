using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ushort helperObservedFate;
    private int helperObservedFateStart;
    private bool helperFateObserved;
    private HelperAction? helperPendingFate;
    private DateTimeOffset helperNextFateAttempt;
    private int helperFateAttempts;
    private bool helperFateApproaching,helperFateStopRequested,helperFateFallback;
    private readonly FollowApproachProgress helperFateProgress=new();
    private void CancelHelperFateApproach()
    {
        if(helperFateApproaching){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception){}RequestFollowMovementStop();}
        helperFateApproaching=false;helperFateStopRequested=false;
    }

    private unsafe void UpdateHelperFateSync(DateTimeOffset now)
    {
        if(!Player.IsLoaded||Objects.LocalPlayer is not {} self)return;
        var manager=FateManager.Instance();if(manager==null)return;
        var fate=manager->CurrentFate;
        var synced=manager->SyncedFateId;
        var start=synced==0?0:fate==null?helperObservedFateStart:fate->StartTimeEpoch;
        var changed=helperFateObserved&&(synced!=helperObservedFate||start!=helperObservedFateStart);
        helperFateObserved=true;helperObservedFate=synced;helperObservedFateStart=start;
        if(changed&&SharingQuest&&synced!=0&&fate!=null&&fate->FateId==synced&&fate->State==FateState.Running){
            var sessions=helperFollowers.Where(f=>HelperPolicy.Audience(f,now.ToUnixTimeMilliseconds())).Select(f=>f.Id).ToArray();
            var map=AgentMap.Instance();
            if(sessions.Length>0&&map!=null&&helperOutgoing.Count<32){
                var point=FollowTravelPosition.From(self.Position);
                var context=new HelperNpc(Guid.NewGuid().ToString("N"),synced,"FATE",Client.TerritoryType,map->CurrentMapId,Player.CurrentWorld.RowId,point,point,self.Rotation);
                helperOutgoing.Enqueue(new(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,"fateSync",now.ToUnixTimeMilliseconds(),context,Sessions:sessions,FateId:synced,FateStart:start));
                RecordFollowTravel("Helper shared FATE sync",new {fate=synced,start});
            }
        }
        if(helperPendingFate is not {} action)return;
        if(!helperPermission.Allows("fateSync")||!HelperPolicy.Fresh(action,now.ToUnixTimeMilliseconds(),followArmedAt)){helperPendingFate=null;CancelHelperFateApproach();return;}
        if(HelperQuestBusy){CancelHelperFateApproach();return;}
        if(HelperTravelBusy||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.Unconscious]||now<helperNextFateAttempt)return;
        if(action.Npc.World!=Player.CurrentWorld.RowId||action.Npc.Territory!=Client.TerritoryType)return;
        var destination=manager->GetFateById(action.FateId);
        if(destination==null||destination->StartTimeEpoch!=action.FateStart||destination->State!=FateState.Running){CancelHelperFateApproach();helperPendingFate=null;return;}
        var position=self.Position;
        var inside=fate!=null&&fate->FateId==action.FateId&&fate->StartTimeEpoch==action.FateStart&&manager->IsInFateRadius(&position);
        var goal=HelperPolicy.Right(action.Npc.Approach.Point,action.Npc.Facing);
        if(Vector3.Distance(goal,destination->Location)>destination->Radius-.5f)goal=action.Npc.Approach.Point;
        if(FollowMovementKeysHeld()){CancelHelperFateApproach();return;}
        if(!inside||config.FollowThem.UseLifestream&&!helperFateFallback&&Vector3.DistanceSquared(position,goal)>1){
            if(helperFateApproaching){if(helperFateProgress.Stuck(now,position,goal,config.FollowThem.StuckSeconds)){CancelHelperFateApproach();if(inside)helperFateFallback=true;else{helperPendingFate=null;helperError="FATE approach obstructed; following remains active.";}}return;}
            if(!config.FollowThem.UseLifestream||Vector3.DistanceSquared(position,goal)>3600||Math.Abs(position.Y-goal.Y)>5)return;
            if(!helperFateStopRequested){followSession.Pause();RequestFollowMovementStop();helperFateStopRequested=true;return;}
            if(FollowTransitionBusy()||followStopPending||followStopUnconfirmed)return;
            try{if(LifestreamBusy())return;Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction([goal],true,.5f,.25f);helperFateApproaching=true;helperFateProgress.Reset(now,position,goal);}
            catch(Exception){helperError="FATE direct approach unavailable; move into its area to sync.";helperPendingFate=null;}return;
        }
        // Being in the correct FATE area is sufficient: do not insist on an obstructed preferred spot.
        if(helperFateApproaching){CancelHelperFateApproach();helperFateStopRequested=true;}
        if(!helperFateStopRequested){followSession.Pause();RequestFollowMovementStop();helperFateStopRequested=true;return;}
        if(followStopPending||followStopUnconfirmed)return;
        if(manager->IsSyncedToFate(fate)||Player.Level<=fate->MaxLevel){RecordFollowTravel("Helper FATE ready",new {action.FateId});helperPendingFate=null;return;}
        if(helperFateAttempts>=3){helperError="FATE Level Sync was not confirmed; use Level Sync manually.";RecordFollowTravel("Helper FATE blocked",new {action.FateId});helperPendingFate=null;return;}
        manager->LevelSync();helperFateAttempts++;helperNextFateAttempt=now.AddSeconds(2);
    }
}
