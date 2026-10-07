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
    private string helperQuestStatus="Waiting for the leader's NPC interaction.",helperWorkingId="";
    private bool helperApproaching,helperStopRequested;
    private DateTimeOffset helperNextAction,helperActionStarted;
    private readonly FollowApproachProgress helperProgress=new();
    private readonly FollowStationaryGate helperStationary=new();
    private int helperInteractAttempts;
    private bool QuestConversationVisible()=>VisibleFollowAddon("Talk")||VisibleFollowAddon("SelectString")||VisibleFollowAddon("SelectIconString")||VisibleFollowAddon("CutSceneSelectString")||VisibleFollowAddon("JournalAccept")||VisibleFollowAddon("JournalResult")||HelperReplayPromptVisible();
    private void CancelHelperApproach()
    {
        if(helperApproaching){try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception){}}
        helperApproaching=false;helperStopRequested=false;helperStationary.Reset();helperWorkingId="";
    }
    private void BlockHelper(string reason){RecordFollowTravel("Helper blocked",new {reason,npc=helperNpcActive?.Name});helperBlocked=reason;helperQuestStatus="Blocked — "+reason;CancelHelperApproach();RequestFollowMovementStop();nextHelperStatus=default;}
    private unsafe void UpdateQuestHelper(DateTimeOffset now)
    {
        if(!helperPermission.Active||!helperPermission.Quest||HelperPaused||helperPermission.QuestPaused||helperBlocked.Length>0||!Player.IsLoaded||Objects.LocalPlayer is not {} self||now<helperNextAction)return;
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]){helperQuestStatus="Loading or occupied; waiting.";return;}
        if(!helperIncoming.TryPeek(out var a))return;
        if(!HelperPolicy.Fresh(a,now.ToUnixTimeMilliseconds(),followArmedAt)){BlockHelper("Recorded dialogue expired. Stop/start, then ask the leader to click the NPC again.");return;}
        if(a.Npc.World!=Player.CurrentWorld.RowId||a.Npc.Territory!=Client.TerritoryType){helperQuestStatus="Waiting to reach the NPC's area.";return;}
        if(HelperTravelBusy){helperQuestStatus="Waiting for travel to finish.";return;}
        if(helperWorkingId!=a.Id){helperWorkingId=a.Id;helperActionStarted=now;helperStopRequested=false;helperInteractAttempts=0;helperStationary.Reset();followSession.Pause();RequestFollowMovementStop();}
        if(FollowMovementKeysHeld()){if(helperApproaching)CancelHelperApproach();helperQuestStatus="Your movement paused NPC approach.";return;}
        if(a.Kind=="interact"){
            if(QuestConversationVisible()){
                if(helperNpcActive?.Conversation==a.Npc.Conversation){CompleteHelperAction(now);return;}
                BlockHelper("Another conversation is already open. Close it, then stop/start assistance.");return;
            }
            var map=AgentMap.Instance();if(map==null||map->CurrentMapId!=a.Npc.Map){helperQuestStatus="Waiting for the NPC's map.";return;}
            var targets=Objects.Where(o=>o.ObjectKind.ToString()=="EventNpc"&&o.BaseId==a.Npc.BaseId&&o.Name.TextValue==a.Npc.Name&&o.IsTargetable&&Vector3.DistanceSquared(o.Position,a.Npc.Position.Point)<1).Take(2).ToArray();
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
            if(helperInteractAttempts>=3){BlockHelper("The NPC did not open a conversation after three attempts.");return;}
            helperReplaying=true;relayInteracting=true;
            try{Targets.Target=target;TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address,false);helperNpcActive=a.Npc;helperInteractAttempts++;helperNextAction=now.AddSeconds(1);helperQuestStatus="Waiting for NPC dialogue — "+a.Npc.Name;}
            finally{helperReplaying=false;relayInteracting=false;}
            return;
        }
        if(helperNpcActive?.Conversation!=a.Npc.Conversation){BlockHelper("This dialogue belongs to a different NPC interaction.");return;}
        if(a.Kind=="talk"&&NocturneNpc(a.Npc)&&NocturneFirstQuest!=0&&VisibleHelperQuest()==NocturneFirstQuest){
            // Replay introduction differs from the first-time quest offer. Wait for explicit Replay Yes or exact acceptance.
            CompleteHelperAction(now);return;
        }
        if(a.Kind=="eventReplay"){UpdateHelperEventReplay(a,now);return;}
        if(a.Kind=="acceptQuest"){UpdateHelperQuestAccept(a,now);return;}
        if(a.Kind=="skip"){
            if(!helperPermission.Skip){CompleteHelperAction(now);return;}
            if(!HelperPolicy.SceneMatches(a.Scene,HelperScene())){if(now-helperActionStarted>TimeSpan.FromSeconds(8))BlockHelper("Cutscene differs; skip was not applied.");return;}
            var agent=AgentCutscene.Instance();if(agent==null)return;
            
            if(agent->SkipDialogAddonId==0){
                if(agent->SkipCallback!=null){helperReplaying=true;try{agent->OpenSkipDialog(agent->SkipCallback);}finally{helperReplaying=false;}helperNextAction=now.AddSeconds(1);return;}
                helperQuestStatus="Open the cutscene Skip prompt to continue; no verified skip callback is available yet.";return;
            }
            // Require the game-owned skip dialog, not an unrelated Yes/No window.
            foreach(var name in new[]{"SelectString","SelectYesno"}){var dialog=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;if(dialog==null||!dialog->IsVisible||dialog->Id!=agent->SkipDialogAddonId)continue;var choice=0;if(name=="SelectString"){var labels=TransportChoices(dialog);var yesLabels=new[]{"Yes.","Yes","Ja","Oui","はい","是","예"};var found=labels.Select((text,index)=>(text,index)).Where(x=>yesLabels.Contains(x.text)).ToArray();if(found.Length!=1){BlockHelper("Skip confirmation differs; choose manually.");return;}choice=found[0].index;}helperReplaying=true;try{dialog->FireCallbackInt(choice);}finally{helperReplaying=false;}CompleteHelperAction(now);return;}
            return;
        }
        if(a.Kind=="talk"){
            var talk=HelperTalk();
            if(talk.Signature==a.Signature&&talk.Text==a.Text){helperReplaying=true;try{if(AdvanceTravelTalk(HelperTargetSignal(a)))CompleteHelperAction(now);}finally{helperReplaying=false;}return;}
            if(now-helperActionStarted>TimeSpan.FromSeconds(5))BlockHelper("Dialogue differs from the leader's recorded line; no response selected.");return;
        }
        if(a.Kind=="choice"){
            var menu=(AtkUnitBase*)GardenGui.GetAddonByName(a.Addon).Address;var choices=HelperChoices(menu,a.Addon);
            if(choices.Count==0){if(now-helperActionStarted>TimeSpan.FromSeconds(8))BlockHelper("The recorded response menu did not appear.");return;}
            RecordFollowTravel("Helper response menu",new {npc=a.Npc.Name,expected=a.Text,a.QuestId,choices});
            var index=HelperPolicy.Match(choices,a.Text);
            var isQuest=a.QuestId!=0&&HelperQuestIdForName(a.Text)==a.QuestId;
            if(isQuest&&index<0){SkipHelperConversation(a,"The same quest is unavailable in your NPC menu; waiting for the next interaction.");return;}
            if(index<0||!isQuest&&HelperPolicy.Signature(choices.OrderBy(x=>x,StringComparer.Ordinal))!=a.Signature){BlockHelper("The NPC's responses differ from the leader's; choose manually.");return;}
            helperReplaying=true;try{SelectTravelChoice(menu,index);}finally{helperReplaying=false;}CompleteHelperAction(now);
        }
    }
    private FollowPortalSignal HelperTargetSignal(HelperAction a)=>new(a.Id,a.Name,a.World,a.Npc.World,"",a.Npc.Territory,a.Npc.Map,a.Npc.BaseId,0,a.Npc.Position.X,a.Npc.Position.Y,a.Npc.Position.Z,a.SentAt,"",SourceKind:"EventNpc");
    private void CompleteHelperAction(DateTimeOffset now){helperIncoming.Dequeue();helperWorkingId="";helperNextAction=now.AddMilliseconds(450);helperQuestStatus="In dialogue — following the leader's choices.";nextHelperStatus=default;}
}
