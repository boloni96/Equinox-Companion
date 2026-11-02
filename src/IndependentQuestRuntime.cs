using System.Numerics;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<IndependentQuestBook>? independentBookTask;
    private IndependentQuestBook? independentBook;
    private IndependentQuestBook.Route? independentRoute;
    private int independentSequence=-1,independentStep;
    private uint independentQuestId,independentNpc;
    private ulong independentQuestCharacter;
    private bool independentStepSent,independentScene,independentSceneSeen,independentDeferred,independentTravelSent,independentAethernetSent;
    private DateTimeOffset independentQuestNext,independentStepSince,independentActionAt;
    private string independentQuestRun="",independentQuestSceneIdentity="";
    private string independentQuestBookError="",independentWorkBefore="";
    private bool QuestProviderRunning()=>independentQuestId!=0;
    private void InitializeIndependentQuests(string? directory)
    {
        independentBookTask=Task.Run(()=>{using var stream=File.OpenRead(Path.Combine(directory??"","quest-routes.zip"));return IndependentQuestBook.Load(stream);});
    }
    private unsafe bool StartIndependentQuest(uint id)
    {
        try{
            if(independentBook==null)throw new InvalidOperationException(independentQuestBookError.Length>0?independentQuestBookError:"Quest data is still loading.");
            if(!Player.IsLoaded||QuestManager.Instance()==null)throw new InvalidOperationException("Log in first.");
            if(independentQuestId!=0||nativeEventActive||QuestToolsOtherOperation()||questRecorderActive)throw new InvalidOperationException("Stop the other helper first.");
            if(QuestConversationVisible()||VisibleFollowAddon("SelectYesno")||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.InDutyQueue])throw new InvalidOperationException("Finish the open conversation or duty first.");
            var routes=independentBook.Find(id.ToString());if(routes.Length!=1)throw new InvalidOperationException("Quest route missing or ambiguous.");
            var route=routes[0];if(route.Definition.TryGetProperty("Disabled",out var disabled)&&disabled.ValueKind==JsonValueKind.True)throw new InvalidOperationException("Upstream marks this route disabled.");
            foreach(var property in route.Definition.EnumerateObject())
                if(property.Name is not ("$schema" or "$" or "Author" or "LastChecked" or "Comment" or "Disabled" or "Interruptible" or "QuestSequence")&&IndependentStepPolicy.Meaningful(property.Value))
                    throw new InvalidOperationException("Unknown route condition: "+property.Name);
            ResetQuestCutscene();independentQuestSceneIdentity="";
            independentRoute=route;independentQuestId=id;independentQuestCharacter=Player.ContentId;independentSequence=-1;independentDeferred=false;independentQuestNext=default;
            independentQuestRun=Guid.NewGuid().ToString("N");TraceKeyboundRun(independentQuestRun,"quest-started",$"Quest {id}","companion-quest",false);
            StopIndependentWalk(resetFailure:true);questToolsStatus="Companion starting: "+QuestTitle(id);return true;
        }catch(Exception e){questToolsStatus="Not started: "+e.Message;return false;}
    }
    private void StopIndependentQuest(string reason,bool deferred=false)
    {
        if(independentQuestId==0)return;
        TraceKeyboundRun(independentQuestRun,"quest-stopped",$"Quest {independentQuestId}, sequence {independentSequence}, step {independentStep}: {reason}","companion-quest",false);
        ResetQuestCutscene();independentQuestSceneIdentity="";
        independentQuestId=0;independentRoute=null;independentScene=false;independentDeferred=deferred;
        StopIndependentWalk(reason);CancelIndependentQuestTravel();questToolsStatus=reason;
    }
    private void ResetIndependentStep(DateTimeOffset now,bool preserveScene=false)
    {
        // Acceptance may advance the sequence before its dialogue/cutscene ends.
        var owned=preserveScene&&independentScene;
        var sceneIdentity=independentQuestSceneIdentity;var sceneNpc=independentNpc;var actionAt=independentActionAt;
        if(!owned){ResetQuestCutscene();independentQuestSceneIdentity="";}
        StopIndependentWalk();independentStepSent=independentScene=independentSceneSeen=false;
        independentTravelSent=independentAethernetSent=false;independentNpc=0;independentStepSince=now;independentActionAt=default;independentWorkBefore="";
        if(owned){independentScene=independentSceneSeen=true;independentQuestSceneIdentity=sceneIdentity;independentNpc=sceneNpc;independentActionAt=actionAt;}
    }
    private unsafe byte[] IndependentWork()
    {
        var qm=QuestManager.Instance();var work=qm==null?null:qm->GetQuestById((ushort)independentQuestId);
        return work==null?new byte[6]:work->Variables.ToArray();
    }
    private void ObserveIndependentQuest(uint eventId,uint npc,short scene)
    {
        if(independentQuestId==0)return;
        independentScene=IndependentDialoguePolicy.OwnedScene(independentQuestId,eventId,npc,independentNpc,independentScene,
            DateTimeOffset.UtcNow-independentActionAt<=TimeSpan.FromSeconds(30));
        independentQuestSceneIdentity=independentScene?eventId+":"+scene:"";
        independentSceneSeen|=independentScene;
    }
    private void NextIndependentStep(DateTimeOffset now){independentStep++;ResetIndependentStep(now);}
    private unsafe void UpdateIndependentQuest(DateTimeOffset now)
    {
        if(independentBookTask?.IsCompleted==true){
            if(independentBookTask.IsCompletedSuccessfully)independentBook=independentBookTask.Result;
            else{independentQuestBookError="Quest data could not load: "+independentBookTask.Exception?.GetBaseException().Message;}
            independentBookTask=null;
        }
        if(independentQuestId==0||now<independentQuestNext)return;independentQuestNext=now.AddMilliseconds(250);
        try{
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
            if(!Player.IsLoaded||Player.ContentId!=independentQuestCharacter)throw new InvalidOperationException("Character changed or logged out.");
            if(FollowMovementKeysHeld())throw new InvalidOperationException("Stopped by your movement.");
            if(QuestToolsOtherOperation()||questRecorderActive)throw new InvalidOperationException("Another helper started.");
            if(Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.InDutyQueue]){StopIndependentQuest("Duty left for manual play.",true);return;}
            if(QuestManager.IsQuestComplete(independentQuestId)){StopIndependentQuest("Quest completion confirmed.");return;}
            var sequence=QuestManager.GetQuestSequence(independentQuestId);
            if(sequence!=independentSequence){independentSequence=sequence;independentStep=0;ResetIndependentStep(now,HelperInCutscene()||QuestConversationVisible());}
            var sequences=independentRoute!.Definition.GetProperty("QuestSequence").EnumerateArray().Where(s=>IndependentStepPolicy.Number(s,"Sequence",999)==sequence).ToArray();
            if(sequences.Length!=1){StopIndependentQuest("No unique route for current quest sequence "+sequence,true);return;}
            if(!sequences[0].TryGetProperty("Steps",out var steps)){StopIndependentQuest("Sequence has no executable steps; continue manually.",true);return;}
            if(independentStep>=steps.GetArrayLength()){
                questToolsStatus="Waiting for the quest journal to advance";
                if(now-independentStepSince>TimeSpan.FromSeconds(30))StopIndependentQuest("Quest journal did not advance after the last step.",true);return;
            }
            var step=steps[independentStep];var kind=IndependentStepPolicy.Text(step,"InteractionType");
            if(step.TryGetProperty("Disabled",out var disabled)&&disabled.ValueKind==JsonValueKind.True){NextIndependentStep(now);return;}
            var reason=IndependentStepPolicy.Review(step);if(reason.Length>0){StopIndependentQuest(reason,true);return;}
            if(step.TryGetProperty("CompletionQuestVariablesFlags",out var flags)&&IndependentStepPolicy.CompletedFlags(flags,IndependentWork())){NextIndependentStep(now);return;}
            if(now-independentStepSince>TimeSpan.FromMinutes(4))throw new InvalidOperationException("Quest step timed out: "+questToolsStatus);
            questToolsStatus=$"{QuestTitle(independentQuestId)} · sequence {sequence}, step {independentStep+1}/{steps.GetArrayLength()} · {kind}";
            if(UpdateOwnedQuestCutscene(now,false,()=>IndependentQuestDialogue(step,kind,now)))return;
            if(IndependentQuestDialogue(step,kind,now))return;
            if(Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]||HelperInCutscene()||Conditions[ConditionFlag.Casting])return;
            var territory=IndependentStepPolicy.Number(step,"TerritoryId");
            if(independentStepSent){
                var changed=Convert.ToHexString(IndependentWork())!=independentWorkBefore;
                var targetTerritory=IndependentStepPolicy.Number(step,"TargetTerritoryId");
                if(targetTerritory!=0&&Client.TerritoryType==targetTerritory||changed||kind=="Interact"&&independentSceneSeen&&now-independentActionAt>TimeSpan.FromSeconds(2))NextIndependentStep(now);
                else if(kind=="AcceptQuest"&&QuestManager.Instance()->IsQuestAccepted(independentQuestId))NextIndependentStep(now);
                else if(now-independentActionAt>TimeSpan.FromSeconds(30))StopIndependentQuest("Action was sent once but progress was not confirmed. "+kind,true);
                return;
            }
            if(!IndependentQuestTravel(step,territory,now))return;
            if(step.TryGetProperty("DelaySecondsAtStart",out var delay)&&now-independentStepSince<TimeSpan.FromSeconds(Math.Clamp(delay.GetDouble(),0,120)))return;
            var id=IndependentStepPolicy.Number(step,"DataId");var npc=id==0?null:Objects.FirstOrDefault(o=>o.BaseId==id&&o.IsTargetable);
            Vector3? position=npc?.Position;
            if(position==null&&step.TryGetProperty("Position",out var p))position=new(p.GetProperty("X").GetSingle(),p.GetProperty("Y").GetSingle(),p.GetProperty("Z").GetSingle());
            var range=kind=="WalkTo"?.35f:2.5f;
            if(step.TryGetProperty("StopDistance",out var dist))range=Math.Clamp(dist.GetSingle(),.25f,5);
            if(position is {} goal&&!IndependentWalk(goal,range,now)){questToolsStatus+=" · "+independentWalkStatus;return;}
            if(kind is "None" or "WalkTo"){NextIndependentStep(now);return;}
            if(kind=="WaitForNpcAtPosition"){if(npc!=null)NextIndependentStep(now);return;}
            if(kind is "AcceptQuest" or "CompleteQuest" or "Interact"){
                if(npc==null){questToolsStatus+=" · waiting for target NPC";return;}
                var system=TargetSystem.Instance();if(system==null)return;
                TraceKeyboundRun(independentQuestRun,"quest-interaction-requested",$"{kind}: NPC {id}, sequence {sequence}, step {independentStep}","companion-quest",false);
                independentNpc=id;Targets.Target=npc;independentActionAt=now;independentStepSent=true;independentWorkBefore=Convert.ToHexString(IndependentWork());
                helperReplaying=true;try{system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)npc.Address,false);}finally{helperReplaying=false;}
                return;
            }
            if(id!=0&&npc==null){questToolsStatus+=" · waiting for action target";return;}
            if(npc!=null)Targets.Target=npc;
            independentWorkBefore=Convert.ToHexString(IndependentWork());independentActionAt=now;
            TraceKeyboundRun(independentQuestRun,"quest-action-requested",$"{kind}, sequence {sequence}, step {independentStep}","companion-quest",false);
            if(!DispatchIndependentQuestAction(step,kind,npc?.GameObjectId??0xE0000000))throw new InvalidOperationException("Game refused quest action: "+kind);
            independentStepSent=true;
        }catch(Exception e){StopIndependentQuest("Stopped: "+e.Message);Log.Warning(e,"Independent quest stopped");}
    }
}
