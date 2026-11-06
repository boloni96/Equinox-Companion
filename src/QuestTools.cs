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
    private void InitializeQuestTools()
    {
        questToolsWindow=new(this);windows.AddWindow(questToolsWindow);
        navigationSystemWindow=new(this);windows.AddWindow(navigationSystemWindow);
        companionHelperWindow=new(this);windows.AddWindow(companionHelperWindow);
        companionHelperCommand=Commands.AddHandler("/eqhelper",new CommandInfo((_,_)=>companionHelperWindow.IsOpen=true){HelpMessage="Open Helper: FollowThem, quest assistance, quest library and recorder."});
        companionFollowCommand=Commands.AddHandler("/eqfollow",new CommandInfo((_,_)=>companionHelperWindow.IsOpen=true){HelpMessage="Open the standalone FollowThem / Helper window."});
        questToolsCommand=Commands.AddHandler("/eqquests",new CommandInfo((_,_)=>questToolsWindow.IsOpen=true){HelpMessage="Independent quest runner, library and recorder."});
        // Dalamud can load assemblies from bytes: Assembly.Location is then empty.
        // Resolve assets from its installation path, and isolate I/O failures from startup.
        var directory=Pi.AssemblyLocation.DirectoryName;
        questCatalogueTask=Task.Run(()=>{
            if(string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("The plugin installation directory is unavailable.");
            var path=Path.Combine(directory,"questionable-quests.json");
            return JsonSerializer.Deserialize<QuestCatalogueRow[]>(File.ReadAllText(path))??[];
        });
        questLeveling=config.QuestLeveling;questLevelingIncludeMsq=config.QuestLevelingIncludeMsq;
        InitializeIndependentQuests(directory);
        InitializeQuestRecorder();
    }
    private void DisposeQuestTools()
    {
        StopNativeEventQuest("Plugin unloaded");
        StopQuestChain("Plugin unloaded");
        StopEventAccount("Plugin unloaded");
        StopIndependentQuest("Plugin unloaded");
        StopQuestRecording("Plugin unloaded");
        DisposeQuestRecorder();DisposeIndependentWalk();RemoveWorkBar();
        if(questToolsCommand)Commands.RemoveHandler("/eqquests");
        windows.RemoveWindow(questToolsWindow);windows.RemoveWindow(companionHelperWindow);windows.RemoveWindow(navigationSystemWindow);
        if(companionHelperCommand)Commands.RemoveHandler("/eqhelper");
        if(companionFollowCommand)Commands.RemoveHandler("/eqfollow");
    }
    private bool QuestToolsOtherOperation()=>NativeGardenWalking||nativeHousingTrip!=null||followSession.Armed||TraderRunning||gardenCareRun?.Running==true||
        gearRouting||armoireRunning||retrievalRun.Running||organizeRunning||sellerRun.Running||keyboundBuying||keyboundEnabled;
    private string QuestTitle(uint id)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(65536+QuestToolsPolicy.ShortId(id))?.Name.ToString()??
        questCatalogue.FirstOrDefault(x=>x.Id==QuestToolsPolicy.ShortId(id))?.Name??id.ToString();
    private unsafe void UpdateQuestTools(DateTimeOffset now)
    {
        if(questCatalogueTask?.IsCompleted==true){
            if(questCatalogueTask.IsCompletedSuccessfully){questCatalogue=questCatalogueTask.Result;FilterQuestCatalogue();}
            else{_ = questCatalogueTask.Exception;questToolsStatus="Quest catalogue could not load; recorder remains available.";}
            questCatalogueTask=null;
        }
        UpdateQuestRecorder(now);
        UpdateLevelingGoal(now);
        UpdateIndependentWalk(now);
        UpdateIndependentQuest(now);
        UpdateNativeEventQuest(now);
        UpdateQuestChain(now);
        UpdateEventAccount(now);
        UpdateWorkBar(now);
    }
    private void FilterQuestCatalogue()=>questSearchRows=questCatalogue.Where(q=>questSearch.Length==0||
        (q.Id+" "+q.Name+" "+q.Group).Contains(questSearch,StringComparison.OrdinalIgnoreCase)).Take(100).ToArray();
    private void DrawQuestTools()
    {
        if(ImGui.BeginTabBar("QuestToolsTabs")){
            if(ImGui.BeginTabItem("Event quest")){DrawNativeEventQuest();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Quest library")){
                ImGui.TextWrapped("Companion runs imported quest steps directly with local collision navigation. Questionable and vnavmesh are not required. Unsupported steps remain pending with a reason; duties are manual.");
                ImGui.TextDisabled($"{questCatalogue.Length:N0} indexed quests · source snapshot 2 Nov 2026");
                if(ImGui.InputTextWithHint("##QuestSearch","Quest name, ID, expansion or category",ref questSearch,160))FilterQuestCatalogue();
                DrawQuestChainOptions();
                ImGui.TextWrapped(questToolsStatus);
                if(independentQuestId!=0&&ImGui.Button("Stop quest"))StopIndependentQuest("Stopped by you.");
                ImGui.TextDisabled($"Independent route data: {independentBook?.Routes.Count ?? 0:N0}");
                if(independentQuestBookError.Length>0)ImGui.TextWrapped(independentQuestBookError);
                ImGui.TextWrapped(independentWalkStatus);
                if(ImGui.BeginChild("QuestCatalogue",Vector2.Zero,true)){
                    foreach(var q in questSearchRows){
                        ImGui.PushID((int)q.Id);ImGui.BeginDisabled(nativeEventActive||questChainActive||independentQuestId!=0||eventAccountActive||questRecorderActive);
                        if(ImGui.SmallButton("Start")){if(q.Id==NativeEventPolicy.Quest)StartNativeEventQuest();else BeginQuestChain(q.Id);}
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
        public override void Draw()=>p.MeasureWindow(WindowName,DrawMeasuredContent);
        private void DrawMeasuredContent()=>p.DrawHelperWindowBody();
        public override void PostDraw()=>p.MeasureWindow(WindowName,PostDrawMeasured,"PostDraw");
        private void PostDrawMeasured()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Helper"));
    }
    private sealed class QuestToolsWindow:Window
    {
        private readonly Plugin p;
        internal QuestToolsWindow(Plugin p):base("Quest tools · Equinox"){this.p=p;Size=new(560,480);SizeCondition=ImGuiCond.FirstUseEver;RespectCloseHotkey=false;}
        public override void Draw()=>p.MeasureWindow(WindowName,DrawMeasuredContent);
        private void DrawMeasuredContent()=>p.DrawQuestTools();
    }
}

