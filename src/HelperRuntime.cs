using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly HelperRelay helperRelay=new();
    private readonly HelperPermission helperPermission=new();
    private HelperFollower[] helperFollowers=[];
    private Task<(HelperReply? Reply,string Error)>? helperStatusTask,helperLeaderTask,helperControlTask,helperSendTask;
    private DateTimeOffset nextHelperStatus,nextHelperLeader;
    private string helperStatusSession="",helperLeaderIdentity="",helperError="";
    private long helperCursor,helperStatusGeneration,helperTravelAfter,helperTravelCutoff;
    private string helperActorIdentity="",helperSendIdentity="",helperControlIdentity="";
    private string helperStatusKey="";
    private DateTimeOffset nextHelperTick;
    private readonly Queue<HelperAction> helperOutgoing=new(),helperIncoming=new();
    private readonly Queue<(HelperFollower Follower,string Command,string Identity)> helperControls=new();
    private readonly HashSet<string> helperSeen=new(),helperOpened=new();
    private bool helperWindowOpen;
    private string helperBlocked="";
    private bool HelperPaused=>helperPermission.Active&&helperPermission.Paused;
    private bool HelperTravelBusy=>travelQueue.Count>0&&!HoldHelperTravel||travelAwaitingArrival!=null||followApproach!=null||pendingTransport!=null||pendingWard!=null||pendingAethernet!=null||receivedPortal!=null||pendingDutyLeave!=null||lifestreamTravelOwned;
    private bool HelperQuestBusy=>!HelperTravelBusy&&helperPermission.Active&&helperPermission.Quest&&!helperPermission.QuestPaused&&(helperReservedConversation.Length>0||helperIncoming.Count>0||helperBlocked.Length>0||helperNpcActive!=null&&QuestConversationVisible());
    private bool SharingQuest=>config.EnableFollowThem&&config.FollowThem.ShareQuestActions&&config.PairingKey.Length==64&&Player.IsLoaded&&helperFollowers.Any(x=>HelperPolicy.Audience(x,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    private void ClearHelperActions(){helperConversations.Clear();ClearHelperReservation();helperIncoming.Clear();helperBlocked="";helperNpcActive=null;CancelHelperApproach();}
    private void EndHelperSession(){CancelHelperFateApproach();helperPendingFate=null;helperPermission.Stop();ClearHelperActions();helperCursor=0;helperTravelAfter=0;helperTravelCutoff=0;helperSeen.Clear();helperSkippedConversations.Clear();nextHelperStatus=default;}
    private void ApplyHelperControl(string command)
    {
        if(command=="stop"){StopFollowThem("Ended by the followed character. Only you can start again.");return;}
        var before=HelperPaused;helperPermission.Control(command);
        if(HelperPaused&&!before){CancelHelperFateApproach();helperPendingFate=null;ClearHelperActions();DiscardHelperTravel();RequestFollowMovementStop();followSession.Pause();}
        else if(before&&!HelperPaused)ResumeAfterConfirmedTravel();
    }
    private void UpdateHelper(DateTimeOffset now)
    {
        if(now<nextHelperTick)return;nextHelperTick=now.AddMilliseconds(100);
        if(helperPairingIdentity!=config.PairingKey){helperPairingIdentity=config.PairingKey;helperFateObserved=false;CancelHelperFateApproach();helperPendingFate=null;helperFollowers=[];helperOutgoing.Clear();ClearHelperActions();helperOpened.Clear();nextHelperLeader=default;}
        if(helperPermission.Active&&Player.IsLoaded&&helperFollowerName.Length>0&&(Player.CharacterName!=helperFollowerName||Player.HomeWorld.RowId!=helperFollowerWorld)){StopFollowThem("Character changed; Helper permission ended.");}
        UpdateHelperActor();
        if(helperControlTask?.IsCompleted==true){var r=helperControlTask.GetAwaiter().GetResult();helperControlTask=null;if(helperControlIdentity==helperActorIdentity){helperError=r.Error;CompleteHelperBring(r.Reply,r.Error);}nextHelperLeader=default;}
        if(helperSendTask?.IsCompleted==true){var r=helperSendTask.GetAwaiter().GetResult();helperSendTask=null;if(helperSendIdentity==helperActorIdentity&&r.Error.Length>0)helperError=r.Error;}
        if(helperStatusTask?.IsCompleted==true){
            var r=helperStatusTask.GetAwaiter().GetResult();helperStatusTask=null;
            if(helperStatusSession==followLeaseId&&helperStatusGeneration==followArmedAt&&helperStatusKey==config.PairingKey&&followSession.Armed&&helperPermission.Active){
                helperError=r.Error;
                if(r.Reply is {} reply){
                    if(reply.TravelAfter>helperTravelCutoff){DiscardHelperTravel();helperTravelCutoff=reply.TravelAfter;}
                    ApplyHelperControl(reply.Control);
                    var questWasPaused=helperPermission.QuestPaused;helperPermission.SetQuestPause(reply.QuestPaused);
                    if(!questWasPaused&&helperPermission.QuestPaused){ClearHelperActions();RequestFollowMovementStop();ResumeAfterConfirmedTravel();}
                    foreach(var a in reply.Actions??[]){helperCursor=Math.Max(helperCursor,a.Sequence);if(helperSkippedConversations.Contains(a.Npc.Conversation)||!helperPermission.Allows(a.Kind)||!HelperPolicy.Fresh(a,now.ToUnixTimeMilliseconds(),followArmedAt)||a.SentAt<=helperTravelAfter||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,a.Name,a.World)||!helperSeen.Add(a.Id))continue;
                        if(ReceiveHelperConversation(a,now))continue;
                        if(a.Kind=="fateSync"){CancelHelperFateApproach();helperPendingFate=a;helperFateAttempts=0;helperFateFallback=false;helperNextFateAttempt=default;continue;}
                        RecordFollowTravel("Helper standalone NPC action ignored",new {a.Kind,reason="A complete verified quest conversation is required."});
                    }
                }else nextHelperStatus=now.AddSeconds(10);
            }
        }
        if(helperLeaderTask?.IsCompleted==true){
            var r=helperLeaderTask.GetAwaiter().GetResult();helperLeaderTask=null;
            if(helperLeaderIdentity==config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId){
                if(r.Reply is {} reply){SetHelperFollowers(reply.Followers??[]);if(HelperPolicy.HasLeaderRole(helperFollowers)&&followSession.Armed)StopFollowThem("You are being followed; your own follower session ended.");foreach(var f in helperFollowers)if(f.Quest&&f.Control!="stop"&&helperOpened.Add(f.Id))helperWindowOpen=true;}
                else{helperError=r.Error;nextHelperLeader=now.AddSeconds(15);}
            }
        }
        if(!config.EnableFollowThem||config.PairingKey.Length!=64){SetHelperFollowers([]);RefreshHelperLeaderBar();return;}
        if(followSession.Armed&&helperPermission.Active&&followLeaseId.Length>0&&!followLeaseDeleting&&helperStatusTask==null&&now>=nextHelperStatus&&followLogin!=0&&followLeaseIdentity==config.PairingKey+"/"+config.FollowThem.TargetName+"/"+config.FollowThem.HomeWorld+"/"+followArmedAt){
            helperStatusSession=followLeaseId;helperStatusGeneration=followArmedAt;helperStatusKey=config.PairingKey;nextHelperStatus=now.AddSeconds(2);
            var status=HelperPaused?("Paused by leader"):helperBlocked.Length>0?"Blocked — "+helperBlocked:HelperQuestBusy?helperQuestStatus:followStatus;
            helperStatusTask=helperRelay.Call(config.PairingKey,"op=status&session="+followLeaseId,new {name=helperFollowerName,world=helperFollowerWorld,status=(helperPermission.QuestPaused?"Quest Helper paused; ":"")+status[..Math.Min(status.Length,460)],quest=helperPermission.Quest,skip=helperPermission.Skip,paused=false,after=helperCursor});
        }
        if(Player.IsLoaded&&Player.HomeWorld.RowId>0&&!string.IsNullOrWhiteSpace(Player.CharacterName)&&helperLeaderTask==null&&now>=nextHelperLeader){
            nextHelperLeader=now.AddSeconds(helperFollowers.Length>0?3:5);helperLeaderIdentity=config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId;
            helperLeaderTask=helperRelay.Call(config.PairingKey,"op=leader&name="+Uri.EscapeDataString(Player.CharacterName)+"&world="+Player.HomeWorld.RowId);
        }
        if(helperControlTask==null&&helperControls.Count==0&&helperSendTask==null&&helperOutgoing.TryDequeue(out var outgoing)&&now.ToUnixTimeMilliseconds()-outgoing.SentAt<10000&&SharingQuest&&outgoing.Name==Player.CharacterName&&outgoing.World==Player.HomeWorld.RowId){
            helperSendIdentity=helperActorIdentity;helperSendTask=helperRelay.Call(config.PairingKey,"op=action",outgoing);}
        if(helperControlTask==null&&helperControls.TryDequeue(out var control)&&Player.IsLoaded&&control.Identity==config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId)
            {helperControlIdentity=helperActorIdentity;helperControlTask=helperRelay.Call(config.PairingKey,"op=control&session="+control.Follower.Id,new {name=Player.CharacterName,world=Player.HomeWorld.RowId,command=control.Command});}
        ObserveHelperDialogue();
        if(helperBlocked.Length>0&&!QuestConversationVisible()){
            if(helperNpcActive is {} failed)helperSkippedConversations.Add(failed.Conversation);
            ClearHelperActions();ResumeAfterConfirmedTravel();nextHelperStatus=default;
            RecordFollowTravel("Helper recovered",new {reason="NPC window closed; normal following resumed."});
        }
        UpdateHelperFateSync(now);ObserveHelperQuestAcceptance(now);ObserveHelperReplayCheck();ObserveHelperRecording(now);
        if(helperReservedConversation.Length>0&&now>=helperReservationUntil)FinishHelperConversation("NPC recording or playback timed out; waiting for a new interaction.");
        UpdateQuestHelper(now);RefreshHelperLeaderBar();
    }
    private string helperPairingIdentity="";
    private string helperFollowerName="";
    private uint helperFollowerWorld;
    private void StartHelperSession(){EndHelperSession();helperPermission.Start(config.FollowThem.QuestHelper,config.FollowThem.SkipLeaderCutscenes);helperFollowerName=Player.CharacterName;helperFollowerWorld=Player.HomeWorld.RowId;}
    private void SendHelperControl(HelperFollower f,string command)
    {
        if(!Player.IsLoaded||helperControls.Count>=32)return;
        if(command is "pause" or "questPause" or "stop")RemoveHelperRecordingAudience(f.Id);
        helperControls.Enqueue((f,command,config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId));
    }
    private HelperFollower[] ActiveHelperFollowers()=>helperFollowers.Where(x=>x.Control!="stop").ToArray();
    private void RefreshHelperLeaderBar()=>RefreshFollowBar();
}
