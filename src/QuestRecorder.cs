using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using EquinoxCompanion.Keybound;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed record QuestRecordedEvent(DateTimeOffset At,string Kind,object Data);
    private readonly List<QuestRecordedEvent> questRecorded=[];
    private readonly Dictionary<string,string> questRecordedWindows=[];
    private readonly HashSet<uint> questRecordedIds=[];
    private bool questRecorderActive,questRecorderHookEnabled,questRecorderIntHookEnabled;
    private ulong questRecorderCharacter;
    private DateTimeOffset questRecorderStarted,questRecorderPoll,questRecorderCheckpoint;
    private string questRecorderStatus="Record manually: Start → do the quest → Stop & export.";
    private string questRecorderPath="",questRecorderState="",questRecorderLabel="Event quest";
    private Task<DiagnosticExportResult>? questRecorderWrite;
    private bool questRecorderSaveAgain;
    private Vector3? questRecorderPosition;
    private uint questRecorderTerritory;
    private float questRecorderRotation;
    private void InitializeQuestRecorder(){
        Addons.RegisterListener(AddonEvent.PostSetup,RecordQuestAddon);
        Addons.RegisterListener(AddonEvent.PreFinalize,RecordQuestAddon);
        Addons.RegisterListener(AddonEvent.PreReceiveEvent,RecordQuestAddon);
    }
    private void DisposeQuestRecorder()=>Addons.UnregisterListener(RecordQuestAddon);
    private void RecordQuest(string kind,object data){
        if(!questRecorderActive)return;
        if(questRecorded.Count>=12000){StopQuestRecording("Recording limit reached; exported all captured events.");return;}
        questRecorded.Add(new(DateTimeOffset.UtcNow,kind,data));
    }
    private unsafe void RecordQuestAddon(AddonEvent type,AddonArgs args){
        if(!questRecorderActive||!QuestToolsPolicy.RecordWindow(args.AddonName))return;
        try{
            if(type==AddonEvent.PreReceiveEvent&&args is AddonReceiveEventArgs received){
                if(!received.AtkEventType.ToString().Contains("Click",StringComparison.Ordinal)&&!received.AtkEventType.ToString().Contains("Change",StringComparison.Ordinal))return;
                // Store the event identifier only; never dereference event payloads.
                RecordQuest("ui-event",new {addon=args.AddonName,eventType=received.AtkEventType.ToString(),parameter=received.EventParam});
            }else RecordQuest("window",new {addon=args.AddonName,change=type.ToString()});
        }catch(Exception e){Log.Debug(e,"Quest recorder UI observation");}
    }
    private unsafe void RecordQuestCallback(AtkUnitBase* addon,uint count,AtkValue* values){
        if(!questRecorderActive||addon==null||values==null||count>32||!QuestToolsPolicy.RecordWindow(addon->NameString))return;
        try{
            var copied=new List<object>();
            for(var i=0;i<count;i++){
                var v=values[i];
                // Never capture arbitrary input strings, chat, credentials or pointers.
                copied.Add(v.Type switch{
                    AtkValueType.Int=>(object)new {index=i,type="int",value=(long)v.Int},
                    AtkValueType.UInt=>new {index=i,type="uint",value=(long)v.UInt},
                    _=>new {index=i,type=v.Type.ToString(),value="not captured"}
                });
            }
            RecordQuest("callback",new {addon=addon->NameString,values=copied});
        }catch(Exception e){Log.Debug(e,"Quest recorder callback observation");}
    }
    private void RecordQuestNpc(Dalamud.Game.ClientState.Objects.Types.IGameObject obj){
        if(!questRecorderActive)return;
        RecordQuest("interaction",new {id=obj.BaseId,name=obj.Name.TextValue,kind=obj.ObjectKind.ToString(),position=RecorderPoint(obj.Position),territory=Client.TerritoryType,world=Player.CurrentWorld.RowId});
    }
    private unsafe object[] QuestRecorderBags(){
        var manager=InventoryManager.Instance();if(manager==null)return [];
        var items=new List<object>();
        foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4}){
            var bag=manager->GetInventoryContainer(type);if(bag==null||!bag->IsLoaded||bag->Items==null||bag->Size>200)continue;
            for(var i=0;i<bag->Size;i++)if(bag->Items[i].ItemId!=0)items.Add(new {container=type.ToString(),slot=i,item=bag->Items[i].ItemId,quantity=bag->Items[i].Quantity});
        }
        return items.ToArray();
    }
    private static object RecorderPoint(Vector3 p)=>new {x=p.X,y=p.Y,z=p.Z};
    private void StartQuestRecording(){
        if(!Player.IsLoaded){questRecorderStatus="Log into the character first.";return;}
        if(questChainActive||independentQuestId!=0||eventAccountActive||QuestToolsOtherOperation()){questRecorderStatus="Stop automation before recording a manual quest.";return;}
        if(questRecorderWrite!=null){questRecorderStatus="Wait for the previous export to finish.";return;}
        questRecorded.Clear();questRecordedWindows.Clear();questRecordedIds.Clear();
        questRecorderCharacter=Player.ContentId;questRecorderStarted=DateTimeOffset.UtcNow;
        questRecorderPath="";questRecorderPosition=null;questRecorderState="";questRecorderPoll=default;questRecorderCheckpoint=questRecorderStarted.AddSeconds(30);
        questRecorderActive=true;questRecorderStatus="Recording · do the quest manually. Mark a problem whenever needed.";
        questRecorderHookEnabled=callbackHook?.IsEnabled==true;questRecorderIntHookEnabled=callbackIntHook?.IsEnabled==true;
        callbackHook?.Enable();callbackIntHook?.Enable();
        RecordQuest("start",new {label=questRecorderLabel,character=Player.CharacterName,homeWorld=Player.HomeWorld.RowId,currentWorld=Player.CurrentWorld.RowId});
    }
    private void StopQuestRecording(string reason){
        if(!questRecorderActive)return;
        if(questRecorded.Count<12000)questRecorded.Add(new(DateTimeOffset.UtcNow,"stop",new {reason}));questRecorderActive=false;questRecorderStatus=reason;
        if(!questRecorderHookEnabled&&!ObservingGardens)callbackHook?.Disable();
        if(!questRecorderIntHookEnabled&&!ObservingGardens)callbackIntHook?.Disable();
        ExportQuestRecording(true);
    }
    private void ExportQuestRecording(bool final=false){
        if(questRecorded.Count==0)return;
        if(questRecorderWrite!=null&&!final){questRecorderSaveAgain=true;return;}
        var path=Path.Combine(Pi.GetPluginConfigDirectory(),"exports","quest-"+questRecorderStarted.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]+".json");
        var data=new {format="equinox-quest-recording-v1",performance=CapturePerformanceReport(),version=typeof(Plugin).Assembly.GetName().Version?.ToString(),started=questRecorderStarted,
            label=questRecorderLabel,recording=questRecorderActive,limitations="Observed UI events and state, not an executable quest path. Arbitrary text input and private chat are excluded. Some native actions have no callback.",events=questRecorded.ToArray()};
        questRecorderWrite=DiagnosticExportWriter.Start(path,data,new JsonSerializerOptions{WriteIndented=true},()=>[],()=>[]);
        questRecorderSaveAgain=false;
    }
    private unsafe void UpdateQuestRecorder(DateTimeOffset now){
        if(questRecorderWrite?.IsCompleted==true){
            var r=questRecorderWrite.GetAwaiter().GetResult();questRecorderWrite=null;
            if(r.Path!=null){questRecorderPath=r.Path;if(!questRecorderActive)questRecorderStatus="Recording exported.";}
            else questRecorderStatus="Export failed: "+r.Error+". Recording is still held in memory; try Export again.";
            if(questRecorderSaveAgain)ExportQuestRecording();
        }
        if(!questRecorderActive||now<questRecorderPoll)return;
        questRecorderPoll=now.AddMilliseconds(500);
        try{
            if(!Player.IsLoaded||Player.ContentId!=questRecorderCharacter){StopQuestRecording("Stopped: character changed or logged out.");return;}
            if(now-questRecorderStarted>TimeSpan.FromHours(2)){StopQuestRecording("Two-hour recording limit reached.");return;}
            if(Objects.LocalPlayer is {} self){
                var map=AgentMap.Instance();
                if(questRecorderPosition==null||questRecorderTerritory!=Client.TerritoryType||Vector3.DistanceSquared(self.Position,questRecorderPosition.Value)>=1||
                    Math.Abs(self.Rotation-questRecorderRotation)>.5f){
                    RecordQuest("movement",new {position=RecorderPoint(self.Position),rotation=self.Rotation,world=Player.CurrentWorld.RowId,territory=Client.TerritoryType,map=map==null?0:map->CurrentMapId});
                    questRecorderPosition=self.Position;questRecorderTerritory=Client.TerritoryType;questRecorderRotation=self.Rotation;
                }
            }
            var qm=QuestManager.Instance();
            if(qm!=null){
                foreach(ref var q in qm->NormalQuests)if(q.QuestId!=0)questRecordedIds.Add(q.QuestId);
                var quests=questRecordedIds.Order().Select(id=>new {id,sheetId=65536+id,name=QuestTitle(id),accepted=qm->IsQuestAccepted(id),sequence=QuestManager.GetQuestSequence(id),complete=QuestManager.IsQuestComplete(id)}).ToArray();
                var target=Targets.Target;
                var state=new {quests,bags=QuestRecorderBags(),scene=HelperScene(),target=target==null?null:new {id=target.BaseId,name=target.Name.TextValue,kind=target.ObjectKind.ToString(),position=RecorderPoint(target.Position)},
                    conditions=Enum.GetValues<Dalamud.Game.ClientState.Conditions.ConditionFlag>().Where(c=>(int)c>0&&(int)c<100&&Conditions[c]).Select(c=>c.ToString()).ToArray()};
                var fingerprint=JsonSerializer.Serialize(state);
                if(fingerprint!=questRecorderState){RecordQuest("state",state);questRecorderState=fingerprint;}
            }
            var visible=KeyboundUi.VisibleAddons().Distinct().Where(QuestToolsPolicy.RecordWindow).ToHashSet();
            foreach(var name in visible){
                var text=KeyboundUi.Texts(name).Take(48).Select(t=>new {node=t.NodeId,text=t.Text.Length>1000?t.Text[..1000]:t.Text}).ToArray();
                var key=JsonSerializer.Serialize(text);
                if(!questRecordedWindows.TryGetValue(name,out var old)||old!=key){RecordQuest("window-content",new {addon=name,text});questRecordedWindows[name]=key;}
            }
            foreach(var name in questRecordedWindows.Keys.Except(visible).ToArray()){RecordQuest("window-closed",new {addon=name});questRecordedWindows.Remove(name);}
            if(now>=questRecorderCheckpoint){questRecorderCheckpoint=now.AddSeconds(30);ExportQuestRecording();}
        }catch(Exception e){questRecorderStatus="Observation unavailable: "+e.GetType().Name;Log.Debug(e,"Quest recorder observation");}
    }
    private void DrawQuestCutsceneControls(){
        if(!questRecorderActive&&!nativeEventActive&&!eventAccountActive&&independentQuestId==0)return;
        var viewport=ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos+new Vector2(20,80),ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(290,0),ImGuiCond.Always);
        if(ImGui.Begin("Quest controls · Equinox",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoFocusOnAppearing|ImGuiWindowFlags.NoNavFocus)){
            if(questRecorderActive){
                ImGui.TextUnformatted("Recording · cutscene controls");
                if(ImGui.Button("Mark problem here"))RecordQuest("problem",new {message="User marked a problem during cutscene",territory=Client.TerritoryType,scene=HelperScene()});
                ImGui.SameLine();if(ImGui.Button("Stop & export"))StopQuestRecording("Stopped by you.");
            }else{
                ImGui.TextWrapped(eventAccountActive?eventAccountStatus:nativeEventActive?nativeEventStatus:questToolsStatus);
                if(ImGui.Button("Stop helper")){
                    if(eventAccountActive)StopEventAccount("Stopped by you.");
                    else if(nativeEventActive)StopNativeEventQuest("Stopped by you.");
                    else StopIndependentQuest("Stopped by you.");
                }
            }
        }ImGui.End();
    }
    private void DrawQuestRecorder(){
        ImGui.TextWrapped("Records quest progress, NPC interactions, movement, window changes and numeric UI actions. Includes special event windows. No automatic clicking or replay.");
        ImGui.BeginDisabled(questRecorderActive);ImGui.InputText("Label",ref questRecorderLabel,100);ImGui.EndDisabled();
        if(ImGui.Button(questRecorderActive?"Stop & export":"Start recording")){
            if(questRecorderActive)StopQuestRecording("Stopped by you.");else StartQuestRecording();
        }
        ImGui.SameLine();ImGui.BeginDisabled(!questRecorderActive);
        if(ImGui.Button("Mark problem here"))RecordQuest("problem",new {message="User marked a problem",territory=Client.TerritoryType,scene=HelperScene()});
        ImGui.EndDisabled();
        ImGui.BeginDisabled(questRecorded.Count==0||questRecorderWrite!=null);
        if(ImGui.Button("Export"))ExportQuestRecording();
        ImGui.EndDisabled();ImGui.SameLine();
        if(ImGui.Button("Open exports folder")){var p=Path.Combine(Pi.GetPluginConfigDirectory(),"exports");Directory.CreateDirectory(p);Dalamud.Utility.Util.OpenLink(p);}
        ImGui.TextWrapped(questRecorderStatus);
        ImGui.TextDisabled($"{questRecorded.Count:N0} observations · automatic checkpoint every 30 seconds");
        if(questRecorderPath.Length>0)ImGui.TextWrapped("Latest export: "+Path.GetFileName(questRecorderPath));
        ImGui.TextWrapped("Export includes game dialogue and your character name. Chat, private windows and arbitrary typed input are excluded. Recording is evidence for adding support, not a guarantee that every native interaction can be replayed.");
    }
}
