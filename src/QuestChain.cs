using System.Net.Http;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool questContinueLine=true,questNextAccepted=true,questChainActive,questChainStartedQuest;
    private uint questChainCandidate,questChainLast;
    private ulong questChainCharacter;
    private readonly HashSet<uint> questChainVisited=[];
    private readonly Dictionary<uint,string> questChainDeferred=[];
    private readonly Dictionary<uint,QuestRouteReview> questRouteReviews=[];
    private Task<QuestRouteReview>? questRouteRequest;
    private uint questRouteRequestId;
    private DateTimeOffset questChainNext;
    private string questChainStatus="";
    private void DrawQuestChainOptions(){
        ImGui.BeginDisabled(questChainActive);
        ImGui.Checkbox("Continue quest line",ref questContinueLine);
        ImGui.Checkbox("Then do other accepted quests",ref questNextAccepted);
        ImGui.EndDisabled();
        ImGui.TextDisabled("Duty / solo-duty routes stay pending. No AutoDuty required.");
        if(questChainStatus.Length>0)ImGui.TextWrapped(questChainStatus);
        if(questChainActive&&ImGui.Button("Stop quest routine"))StopQuestChain("Stopped by you.");
        if(questChainDeferred.Count>0&&ImGui.CollapsingHeader("Quests left pending")){
            foreach(var entry in questChainDeferred)ImGui.TextWrapped(QuestTitle(entry.Key)+": "+entry.Value);
        }
    }
    private void BeginQuestChain(uint id){
        if(questChainActive||eventAccountActive||questRecorderActive||QuestToolsOtherOperation()||!Player.IsLoaded){questToolsStatus="Finish the other operation first.";return;}
        try{if(QuestProviderRunning()){questToolsStatus="Questionable already has a task; it was left alone.";return;}}
        catch{questToolsStatus="Install / enable Questionable first.";return;}
        questChainActive=true;questChainStartedQuest=false;questChainCharacter=Player.ContentId;
        questChainCandidate=QuestToolsPolicy.ShortId(id);questChainLast=0;questChainVisited.Clear();questChainDeferred.Clear();questChainNext=default;
        questChainStatus="Checking the selected quest route before starting.";
    }
    private void StopQuestChain(string reason){
        questChainActive=false;questChainStartedQuest=false;questChainCandidate=0;
        StopQuestionableQuest(reason);questChainStatus=reason;
    }
    private static async Task<QuestRouteReview> ReadQuestRoute(QuestCatalogueRow row){
        try{
            var path="QuestPaths/"+row.Group+"/"+row.Id+"_"+row.Name+".json";
            var encoded=string.Join("/",path.Split('/').Select(Uri.EscapeDataString));
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(20)};
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var response=await http.GetAsync("https://raw.githubusercontent.com/PunishXIV/Questionable/f737a0865627bc6fa70fb7024ab403b3e4881736/"+encoded,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is >1048576)return new(false,false,[],"Route exceeds review limit.");
            using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer=new MemoryStream();
            var bytes=new byte[8192];int n;
            while((n=await stream.ReadAsync(bytes,timeout.Token))>0){if(buffer.Length+n>1048576)return new(false,false,[],"Route exceeds review limit.");buffer.Write(bytes,0,n);}
            return QuestRoutePolicy.Review(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
        }catch(Exception e){return new(false,false,[],"Could not check route: "+e.GetType().Name);}
    }
    private unsafe uint QuestChainSuccessor(uint previous){
        var explicitNext=questRouteReviews.GetValueOrDefault(previous)?.Next??[];
        var candidates=explicitNext.Length>0?explicitNext:
            questCatalogue.Where(q=>{
                var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(q.Id+65536);
                return row.HasValue&&row.Value.PreviousQuest.Any(p=>QuestToolsPolicy.ShortId(p.RowId)==previous);
            }).Select(q=>q.Id).ToArray();
        var available=candidates.Where(id=>id!=previous&&!questChainVisited.Contains(id)&&!QuestManager.IsQuestComplete(id)&&
            !Pi.GetIpcSubscriber<string,bool>("Questionable.IsQuestLocked").InvokeFunc(id.ToString())).ToArray();
        var accepted=available.Where(id=>QuestManager.Instance()->IsQuestAccepted(id)).ToArray();
        // Do not choose between unaccepted branches on the player's behalf.
        return accepted.Length>0?accepted[0]:available.Length==1?available[0]:0;
    }
    private unsafe uint NextAcceptedQuest(){
        var qm=QuestManager.Instance();if(qm==null)return 0;
        foreach(ref var quest in qm->NormalQuests){
            var id=(uint)quest.QuestId;
            if(id!=0&&!questChainVisited.Contains(id)&&questCatalogue.Any(q=>q.Id==id))return id;
        }
        return 0;
    }
    private unsafe void UpdateQuestChain(DateTimeOffset now){
        if(questRouteRequest?.IsCompleted==true){
            questRouteReviews[questRouteRequestId]=questRouteRequest.GetAwaiter().GetResult();questRouteRequest=null;
        }
        if(!questChainActive||now<questChainNext)return;questChainNext=now.AddSeconds(1);
        try{
            if(!Player.IsLoaded||Player.ContentId!=questChainCharacter){StopQuestChain("Stopped: character changed.");return;}
            if(QuestToolsOtherOperation()||eventAccountActive||questRecorderActive){StopQuestChain("Stopped: another helper started.");return;}
            if(Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]||Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.InDutyQueue]){StopQuestChain("Duty / queue detected; continue it manually.");return;}
            if(questionableOwnedQuest!=0){questChainStatus=questToolsStatus;return;}
            if(QuestProviderRunning()){StopQuestChain("Questionable has another task; it was left alone.");return;}
            if(questChainStartedQuest){
                questChainStartedQuest=false;
                if(!QuestManager.IsQuestComplete(questChainCandidate)){
                    StopQuestChain("Quest stopped before completion: "+questToolsStatus);return;
                }
                questChainLast=questChainCandidate;questChainCandidate=0;
            }
            if(questChainCandidate==0){
                if(questContinueLine&&questChainLast!=0)questChainCandidate=QuestChainSuccessor(questChainLast);
                questChainLast=0;
                if(questChainCandidate==0&&questNextAccepted)questChainCandidate=NextAcceptedQuest();
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
            if(!review.Readable||review.Duty){
                questChainDeferred[id]=review.Reason;questChainStatus=QuestTitle(id)+" · "+review.Reason;questChainCandidate=0;return;
            }
            if(!StartQuestionableQuest(id)){
                questChainDeferred[id]=questToolsStatus;questChainCandidate=0;return;
            }
            questChainStartedQuest=true;
        }catch(Exception e){StopQuestChain("Quest routine stopped: "+e.Message);}
    }
}
