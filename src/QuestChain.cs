using System.Net.Http;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool questContinueLine=true,questNextAccepted=true,questChainActive,questChainStartedQuest;
    private bool questLeveling,questLevelingIncludeMsq=true;
    private uint questChainCandidate,questChainLast;
    private ulong questChainCharacter;
    private readonly HashSet<uint> questChainVisited=[];
    private readonly Dictionary<uint,string> questChainDeferred=[];
    private readonly Dictionary<uint,int> questChainDeferredSequence=[];
    private readonly Dictionary<uint,QuestRouteReview> questRouteReviews=[];
    private Task<QuestRouteReview>? questRouteRequest;
    private uint questRouteRequestId;
    private DateTimeOffset questChainNext;
    private string questChainStatus="";
    private void DrawQuestChainOptions(){
        ImGui.BeginDisabled(questChainActive);
        if(ImGui.Checkbox("Quest leveling",ref questLeveling)){config.QuestLeveling=questLeveling;SaveConfiguration();}
        if(questLeveling){
            DrawLevelingGoal();
            if(ImGui.Checkbox("Include MSQ",ref questLevelingIncludeMsq)){config.QuestLevelingIncludeMsq=questLevelingIncludeMsq;SaveConfiguration();}
            ImGui.TextWrapped("Finds supported XP quests, including ones not accepted yet. Checks current job, level and prerequisites. Combat, duties, leves and unverified special requirements stay manual.");
        }
        ImGui.BeginDisabled(questLeveling);
        ImGui.Checkbox("Continue quest line",ref questContinueLine);
        ImGui.Checkbox("Then do other accepted quests",ref questNextAccepted);
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        ImGui.TextDisabled("Duty / solo-duty routes stay pending. No AutoDuty required.");
        ImGui.BeginDisabled(nativeEventActive||questChainActive||eventAccountActive||independentQuestId!=0);
        if(ImGui.Button(questLeveling?"Start recommended quest leveling":"Work on accepted quests")){
            ResetDeferredCharacter();questChainVisited.Clear();var id=NextAcceptedQuest();
            if(id!=0)BeginQuestChain(id);else questChainStatus=questLeveling?questLevelingRecommendation:"No indexed accepted quests were found on this character.";
        }
        ImGui.EndDisabled();
        if(questLeveling&&questLevelingRecommendation.Length>0)ImGui.TextWrapped(questLevelingRecommendation);
        if(questChainStatus.Length>0)ImGui.TextWrapped(questChainStatus);
        if(questChainActive&&ImGui.Button("Stop quest routine"))StopQuestChain("Stopped by you.");
        if(questChainDeferred.Count>0&&ImGui.CollapsingHeader("Quests left pending")){
            foreach(var entry in questChainDeferred)ImGui.TextWrapped(QuestTitle(entry.Key)+": "+entry.Value);
            if(!questChainActive&&ImGui.Button("Retry pending quests")){questChainDeferred.Clear();questChainDeferredSequence.Clear();questChainVisited.Clear();var retry=NextAcceptedQuest();if(retry!=0)BeginQuestChain(retry);}
        }
    }
    private void BeginQuestChain(uint id){
        if(nativeEventActive||questChainActive||eventAccountActive||questRecorderActive||QuestToolsOtherOperation()||!Player.IsLoaded){questToolsStatus="Finish the other operation first.";return;}
        if(QuestProviderRunning()){questToolsStatus="Another quest is running.";return;}
        if(independentBook==null){questToolsStatus="Independent quest data is still loading.";return;}
        if(questLeveling&&!CanStartLevelingGoal()){questChainStatus=questLevelingRecommendation;return;}
        if(questLeveling)BeginLevelingProgress();
        ResetDeferredCharacter();questChainActive=true;questChainStartedQuest=false;questChainCharacter=Player.ContentId;
        questChainCandidate=QuestToolsPolicy.ShortId(id);questChainLast=0;questChainVisited.Clear();questChainNext=default;
        questLevelingJob=Objects.LocalPlayer?.ClassJob.RowId??0;
        questChainStatus="Checking the selected quest route before starting.";
    }
    private void StopQuestChain(string reason){
        questChainActive=false;questChainStartedQuest=false;questChainCandidate=0;
        StopIndependentQuest(reason);questChainStatus=reason;
    }
    private Task<QuestRouteReview> ReadQuestRoute(QuestCatalogueRow row){
        var routes=independentBook?.Find(row.Id.ToString())??[];
        return Task.FromResult(routes.Length==1?QuestRoutePolicy.Review(routes[0].Definition.GetRawText()):new QuestRouteReview(false,false,[],"Quest data missing or ambiguous."));
    }
    private unsafe uint QuestChainSuccessor(uint previous){
        var explicitNext=questRouteReviews.GetValueOrDefault(previous)?.Next??[];
        var candidates=explicitNext.Length>0?explicitNext:
            questCatalogue.Where(q=>{
                var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(q.Id+65536);
                return row.HasValue&&row.Value.PreviousQuest.Any(p=>QuestToolsPolicy.ShortId(p.RowId)==previous);
            }).Select(q=>q.Id).ToArray();
        var available=candidates.Where(id=>(!questLeveling||questLevelingIncludeMsq||!questCatalogue.Any(q=>q.Id==id&&q.Group.Contains("/MSQ",StringComparison.OrdinalIgnoreCase)))&&id!=previous&&!questChainVisited.Contains(id)&&!QuestStillDeferred(id)&&!QuestManager.IsQuestComplete(id)&&
            independentBook?.Find(id.ToString()).Length==1).ToArray();
        var accepted=available.Where(id=>QuestManager.Instance()->IsQuestAccepted(id)).ToArray();
        // Do not choose between unaccepted branches on the player's behalf.
        return accepted.Length>0?accepted[0]:available.Length==1?available[0]:0;
    }
    private void ResetDeferredCharacter(){if(questChainCharacter!=Player.ContentId){questChainDeferred.Clear();questChainDeferredSequence.Clear();questChainCharacter=Player.ContentId;}}
    private bool QuestStillDeferred(uint id)=>questChainDeferredSequence.TryGetValue(id,out var sequence)&&sequence==QuestManager.GetQuestSequence(id);
    private unsafe uint NextAcceptedQuest(){
        var qm=QuestManager.Instance();if(qm==null)return 0;
        foreach(ref var quest in qm->NormalQuests){
            var id=(uint)quest.QuestId;
            if(!questLeveling&&id!=0&&!questChainVisited.Contains(id)&&!QuestStillDeferred(id)&&questCatalogue.Any(q=>q.Id==id))return id;
        }
        if(questLeveling)return NextLevelingQuest();
        return 0;
    }
    private unsafe void UpdateQuestChain(DateTimeOffset now){
        if(questRouteRequest?.IsCompleted==true){
            questRouteReviews[questRouteRequestId]=questRouteRequest.GetAwaiter().GetResult();questRouteRequest=null;
        }
        if(!questChainActive||now<questChainNext)return;questChainNext=now.AddSeconds(1);
        try{
            if(!Player.IsLoaded||Player.ContentId!=questChainCharacter){StopQuestChain("Stopped: character changed.");return;}
            if(questLeveling&&Objects.LocalPlayer?.ClassJob.RowId!=questLevelingJob){StopQuestChain("Job changed; restart leveling to select quests for this job.");return;}
            if(QuestToolsOtherOperation()||eventAccountActive||questRecorderActive){StopQuestChain("Stopped: another helper started.");return;}
            if(Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]||Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.InDutyQueue]){StopQuestChain("Duty / queue detected; continue it manually.");return;}
            if(independentQuestId!=0){questChainStatus=questToolsStatus;return;}
            if(QuestProviderRunning()){StopQuestChain("Another quest task is active.");return;}
            if(questChainStartedQuest){
                questChainStartedQuest=false;
                if(!QuestManager.IsQuestComplete(questChainCandidate)){
                    if(independentDeferred){questChainDeferred[questChainCandidate]=questToolsStatus;questChainDeferredSequence[questChainCandidate]=QuestManager.GetQuestSequence(questChainCandidate);questChainCandidate=0;independentDeferred=false;}
                    else {StopQuestChain("Quest stopped before completion: "+questToolsStatus);return;}
                }
                if(questLeveling&&questChainCandidate!=0)levelingProgress.Complete();
                questChainLast=questChainCandidate;questChainCandidate=0;
            }
            if(questChainCandidate==0){
                if(!questLeveling&&questContinueLine&&questChainLast!=0)questChainCandidate=QuestChainSuccessor(questChainLast);
                questChainLast=0;
                if(questChainCandidate==0&&(questNextAccepted||questLeveling))questChainCandidate=NextAcceptedQuest();
                if(questChainCandidate==0){StopQuestChain("No eligible non-duty quests remain in this routine. "+questChainDeferred.Count+" left pending.");return;}
            }
            var id=questChainCandidate;
            if(QuestManager.IsQuestComplete(id)){
                questChainVisited.Add(id);questChainLast=id;questChainCandidate=0;return;
            }
            if(!questRouteReviews.TryGetValue(id,out var review)){
                questChainStatus="Checking duty requirements: "+QuestTitle(id);
                if(questRouteRequest!=null)return;
                var row=questCatalogue.FirstOrDefault(q=>q.Id==id);
                if(row==null){questChainDeferred[id]="No indexed quest path.";questChainVisited.Add(id);questChainCandidate=0;return;}
                questRouteRequestId=id;questRouteRequest=ReadQuestRoute(row);return;
            }
            questChainVisited.Add(id);
            if(!review.Readable){
                questChainDeferred[id]=review.Reason;questChainStatus=QuestTitle(id)+" · "+review.Reason;questChainCandidate=0;return;
            }
            if(!StartIndependentQuest(id)){
                questChainDeferred[id]=questToolsStatus;questChainCandidate=0;return;
            }
            questChainStartedQuest=true;
        }catch(Exception e){StopQuestChain("Quest routine stopped: "+e.Message);}
    }
}

