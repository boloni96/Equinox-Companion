using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private HelperNpc? helperNpcActive;
    private string helperTargetKind="EventNpc";
    private string helperSubmittedMenu="";
    private string helperQuestStatus="Waiting for the leader's NPC interaction.",helperWorkingId="";
    private bool helperApproaching,helperStopRequested;
    private DateTimeOffset helperNextAction,helperActionStarted,helperInteractRetryAt;
    private readonly FollowApproachProgress helperProgress=new();
    private readonly FollowStationaryGate helperStationary=new();
    private int helperInteractAttempts;
    private int helperTalkAttempts;
    private DateTimeOffset helperTalkSentAt;
    private bool QuestConversationVisible()=>VisibleFollowAddon("Talk")||VisibleFollowAddon("SelectString")||VisibleFollowAddon("SelectIconString")||VisibleFollowAddon("CutSceneSelectString")||VisibleFollowAddon("JournalAccept")||VisibleFollowAddon("JournalResult")||VisibleFollowAddon("DifficultySelectYesNo")||HelperSoloDutyVisible()||HelperReplayPromptVisible();
    private void CancelHelperApproach()
    {
        if(helperApproaching){try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception){}}
        helperApproaching=false;helperStopRequested=false;helperStationary.Reset();helperWorkingId="";
    }
    private void BlockHelper(string reason){
        helperLastIssue=reason;
        RecordFollowTravel("Helper blocked",new {reason,npc=helperNpcActive?.Name});
        helperConversations.Clear();
        if(helperIncoming.TryPeek(out var failed)&&helperNpcActive?.Conversation==failed.Npc.Conversation){SkipHelperConversation(failed,reason);return;}
        ClearHelperActions();helperError="Quest Helper: "+reason;helperQuestStatus=helperError;ResumeAfterConfirmedTravel();nextHelperStatus=default;
    }
    private unsafe void UpdateQuestHelper(DateTimeOffset now)
    {
        if(!helperPermission.Active||!helperPermission.Quest||HelperPaused||helperPermission.QuestPaused||helperBlocked.Length>0||!Player.IsLoaded||Objects.LocalPlayer is not {} self||now<helperNextAction)return;
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]){helperQuestStatus="Loading or occupied; waiting.";return;}
        if(!helperIncoming.TryPeek(out var a))return;
        if(!(helperReservedConversation==a.Npc.Conversation&&now<helperPlaybackUntil)&&!HelperPolicy.Fresh(a,now.ToUnixTimeMilliseconds(),followArmedAt)){BlockHelper("Recorded dialogue expired. Stop/start, then ask the leader to click the NPC again.");return;}
        if(a.Npc.World!=Player.CurrentWorld.RowId||a.Npc.Territory!=Client.TerritoryType){helperQuestStatus="Waiting to reach the NPC's area.";return;}
        if(HelperTravelBusy){helperQuestStatus="Waiting for travel to finish.";return;}
        if(HelperShopVisible()){BlockHelper("A shop is open; Quest Helper does not mirror vendors.");return;}
        if(helperWorkingId!=a.Id){helperWorkingId=a.Id;helperActionSubmitted=false;helperTalkAttempts=0;helperQuestMenuSelected=false;helperActionStarted=now;helperStopRequested=false;helperInteractAttempts=0;helperInteractRetryAt=default;helperStationary.Reset();followSession.Pause();RequestFollowMovementStop();}
        if(FollowMovementKeysHeld()){if(helperApproaching)CancelHelperApproach();helperQuestStatus="Your movement paused NPC approach.";return;}
        if(HelperQuestScenePolicy.Confirmation(a)){
            if(helperNpcActive?.Conversation!=a.Npc.Conversation){BlockHelper("Quest scene belongs to another interaction.");return;}
            if(helperNativeScenes.Contains(a.Scene)){RecordFollowTravel("Helper quest scene confirmed",new {a.QuestId,a.Scene});CompleteHelperAction(now);return;}
            if(now-helperActionStarted>TimeSpan.FromSeconds(8))BlockHelper("The recorded quest scene did not start; quest progress may differ.");return;
        }
        if(a.Kind=="interact"){
            helperTargetKind=HelperQuestScenePolicy.ObjectKind(a.Text);
            if(helperInteractAttempts==0&&a.QuestId!=0&&!HelperQuestScenePolicy.SameStep(a.Signature,FFXIVClientStructs.FFXIV.Client.Game.QuestManager.Instance()!=null&&FFXIVClientStructs.FFXIV.Client.Game.QuestManager.Instance()->IsQuestAccepted(a.QuestId),FFXIVClientStructs.FFXIV.Client.Game.QuestManager.GetQuestSequence(a.QuestId))){BlockHelper("Your quest step differs from the recorded interaction.");return;}
            if(helperNpcActive?.Conversation==a.Npc.Conversation&&a.QuestId!=0&&helperNativeScenes.Any(scene=>scene.StartsWith(a.QuestId+":",StringComparison.Ordinal))){CompleteHelperAction(now);return;}
            if(QuestConversationVisible()){
                if(helperNpcActive?.Conversation==a.Npc.Conversation){CompleteHelperAction(now);return;}
                if(TryAdoptHelperConversation(a,now))return;
                BlockHelper("Another conversation is already open and could not be verified against the recording. Close it, then stop/start assistance.");return;
            }
            // Keep observing scenes and windows while a native interaction opens its delayed prompt.
            if(HelperInteractionPolicy.Waiting(now,helperInteractRetryAt)){helperQuestStatus="Waiting for NPC dialogue — "+a.Npc.Name;return;}
            if(helperInteractAttempts>=3){BlockHelper("The NPC did not open a conversation after three spaced attempts.");return;}
            var map=AgentMap.Instance();if(map==null||map->CurrentMapId!=a.Npc.Map){helperQuestStatus="Waiting for the NPC's map.";return;}
            var targets=Objects.Where(o=>o.ObjectKind.ToString()==helperTargetKind&&o.BaseId==a.Npc.BaseId&&o.Name.TextValue==a.Npc.Name&&o.IsTargetable&&Vector3.DistanceSquared(o.Position,a.Npc.Position.Point)<1).Take(2).ToArray();
            if(targets.Length!=1){if(now-helperActionStarted>TimeSpan.FromSeconds(8))BlockHelper("The exact recorded NPC is not available here.");return;}
            var target=targets[0];var inRange=Vector3.Distance(self.Position,target.Position)<=target.HitboxRadius+2.5f;
            var goal=FollowCrystalApproach.Point(target.Position,config.FollowThem.PreferRightSide?HelperPolicy.Right(a.Npc.Approach.Point,a.Npc.Facing):a.Npc.Approach.Point,target.HitboxRadius);
            // Range is the fallback: no need to reach the preferred side through an obstacle.
            if(!inRange){
                if(Vector3.DistanceSquared(self.Position,goal)>3600||Math.Abs(self.Position.Y-goal.Y)>5){BlockHelper("NPC is too far away or on another level.");return;}
                if(!config.FollowThem.UseLifestream){BlockHelper("Move within NPC interaction range, or enable Lifestream direct approach.");return;}
                if(helperApproaching){if(helperProgress.Stuck(now,self.Position,goal,config.FollowThem.StuckSeconds)){BlockHelper("NPC approach is obstructed and the NPC is still out of range.");}return;}
                if(FollowTransitionBusy()||followStopPending||followStopUnconfirmed)return;
                try{if(LifestreamBusy())return;Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction([goal],true,.5f,.25f);helperApproaching=true;helperProgress.Reset(now,self.Position,goal);helperQuestStatus="Approaching NPC — "+a.Npc.Name;}
                catch(Exception){BlockHelper("Direct NPC approach is unavailable. Move nearby manually.");}return;
            }
            if(!helperStopRequested){if(helperApproaching){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception){return;}helperApproaching=false;}RequestFollowMovementStop();helperStopRequested=true;helperStationary.Reset();return;}
            if(followStopPending||followStopUnconfirmed||!helperStationary.Observe(now,self.Position,!FollowTransitionBusy()))return;
            var signal=HelperTargetSignal(a);
            if(!FaceTravelTarget(signal,target,now))return;
            helperReplaying=true;relayInteracting=true;
            try{ResetHelperNativeScene();Targets.Target=target;TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address,false);helperNpcActive=a.Npc;helperInteractAttempts++;helperInteractRetryAt=HelperInteractionPolicy.Deadline(now);helperNextAction=now.AddMilliseconds(450);RecordFollowTravel("Helper NPC interaction submitted",new {npc=a.Npc.Name,attempt=helperInteractAttempts,retryAt=helperInteractRetryAt});helperQuestStatus="Waiting for NPC dialogue — "+a.Npc.Name;}
            finally{helperReplaying=false;relayInteracting=false;}
            return;
        }
        if(helperNpcActive?.Conversation!=a.Npc.Conversation){BlockHelper("This dialogue belongs to a different NPC interaction.");return;}
        if(!NocturneNpc(a.Npc)&&EnsureHelperQuestPrerequisite(a,now))return;
        if(a.Kind=="talk"&&NocturneNpc(a.Npc)&&NocturneFirstQuest!=0&&VisibleHelperQuest()==NocturneFirstQuest){
            // Replay introduction differs from the first-time quest offer. Wait for explicit Replay Yes or exact acceptance.
            CompleteHelperAction(now);return;
        }
        if(a.Kind=="eventReplay"){UpdateHelperEventReplay(a,now);return;}
        if(a.Kind=="completeQuest"){UpdateHelperQuestResult(a,now);return;}
        if(a.Kind=="soloDuty"){UpdateHelperSoloDuty(a,now);return;}
        if(a.Kind=="acceptQuest"){UpdateHelperQuestAccept(a,now);return;}
        if(a.Kind=="skip"){UpdateHelperCutsceneSkip(a,now);return;}
        if(a.Kind=="talk"){
            var talk=HelperTalk();
            if(!helperActionSubmitted&&(talk.Signature!=a.Signature||talk.Text!=a.Text)){
                var advance=HelperCutsceneReplayPolicy.MatchingLaterTalk(helperIncoming.ToArray(),HelperScene(),talk.Text,talk.Signature);
                if(advance>0){
                    RecordFollowTravel("Helper dialogue already advanced",new {a.Scene,omitted=advance,text=talk.Text});
                    for(var i=0;i<advance;i++)CompleteHelperAction(now);
                    return;
                }
            }
            if(helperActionSubmitted){
                if(talk.Signature!=a.Signature||talk.Text!=a.Text){CompleteHelperAction(now);return;}
                if(HelperConversationPolicy.RetryTalk(helperTalkAttempts,(now-helperTalkSentAt).TotalMilliseconds)){helperActionSubmitted=false;return;}
                if(now-helperTalkSentAt>=TimeSpan.FromSeconds(2))BlockHelper("The matching dialogue did not advance after three spaced clicks.");return;
            }
            if(talk.Signature==a.Signature&&talk.Text==a.Text){
                helperReplaying=true;
                try{if(AdvanceQuestTalk(a)){
                    helperActionSubmitted=true;helperTalkAttempts++;helperTalkSentAt=now;helperNextAction=now.AddMilliseconds(450);
                    RecordFollowTravel("Helper dialogue submitted",new {a.Scene,a.QuestId,attempt=helperTalkAttempts,text=a.Text});
                }else{
                    helperQuestStatus="Waiting for the matching quest dialogue to become ready; close any manual prompt.";
                    if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("Matching dialogue could not be advanced; its scene or another open window requires manual attention.");
                }}finally{helperReplaying=false;}return;
            }
            if(now-helperActionStarted>TimeSpan.FromSeconds(5)){
                RecordFollowTravel("Helper dialogue mismatch",new {expected=a.Text,actual=talk.Text,expectedScene=a.Scene,actualScene=HelperScene(),expectedSignature=a.Signature,actualSignature=talk.Signature});
                BlockHelper("Dialogue differs from the leader's recorded line; no response selected.");
            }return;
        }
        if(a.Kind=="choice"){
            var menu=(AtkUnitBase*)GardenGui.GetAddonByName(a.Addon).Address;var choices=HelperChoices(menu,a.Addon);
            if(helperActionSubmitted){
                if(choices.Count==0||HelperPolicy.Signature(choices.OrderBy(x=>x,StringComparer.Ordinal))!=helperSubmittedMenu){CompleteHelperAction(now);return;}
                if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The response menu did not change after the selection.");return;
            }
            if(choices.Count==0){if(now-helperActionStarted>TimeSpan.FromSeconds(8))BlockHelper("The recorded response menu did not appear.");return;}
            RecordFollowTravel("Helper response menu",new {npc=a.Npc.Name,expected=a.Text,a.QuestId,choices});
            var index=HelperPolicy.Match(choices,a.Text);
            var isQuest=a.QuestId!=0&&HelperQuestIdForName(a.Text)==a.QuestId;
            if(isQuest&&index<0){SkipHelperConversation(a,"The same quest is unavailable in your NPC menu; waiting for the next interaction.");return;}
            if(index<0||!isQuest&&HelperPolicy.Signature(choices.OrderBy(x=>x,StringComparer.Ordinal))!=a.Signature){BlockHelper("The NPC's responses differ from the leader's; choose manually.");return;}
            helperSubmittedMenu=HelperPolicy.Signature(choices.OrderBy(x=>x,StringComparer.Ordinal));helperReplaying=true;try{SelectTravelChoice(menu,index);}finally{helperReplaying=false;}helperActionSubmitted=true;helperNextAction=now.AddMilliseconds(450);
        }
    }
    private FollowPortalSignal HelperTargetSignal(HelperAction a)=>new(a.Id,a.Name,a.World,a.Npc.World,"",a.Npc.Territory,a.Npc.Map,a.Npc.BaseId,0,a.Npc.Position.X,a.Npc.Position.Y,a.Npc.Position.Z,a.SentAt,"",SourceKind:helperTargetKind);
    private void CompleteHelperAction(DateTimeOffset now){
        var done=helperIncoming.Dequeue();helperStepDelays.Remove(done.Id);helperWorkingId="";helperActionSubmitted=false;
        if(helperIncoming.TryPeek(out var next))helperNextAction=now.AddMilliseconds(helperStepDelays.GetValueOrDefault(next.Id,450));
        else if(helperReservedConversation.Length>0){FinishHelperConversation("Recorded conversation completed; following resumed.");return;}
        else helperNextAction=now.AddMilliseconds(450);
        helperQuestStatus="In dialogue — following the leader's choices.";nextHelperStatus=default;
    }
}

