using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly List<HelperAction> helperRecorded=new();
    private readonly Queue<HelperAction> helperConversations=new();
    private bool helperRecording,helperRecordingFailed,helperRecordingAnnounced;
    private bool HelperShopVisible()=>new[]{"Shop","ShopExchangeItem","ShopExchangeCurrency","InclusionShop","FreeCompanyCreditShop","CollectablesShop"}.Any(VisibleFollowAddon);
    private void AnnounceHelperQuestRecording()
    {
        if(helperRecordingAnnounced||helperCaptureNpc is not {} npc||HelperShopVisible())return;
        var quest=HelperQuestScope.Evidence(helperRecorded);if(quest==0)return;
        helperRecordingAnnounced=true;QueueHelperEnvelope("recording",npc,questId:quest);
    }
    private string[] helperRecordAudience=[];
    private DateTimeOffset helperRecordQuiet;
    private string helperReservedConversation="";
    private DateTimeOffset helperReservationUntil,helperPlaybackUntil;
    private readonly Dictionary<string,int> helperStepDelays=new();
    private bool helperActionSubmitted;
    private bool HoldHelperTravel=>helperPermission.Active&&!HelperPaused&&!helperPermission.QuestPaused&&helperBlocked.Length==0&&helperReservedConversation.Length>0&&DateTimeOffset.UtcNow<helperReservationUntil;
    private void ResetHelperRecording()
    {
        if(helperRecordingAnnounced&&helperCaptureNpc is {} npc)QueueHelperEnvelope("cancelConversation",npc);
        helperResultQuest=0;helperResultClosedAt=default;helperCaptureNpc=null;helperDialogue.Reset();helperRecording=false;helperRecordingFailed=false;helperRecordingAnnounced=false;helperRecorded.Clear();helperRecordAudience=[];helperRecordQuiet=default;helperAcceptIntent=false;helperOfferedQuest=0;
    }
    private void QueueHelperEnvelope(string kind,HelperNpc npc,HelperAction[]? steps=null,uint questId=0)
    {
        var now=DateTimeOffset.UtcNow;
        if(helperRecordAudience.Length==0||helperOutgoing.Count>=32)return;
        helperOutgoing.Enqueue(new(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,kind,now.ToUnixTimeMilliseconds(),npc,Sessions:helperRecordAudience,Steps:steps,QuestId:questId));
    }
    private unsafe void ObserveHelperRecording(DateTimeOffset now)
    {
        if(!helperRecording)return;
        if(HelperShopVisible()){ResetHelperRecording();return;}
        if(!SharingQuest||helperCaptureNpc is not {} npc||now-helperCaptureAt>TimeSpan.FromMinutes(10)||helperRecordingFailed){ResetHelperRecording();return;}
        helperRecordAudience=helperRecordAudience.Where(id=>helperFollowers.Any(f=>f.Id==id&&HelperPolicy.Audience(f,now.ToUnixTimeMilliseconds()))).ToArray();
        if(helperRecordAudience.Length==0){ResetHelperRecording();return;}
        if(QuestConversationVisible()||helperAcceptIntent||HelperResultPending||Conditions[ConditionFlag.OccupiedInQuestEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78]||Conditions[ConditionFlag.OccupiedInCutSceneEvent]){helperRecordQuiet=default;return;}
        // A brief window replacement is not the end of a conversation.
        var evt=EventFramework.Instance();
        if(evt!=null&&evt->EventState1.EventId.Id!=0&&helperRecorded.Count<2)return;
        if(helperRecordQuiet==default){helperRecordQuiet=now;return;}
        if(now-helperRecordQuiet<TimeSpan.FromSeconds(2))return;
        CommitHelperRecording(npc);
    }
    private void CommitHelperRecording(HelperNpc npc)
    {
        if(HelperResultPending)return;
        FlushHelperTalk();
        if(HelperShopVisible()||HelperQuestScope.Evidence(helperRecorded)==0){RecordFollowTravel("Helper interaction excluded",new {npc=npc.Name,reason=HelperShopVisible()?"Shop":"No verified quest scene or offer"});ResetHelperRecording();return;}
        if(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(helperRecorded).Length>230000){helperError="NPC recording is too large; nothing replayed.";ResetHelperRecording();return;}
        if(helperRecorded.Count>1){QueueHelperEnvelope("conversation",npc,helperRecorded.ToArray());RecordFollowTravel("Helper conversation committed",new {npc=npc.Name,steps=helperRecorded.Count});}
        else QueueHelperEnvelope("cancelConversation",npc);
        helperRecording=false;helperRecordingAnnounced=false;helperRecorded.Clear();helperRecordAudience=[];helperCaptureNpc=null;helperDialogue.Reset();
    }
    private void CompleteHelperRecordingBeforeNextNpc()
    {
        ObserveHelperQuestResult(DateTimeOffset.UtcNow);
        // The next genuine NPC click is an interaction boundary, even before the quiet debounce ends.
        if(HelperConversationPolicy.CommitAtNextNpc(helperRecording,helperRecordingFailed,helperAcceptIntent||HelperResultPending,QuestConversationVisible())&&helperCaptureNpc is {} previous)CommitHelperRecording(previous);
    }
    private bool ReceiveHelperConversation(HelperAction a,DateTimeOffset now)
    {
        if(a.Kind=="recording"){
            if(a.QuestId==0)return true;
            helperObservedQuest=a.QuestId;nextHelperStatus=default;
            // Preserve an already playing conversation rather than replacing it silently.
            if(helperIncoming.Count>0||helperBlocked.Length>0)return true;
            helperReservedConversation=a.Npc.Conversation;helperReservationUntil=now.AddMinutes(10);followSession.Pause();RequestFollowMovementStop();helperQuestStatus="Waiting for the leader to finish the NPC conversation.";return true;
        }
        if(a.Kind=="cancelConversation"){
            if(helperReservedConversation==a.Npc.Conversation)FinishHelperConversation("Leader cancelled the recorded conversation.");return true;
        }
        if(a.Kind!="conversation")return false;
        if(HelperQuestScope.Evidence(a.Steps??[])==0){
            if(helperReservedConversation==a.Npc.Conversation)FinishHelperConversation("Non-quest NPC interaction ignored.");
            RecordFollowTravel("Helper non-quest ignored",new {npc=a.Npc.Name});return true;
        }
        if(!HelperConversationPolicy.ValidSteps(a)){helperError="Incomplete NPC recording was rejected.";return true;}
        helperObservedQuest=HelperQuestScope.Evidence(a.Steps!);nextHelperStatus=default;
        if(helperIncoming.Count>0||helperBlocked.Length>0){
            if(helperConversations.Count>=4){helperError="Four NPC conversations are queued; wait for the follower.";return true;}
            helperConversations.Enqueue(a);RecordFollowTravel("Helper conversation queued",new {npc=a.Npc.Name,steps=a.Steps!.Length});return true;
        }
        helperReservedConversation=a.Npc.Conversation;helperReservationUntil=now.AddMinutes(10);helperPlaybackUntil=helperReservationUntil;
        helperBlocked="";helperLastIssue="";helperStepDelays.Clear();long previous=a.Steps![0].SentAt;
        foreach(var step in a.Steps){
            if(step.Kind=="skip"&&!helperPermission.Skip){FinishHelperConversation("This conversation includes a cutscene skip you have not enabled.");return true;}
            helperStepDelays[step.Id]=step.Kind=="interact"?0:HelperConversationPolicy.Delay(previous,step.SentAt);previous=step.SentAt;
            helperIncoming.Enqueue(step with {SentAt=a.SentAt});
        }
        helperNextAction=now;RecordFollowTravel("Helper conversation received",new {npc=a.Npc.Name,steps=a.Steps.Length});return true;
    }
    private void FinishHelperConversation(string reason)
    {
        RecordFollowTravel("Helper conversation finished",new {reason});
        var pending=helperConversations.ToArray();
        ClearHelperActions();helperQuestStatus=reason;ResumeAfterConfirmedTravel();nextHelperStatus=default;
        foreach(var next in pending)ReceiveHelperConversation(next,DateTimeOffset.UtcNow);
    }
    private void RemoveHelperRecordingAudience(string id)
    {
        helperRecordAudience=helperRecordAudience.Where(x=>x!=id).ToArray();
        var pending=helperOutgoing.Select(a=>a with {Sessions=(a.Sessions??[]).Where(x=>x!=id).ToArray()}).Where(a=>a.Sessions!.Length>0).ToArray();
        helperOutgoing.Clear();foreach(var a in pending)helperOutgoing.Enqueue(a);
    }
    private void ClearHelperReservation(){helperReservedConversation="";helperReservationUntil=default;helperPlaybackUntil=default;helperStepDelays.Clear();helperActionSubmitted=false;helperResultConfirmationId="";}
}

