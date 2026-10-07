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
    private long helperCursor,helperStatusGeneration,helperTravelAfter;
    private string helperStatusKey="";
    private DateTimeOffset nextHelperTick;
    private readonly Queue<HelperAction> helperOutgoing=new(),helperIncoming=new();
    private readonly Queue<(HelperFollower Follower,string Command,string Identity)> helperControls=new();
    private readonly HashSet<string> helperSeen=new(),helperOpened=new();
    private IDtrBarEntry? helperLeaderBar;
    private bool helperWindowOpen;
    private string helperBlocked="";
    private bool HelperPaused=>helperPermission.Active&&helperPermission.Paused;
    private bool HelperTravelBusy=>travelQueue.Count>0||travelAwaitingArrival!=null||followApproach!=null||pendingTransport!=null||pendingWard!=null||pendingAethernet!=null||receivedPortal!=null||pendingDutyLeave!=null||lifestreamTravelOwned;
    private bool HelperQuestBusy=>!HelperTravelBusy&&helperPermission.Active&&helperPermission.Quest&&(helperIncoming.Count>0||helperBlocked.Length>0||helperNpcActive!=null&&QuestConversationVisible());
    private bool SharingQuest=>config.EnableFollowThem&&config.FollowThem.ShareQuestActions&&config.PairingKey.Length==64&&Player.IsLoaded&&helperFollowers.Any(x=>HelperPolicy.Audience(x,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    private void ClearHelperActions(){helperIncoming.Clear();helperBlocked="";helperNpcActive=null;CancelHelperApproach();}
    private void EndHelperSession(){helperPermission.Stop();ClearHelperActions();helperCursor=0;helperTravelAfter=0;helperSeen.Clear();nextHelperStatus=default;}
    private void ApplyHelperControl(string command)
    {
        if(command=="stop"){StopFollowThem("Ended by the followed character. Only you can start again.");return;}
        var before=HelperPaused;helperPermission.Control(command);
        if(HelperPaused&&!before){ClearHelperActions();CancelFollowApproach();CancelLifestreamTravel();RequestFollowMovementStop();followSession.Pause();}
        else if(before&&!HelperPaused)ResumeAfterConfirmedTravel();
    }
    private void UpdateHelper(DateTimeOffset now)
    {
        if(now<nextHelperTick)return;nextHelperTick=now.AddMilliseconds(100);
        if(helperPairingIdentity!=config.PairingKey){helperPairingIdentity=config.PairingKey;helperFollowers=[];helperOutgoing.Clear();ClearHelperActions();helperOpened.Clear();nextHelperLeader=default;}
        if(helperPermission.Active&&Player.IsLoaded&&helperFollowerName.Length>0&&(Player.CharacterName!=helperFollowerName||Player.HomeWorld.RowId!=helperFollowerWorld)){StopFollowThem("Character changed; Helper permission ended.");}
        if(helperControlTask?.IsCompleted==true){var r=helperControlTask.GetAwaiter().GetResult();helperControlTask=null;helperError=r.Error;nextHelperLeader=default;}
        if(helperSendTask?.IsCompleted==true){var r=helperSendTask.GetAwaiter().GetResult();helperSendTask=null;if(r.Error.Length>0)helperError=r.Error;}
        if(helperStatusTask?.IsCompleted==true){
            var r=helperStatusTask.GetAwaiter().GetResult();helperStatusTask=null;
            if(helperStatusSession==followLeaseId&&helperStatusGeneration==followArmedAt&&helperStatusKey==config.PairingKey&&followSession.Armed&&helperPermission.Active){
                helperError=r.Error;
                if(r.Reply is {} reply){
                    ApplyHelperControl(reply.Control);
                    foreach(var a in reply.Actions??[]){helperCursor=Math.Max(helperCursor,a.Sequence);if(!helperPermission.Allows(a.Kind)||!HelperPolicy.Fresh(a,now.ToUnixTimeMilliseconds(),followArmedAt)||a.SentAt<=helperTravelAfter||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,a.Name,a.World)||!helperSeen.Add(a.Id))continue;
                        if(helperIncoming.Count>=32){helperBlocked="Dialogue queue is full; pause and resume Quest Helper to clear it.";break;}
                        helperIncoming.Enqueue(a);
                    }
                }else nextHelperStatus=now.AddSeconds(10);
            }
        }
        if(helperLeaderTask?.IsCompleted==true){
            var r=helperLeaderTask.GetAwaiter().GetResult();helperLeaderTask=null;
            if(helperLeaderIdentity==config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId){
                if(r.Reply is {} reply){helperFollowers=reply.Followers??[];foreach(var f in helperFollowers)if(f.Quest&&f.Control!="stop"&&helperOpened.Add(f.Id))helperWindowOpen=true;}
                else{helperError=r.Error;helperFollowers=[];nextHelperLeader=now.AddSeconds(15);}
            }
        }
        if(!config.EnableFollowThem||config.PairingKey.Length!=64){helperFollowers=[];RefreshHelperLeaderBar();return;}
        if(followSession.Armed&&helperPermission.Active&&followLeaseId.Length>0&&!followLeaseDeleting&&helperStatusTask==null&&now>=nextHelperStatus&&followLogin!=0&&followLeaseIdentity==config.PairingKey+"/"+config.FollowThem.TargetName+"/"+config.FollowThem.HomeWorld+"/"+followArmedAt){
            helperStatusSession=followLeaseId;helperStatusGeneration=followArmedAt;helperStatusKey=config.PairingKey;nextHelperStatus=now.AddSeconds(2);
            var status=HelperPaused?("Paused by leader"):helperBlocked.Length>0?"Blocked — "+helperBlocked:HelperQuestBusy?helperQuestStatus:followStatus;
            helperStatusTask=helperRelay.Call(config.PairingKey,"op=status&session="+followLeaseId,new {name=helperFollowerName,world=helperFollowerWorld,status=status[..Math.Min(status.Length,500)],quest=helperPermission.Quest,skip=helperPermission.Skip,paused=false,after=helperCursor});
        }
        if(Player.IsLoaded&&helperLeaderTask==null&&now>=nextHelperLeader){
            nextHelperLeader=now.AddSeconds(helperFollowers.Length>0?3:5);helperLeaderIdentity=config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId;
            helperLeaderTask=helperRelay.Call(config.PairingKey,"op=leader&name="+Uri.EscapeDataString(Player.CharacterName)+"&world="+Player.HomeWorld.RowId);
        }
        if(helperSendTask==null&&helperOutgoing.TryDequeue(out var outgoing)&&now.ToUnixTimeMilliseconds()-outgoing.SentAt<10000&&SharingQuest)
            helperSendTask=helperRelay.Call(config.PairingKey,"op=action",outgoing);
        if(helperControlTask==null&&helperControls.TryDequeue(out var control)&&Player.IsLoaded&&control.Identity==config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId)
            helperControlTask=helperRelay.Call(config.PairingKey,"op=control&session="+control.Follower.Id,new {name=Player.CharacterName,world=Player.HomeWorld.RowId,command=control.Command});
        UpdateQuestHelper(now);RefreshHelperLeaderBar();
    }
    private string helperPairingIdentity="";
    private string helperFollowerName="";
    private uint helperFollowerWorld;
    private void StartHelperSession(){EndHelperSession();helperPermission.Start(config.FollowThem.QuestHelper,config.FollowThem.SkipLeaderCutscenes);helperFollowerName=Player.CharacterName;helperFollowerWorld=Player.HomeWorld.RowId;}
    private void SendHelperControl(HelperFollower f,string command)
    {
        if(!Player.IsLoaded||helperControls.Count>=32)return;
        helperControls.Enqueue((f,command,config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId));
    }
    private void RefreshHelperLeaderBar()
    {
        var active=helperFollowers.Where(x=>x.Control!="stop"&&DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-x.Updated<15000).ToArray();
        if(!config.EnableFollowThem||active.Length==0){helperLeaderBar?.Remove();helperLeaderBar=null;return;}
        helperLeaderBar??=QuickLootBar.Get("Equinox Helper Leader");helperLeaderBar.Shown=true;
        helperLeaderBar.Text=new SeStringBuilder().AddText("FOLLOWED by "+string.Join(", ",active.Select(x=>x.Name))+(active.All(x=>x.Control=="pause")?" · PAUSED":"")).Build();
        helperLeaderBar.Tooltip=new SeStringBuilder().AddText("Left-click to pause/resume. Right-click for Helper Controls. Follower Stop ends permission; only the follower can start again.").Build();
        helperLeaderBar.OnClick=e=>{if(e.ClickType==MouseClickType.Right)helperWindowOpen=true;else if(e.ClickType==MouseClickType.Left){helperWindowOpen=true;var command=active.All(f=>f.Control=="pause")?"resume":"pause";foreach(var f in active)SendHelperControl(f,command);}};
    }
}
