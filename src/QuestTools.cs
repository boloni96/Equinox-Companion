using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private QuestToolsWindow questToolsWindow=null!;
    private bool questToolsCommand,companionHelperCommand,companionFollowCommand;
    private CompanionHelperWindow companionHelperWindow=null!;
    private Task<QuestCatalogueRow[]>? questCatalogueTask;
    private QuestCatalogueRow[] questCatalogue=[],questSearchRows=[];
    private string questSearch="",questToolsStatus="Choose a quest, or record one manually.";
    private uint questionableOwnedQuest;
    private ulong questionableOwnedCharacter;
    private DateTimeOffset questionableNext,questionableStarted;
    private bool QuestProviderRunning()=>Pi.GetIpcSubscriber<bool>("Questionable.IsRunning").InvokeFunc();
    private string? QuestProviderCurrent()=>Pi.GetIpcSubscriber<string?>("Questionable.GetCurrentQuestId").InvokeFunc();
    private void InitializeQuestTools()
    {
        questToolsWindow=new(this);windows.AddWindow(questToolsWindow);
        companionHelperWindow=new(this);windows.AddWindow(companionHelperWindow);
        companionHelperCommand=Commands.AddHandler("/eqhelper",new CommandInfo((_,_)=>companionHelperWindow.IsOpen=true){HelpMessage="Open Helper: FollowThem, quest assistance, quest library and recorder."});
        companionFollowCommand=Commands.AddHandler("/eqfollow",new CommandInfo((_,_)=>companionHelperWindow.IsOpen=true){HelpMessage="Open the standalone FollowThem / Helper window."});
        questToolsCommand=Commands.AddHandler("/eqquests",new CommandInfo((_,_)=>questToolsWindow.IsOpen=true){HelpMessage="Quest catalogue, Questionable integration and quest recorder."});
        var path=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"questionable-quests.json");
        questCatalogueTask=Task.Run(()=>JsonSerializer.Deserialize<QuestCatalogueRow[]>(File.ReadAllText(path))??[]);
        InitializeQuestRecorder();
    }
    private void DisposeQuestTools()
    {
        StopEventAccount("Plugin unloaded");
        StopQuestionableQuest("Plugin unloaded");
        StopQuestRecording("Plugin unloaded");
        DisposeQuestRecorder();
        if(questToolsCommand)Commands.RemoveHandler("/eqquests");
        windows.RemoveWindow(questToolsWindow);windows.RemoveWindow(companionHelperWindow);
        if(companionHelperCommand)Commands.RemoveHandler("/eqhelper");
        if(companionFollowCommand)Commands.RemoveHandler("/eqfollow");
    }
    private bool QuestToolsOtherOperation()=>followSession.Armed||TraderRunning||gardenCareRun?.Running==true||
        gearRouting||armoireRunning||retrievalRun.Running||organizeRunning||sellerRun.Running||keyboundBuying||keyboundEnabled;
    private unsafe bool StartQuestionableQuest(uint id)
    {
        try
        {
            id=QuestToolsPolicy.ShortId(id);
            if(!Player.IsLoaded||QuestManager.Instance()==null)throw new InvalidOperationException("Log into the intended character first.");
            if(QuestToolsOtherOperation())throw new InvalidOperationException("Stop the other helper or inventory operation first.");
            if(questRecorderActive)throw new InvalidOperationException("Stop manual recording before starting quest automation.");
            if(QuestManager.IsQuestComplete(id)){questToolsStatus="Quest already complete. Reward progress is checked separately.";return true;}
            if(QuestProviderRunning())throw new InvalidOperationException("Questionable is already running; its current task was left alone.");
            var key=id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(Pi.GetIpcSubscriber<string,bool>("Questionable.IsQuestLocked").InvokeFunc(key))
                throw new InvalidOperationException("Quest unavailable or not supported by the installed Questionable. Check its prerequisites and update its quest paths.");
            if(!Pi.GetIpcSubscriber<string,bool>("Questionable.StartSingleQuest").InvokeFunc(key))
                throw new InvalidOperationException("Questionable did not accept this quest. Record the missing steps instead.");
            questionableOwnedQuest=id;questionableOwnedCharacter=Player.ContentId;
            questionableStarted=DateTimeOffset.UtcNow;questionableNext=default;
            questToolsStatus="Questionable started one quest: "+QuestTitle(id);
            return true;
        }
        catch(Exception e){questToolsStatus="Quest not started: "+e.Message;return false;}
    }
    private string QuestTitle(uint id)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(65536+QuestToolsPolicy.ShortId(id))?.Name.ToString()??
        questCatalogue.FirstOrDefault(x=>x.Id==QuestToolsPolicy.ShortId(id))?.Name??id.ToString();
    private void StopQuestionableQuest(string reason)
    {
        if(questionableOwnedQuest==0)return;
        try
        {
            if(QuestProviderCurrent()==questionableOwnedQuest.ToString())
                Pi.GetIpcSubscriber<string,bool>("Questionable.Stop").InvokeFunc("Equinox: "+reason);
        }
        catch(Exception e){Log.Warning(e,"Could not stop Equinox-owned Questionable quest");}
        questionableOwnedQuest=0;questToolsStatus=reason;
    }
    private unsafe void UpdateQuestTools(DateTimeOffset now)
    {
        if(questCatalogueTask?.IsCompleted==true){
            if(questCatalogueTask.IsCompletedSuccessfully){questCatalogue=questCatalogueTask.Result;FilterQuestCatalogue();}
            else{_ = questCatalogueTask.Exception;questToolsStatus="Quest catalogue could not load; recorder remains available.";}
            questCatalogueTask=null;
        }
        UpdateQuestRecorder(now);
        if(questionableOwnedQuest!=0&&now>=questionableNext){
            questionableNext=now.AddSeconds(1);
            try{
                if(!Player.IsLoaded||Player.ContentId!=questionableOwnedCharacter){StopQuestionableQuest("Quest stopped: character changed or logged out.");}
                else if(QuestToolsOtherOperation()){StopQuestionableQuest("Quest stopped: another helper started.");}
                else if(QuestProviderCurrent() is {} current&&current!=questionableOwnedQuest.ToString()){
                    questionableOwnedQuest=0;questToolsStatus="Questionable switched quests outside this routine; assistance stopped.";
                }
                else if(QuestManager.IsQuestComplete(questionableOwnedQuest)){StopQuestionableQuest("Quest completed; reward collection is separate.");}
                else if(!QuestProviderRunning()&&now-questionableStarted>TimeSpan.FromSeconds(5)){questionableOwnedQuest=0;questToolsStatus="Questionable stopped before quest completion. Check its status; no restart was sent.";}
                else if(now-questionableStarted>TimeSpan.FromHours(2)){StopQuestionableQuest("Quest session timed out.");}
                else questToolsStatus=QuestTitle(questionableOwnedQuest)+" · quest sequence "+QuestManager.GetQuestSequence(questionableOwnedQuest)+" · Questionable running";
            }catch(Exception e){StopQuestionableQuest("Questionable unavailable: "+e.Message);}
        }
        UpdateEventAccount(now);
    }
    private void FilterQuestCatalogue()=>questSearchRows=questCatalogue.Where(q=>questSearch.Length==0||
        (q.Id+" "+q.Name+" "+q.Group).Contains(questSearch,StringComparison.OrdinalIgnoreCase)).Take(100).ToArray();
    private void DrawQuestTools()
    {
        if(ImGui.BeginTabBar("QuestToolsTabs")){
            if(ImGui.BeginTabItem("Quest library")){
                ImGui.TextWrapped("Uses your installed Questionable and its requirements. Starts only the quest you choose; does not replace your priority list.");
                ImGui.TextDisabled($"{questCatalogue.Length:N0} indexed quests · source snapshot 2 Nov 2026");
                if(ImGui.InputTextWithHint("##QuestSearch","Quest name, ID, expansion or category",ref questSearch,160))FilterQuestCatalogue();
                ImGui.TextWrapped(questToolsStatus);
                if(questionableOwnedQuest!=0&&ImGui.Button("Stop quest"))StopQuestionableQuest("Stopped by you.");
                if(ImGui.CollapsingHeader("Questionable setup")){
                    ImGui.TextWrapped("Install and enable Questionable with its required plugins. The catalogue is a reference; availability is checked against your installed version and this character.");
                    if(ImGui.Button("Questionable installation"))Dalamud.Utility.Util.OpenLink("https://github.com/PunishXIV/Questionable");
                }
                if(ImGui.BeginChild("QuestCatalogue",Vector2.Zero,true)){
                    foreach(var q in questSearchRows){
                        ImGui.PushID((int)q.Id);ImGui.BeginDisabled(questionableOwnedQuest!=0||eventAccountActive||questRecorderActive);
                        if(ImGui.SmallButton("Start"))StartQuestionableQuest(q.Id);
                        ImGui.EndDisabled();ImGui.SameLine();ImGui.TextUnformatted(q.Name+" · "+q.Id);
                        if(ImGui.IsItemHovered())ImGui.SetTooltip(q.Group);
                        ImGui.PopID();
                    }
                    if(questSearchRows.Length==100)ImGui.TextDisabled("Showing first 100 matches; refine search.");
                }ImGui.EndChild();ImGui.EndTabItem();
            }
            if(ImGui.BeginTabItem("Recorder")){DrawQuestRecorder();ImGui.EndTabItem();}
            ImGui.EndTabBar();
        }
    }
    private sealed class CompanionHelperWindow:Window
    {
        private readonly Plugin p;
        internal CompanionHelperWindow(Plugin p):base("Helper · Equinox###EquinoxStandaloneHelper"){
            this.p=p;Size=new(560,480);SizeCondition=ImGuiCond.FirstUseEver;RespectCloseHotkey=false;
        }
        public override void Draw()=>p.DrawHelperWindowBody();
    }
    private sealed class QuestToolsWindow:Window
    {
        private readonly Plugin p;
        internal QuestToolsWindow(Plugin p):base("Quest tools · Equinox"){this.p=p;Size=new(560,480);SizeCondition=ImGuiCond.FirstUseEver;RespectCloseHotkey=false;}
        public override void Draw()=>p.DrawQuestTools();
    }
}
