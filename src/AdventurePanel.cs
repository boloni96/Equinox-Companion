using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private sealed class AdventureWindow:Window
 {
  private readonly Plugin p;
  public AdventureWindow(Plugin p):base("Adventure · Equinox###EquinoxAdventure"){this.p=p;RespectCloseHotkey=false;Size=new(740,590);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(510,330),MaximumSize=new(float.MaxValue)};AllowPinning=true;}
  public override void Draw()=>p.DrawAdventure();
  public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Adventure"));
 }
 private AdventureWindow adventureWindow=null!;private readonly AdventureSync adventureSync=new();private bool adventureCommand;
 private Task<AdventureReply>? adventureRequest;private bool adventureMutation;private string adventureRequestKey="",adventureConnectionKey="",adventureMessage="Checking your paired Journal…";
 private string adventurePerson="",adventureAccount="";
 private AdventureBoard[] adventureBoards=[];private string adventureSelected="";private DateTimeOffset adventureNext,adventureFetched;private bool adventureFresh;
 private readonly Dictionary<string,bool> adventureCompletion=[];private string? adventurePrompt;private bool adventureFollow=true;private readonly AdventureLocationSession adventureFollowSession=new();
 private static string AdventureText(string value)=>value.Replace("##","");
 private void InitializeAdventures(){adventureWindow=new(this);windows.AddWindow(adventureWindow);adventureCommand=Commands.AddHandler("/adventure",new CommandInfo((_,_)=>OpenAdventure()){HelpMessage="Open shared Garden and Character adventures from your paired Journal. /equinox adventure also works."});}
 private void DisposeAdventures(){if(adventureCommand)Commands.RemoveHandler("/adventure");windows.RemoveWindow(adventureWindow);adventureSync.Dispose();DisposeAdventurePortraits();}
 private void OpenAdventure(){adventureWindow.IsOpen=true;adventureNext=default;nextRosterRead=default;}
 private (string Id,string Generation,int? Index,string? Task,bool Value)? adventureQueuedAction;
 private bool AdventureSaving=>adventureMutation||adventureQueuedAction is not null;
 private void AdventureAction(AdventureBoard b,int? index=null,AdventureTask? task=null,bool value=false)
 {
  if(AdventureSaving||!adventureFresh)return;
  if(adventureRequest is not null){adventureQueuedAction=(b.Id,b.Generation,index,task?.Id,value);return;}
  adventureMutation=true;adventureRequestKey=config.PairingKey;
  adventureRequest=index is {} n?adventureSync.Move(config.PairingKey,b,n):adventureSync.Check(config.PairingKey,b,task!.Id,value);
 }
 private void UpdateAdventure()
 {
  var now=DateTimeOffset.UtcNow;
  if(adventureConnectionKey!=config.PairingKey){adventureConnectionKey=config.PairingKey;adventureQueuedAction=null;adventureBoards=[];adventureSelected="";adventurePerson="";adventureAccount="";adventureCompletion.Clear();adventurePrompt=null;adventureFresh=false;adventureNext=default;
   var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.PairingKey)));
   if(config.AdventurePairingHash==hash){adventureBoards=config.CachedAdventures.Where(AdventurePolicy.Valid).ToArray();adventureSelected=config.SelectedAdventure;}
  }
  if(adventureRequest?.IsCompleted==true){
   var reply=adventureRequest.IsCompletedSuccessfully?adventureRequest.Result:new AdventureReply(null,"Adventure request failed.");
   if(adventureRequest.IsFaulted)_=adventureRequest.Exception;
   if(adventureRequestKey==config.PairingKey){adventureMessage=reply.Status;adventureFresh=reply.Boards is not null;if(reply.Boards is {} boards){adventureBoards=AdventurePolicy.Reconcile(adventureBoards,boards,adventureMutation);adventureFetched=now;CacheAdventures();}
    adventureNext=now.AddSeconds(reply.Boards is null?5:3);
   }adventureRequest=null;adventureMutation=false;
  }
  if(adventureRequest is null&&adventureQueuedAction is {} queued){
   adventureQueuedAction=null;
   var latest=adventureBoards.FirstOrDefault(b=>b.Id==queued.Id&&b.Generation==queued.Generation);
   var task=latest?.Stops.SelectMany(s=>s.Tasks).FirstOrDefault(t=>t.Id==queued.Task);
   if(adventureFresh&&latest is not null&&(queued.Index is {} index&&index>=0&&index<=latest.Stops.Length||task is not null&&AdventurePolicy.CanEdit(task)))AdventureAction(latest,queued.Index,task,queued.Value);
  }
  if(adventureRequest is null&&config.SyncEnabled&&config.PairingKey.Length==64&&now>=adventureNext){adventureRequestKey=config.PairingKey;adventureRequest=adventureSync.Read(config.PairingKey);adventureNext=now.AddSeconds(3);}
 }

 private void CacheAdventures()
 {
  var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.PairingKey)));
  var old=config.CachedAdventures;
  if(config.AdventurePairingHash==hash&&old.Length==adventureBoards.Length&&old.Zip(adventureBoards).All(x=>x.First.Id==x.Second.Id&&x.First.Revision==x.Second.Revision)&&config.SelectedAdventure==adventureSelected)return;
  config.AdventurePairingHash=hash;config.CachedAdventures=adventureBoards;config.SelectedAdventure=adventureSelected;SaveConfiguration();
 }
 private void DrawAdventureNavigation(AdventureBoard board)
 {
  if(board.Kind!="garden"){
   if(ImGui.BeginCombo("World / character",board.Index>=board.Stops.Length?"Round reviewed":AdventureText(board.Stops[board.Index].Title))){
    for(var i=0;i<board.Stops.Length;i++){var s=board.Stops[i];if(ImGui.Selectable($"{s.Tasks.Count(t=>t.Done)}/{s.Tasks.Length} · {AdventureText(s.Title)}##stop{i}",i==board.Index))AdventureAction(board,i);}ImGui.EndCombo();
   }return;
  }
  var current=board.Index<board.Stops.Length?board.Stops[board.Index]:board.Stops.FirstOrDefault();if(current is null)return;
  var worlds=board.Stops.Select(s=>s.World).Distinct().ToArray();
  ImGui.SetNextItemWidth(Math.Max(180,ImGui.GetContentRegionAvail().X-65));
  if(ImGui.BeginCombo("World",AdventureText(current.World))){foreach(var world in worlds){var stops=board.Stops.Select((s,i)=>(Stop:s,Index:i)).Where(x=>x.Stop.World==world).ToArray();if(ImGui.Selectable($"{stops.Count(x=>AdventurePolicy.Complete(x.Stop))}/{stops.Length} · {AdventureText(world)}",world==current.World)){var next=stops.FirstOrDefault(x=>!AdventurePolicy.Complete(x.Stop));AdventureAction(board,next.Stop is null?stops[0].Index:next.Index);}}ImGui.EndCombo();}
  string Label(AdventureStop stop){var names=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters.Where(c=>stop.CharacterIds?.Contains(c.Id)==true).Take(1).Select(c=>p.Name+" / "+c.Account+" / "+c.Name));return $"{stop.Tasks.Count(t=>t.Done)}/{stop.Tasks.Length} · {string.Join(", ",names)} · {stop.Title} · {stop.Subtitle}";}
  ImGui.SetNextItemWidth(Math.Max(180,ImGui.GetContentRegionAvail().X-65));
  if(ImGui.BeginCombo("House",AdventureText(Label(current)))){for(var i=0;i<board.Stops.Length;i++){var stop=board.Stops[i];if(stop.World!=current.World)continue;if(ImGui.Selectable(AdventureText(Label(stop))+"##house"+i,i==board.Index))AdventureAction(board,i);}ImGui.EndCombo();}
  if(ImGui.IsItemHovered())ImGui.SetTooltip(Label(current));
 }
 private bool AdventureInScope(AdventureBoard board)
 {
  if(adventurePerson.Length==0&&adventureAccount.Length==0)return true;
  var ids=(config.SharedRoster?.People??[]).Where(p=>adventurePerson.Length==0||p.Id==adventurePerson).SelectMany(p=>p.Characters).Where(c=>adventureAccount.Length==0||c.AccountId==adventureAccount).Select(c=>c.Id).ToHashSet();
  return board.Stops.Any(s=>s.CharacterIds?.Any(ids.Contains)==true);
 }
 private void DrawAdventureFilters()
 {
  var people=config.SharedRoster?.People??[];
  ImGui.SetNextItemWidth(180);
  if(ImGui.BeginCombo("Person",people.FirstOrDefault(p=>p.Id==adventurePerson)?.Name??"All people")){
   if(ImGui.Selectable("All people",adventurePerson.Length==0)){adventurePerson="";adventureAccount="";adventureSelected="";}
   foreach(var p in people)if(ImGui.Selectable(AdventureText(p.Name)+"##"+p.Id,p.Id==adventurePerson)){adventurePerson=p.Id;adventureAccount="";adventureSelected="";}
   ImGui.EndCombo();
  }
  var accounts=people.Where(p=>adventurePerson.Length==0||p.Id==adventurePerson).SelectMany(p=>(p.Accounts??[]).Select(a=>(Id:a.Id,Name:p.Name+" / "+a.Name))).ToArray();
  ImGui.SetNextItemWidth(260);
  if(ImGui.BeginCombo("Account",accounts.FirstOrDefault(a=>a.Id==adventureAccount).Name??"All accounts")){
   if(ImGui.Selectable("All accounts",adventureAccount.Length==0)){adventureAccount="";adventureSelected="";}
   foreach(var a in accounts)if(ImGui.Selectable(AdventureText(a.Name)+"##"+a.Id,a.Id==adventureAccount)){adventureAccount=a.Id;adventureSelected="";}
   ImGui.EndCombo();
  }
 }
 private bool DrawAdventureStepsHeader(AdventureTask[] tasks)
 {
  ImGui.SetNextItemOpen(false,ImGuiCond.Once);
  return ImGui.CollapsingHeader($"{tasks.Count(t=>t.Done)} / {tasks.Length} confirmed · Steps###confirmed-steps");
 }
 private void DrawAdventureHouseControls(AdventureStop stop)
 {
  if(string.IsNullOrEmpty(stop.HouseId))return;
  if(ImGui.SmallButton("Garden guide"))OnPlantingCommand("/gardening","");
  var house=config.SharedRoster?.People.SelectMany(p=>p.Characters).SelectMany(c=>c.Houses).FirstOrDefault(h=>h.Id==stop.HouseId);
  if(house is null)return;
  ImGui.SameLine();if(ImGui.SmallButton("GO SEE")){EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==house.World);if(world.Id>0)GoSeeBid(world.Id,house.District,house.Ward,house.Plot);}
  if(!string.IsNullOrEmpty(bidTravelMessage))ImGui.TextWrapped(bidTravelMessage);DrawGoSeeHomeStop();
 }
 private void DrawAdventureTaskList(AdventureBoard board,AdventureStop stop,AdventureTask[] tasks)
 {
    PrepareAdventureLayout(stop);
    foreach(var task in tasks){ImGui.PushID(task.Id);
     var house=AdventurePolicy.IsHouseVisit(task)?adventureLayoutHouses.GetValueOrDefault(AdventurePolicy.TaskHouseId(task)):null;
     var label=house is null?task.Label:$"{(task.Done?"Entered":"Enter")} · {house.Name} · {house.District} W{house.Ward} P{house.Plot}";var pending=board.Pending?.LastOrDefault(p=>p.TaskId==task.Id);var check=task.ServerConfirmed?task.Done:pending?.Checked??task.Done;
     ImGui.BeginDisabled(!AdventurePolicy.CanEdit(task)||(board.Protocol<2&&pending is not null)||AdventureSaving||!adventureFresh);
     if(ImGui.Checkbox("##done",ref check))AdventureAction(board,task:task,value:check);ImGui.EndDisabled();
     var hover=ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);ImGui.SameLine();
     if(ImGui.SmallButton("Copy"))ImGui.SetClipboardText(task.Group+" · "+label);ImGui.SameLine();ImGui.TextWrapped(label);
     var bedLink=task.Rule is {Batch:>=1,Bed:>=1 and <=8} rule&&rule.HouseId==stop.HouseId&&stop.HouseId.Length>0;
     if(bedLink&&ImGui.IsItemClicked()&&task.Rule is {} clicked)adventureOpenBed=(clicked.HouseId,clicked.Batch,clicked.Bed);
     if(hover||ImGui.IsItemHovered())ImGui.SetTooltip((bedLink?"Click to open this bed’s batch.\n":"")+(task.Status.Length>0?task.Status:task.Done?"Completion recorded in the Journal":"Waiting for completion")+(task.ServerConfirmed?"\nConfirmed automatically from game records. This check cannot be removed manually.":!task.CanCheck?"\nThis task updates automatically.":"\nManual completion can be changed here."));
     if(pending is not null&&!task.ServerConfirmed)ImGui.TextColored(new Vector4(1,.8f,.4f,1),board.Protocol>=2?"Saved · Journal will catch up":"Pending Journal confirmation");
     ImGui.PopID();
    }
 }
 private void DrawAdventureTaskSection(AdventureBoard board,AdventureStop stop,AdventureTask[] tasks,bool collapse)
 {
  if(collapse){
   ImGui.SetNextItemOpen(false,ImGuiCond.Once);
   if(!ImGui.CollapsingHeader($"{(board.Kind=="garden"?"Bed tasks":"Tasks")} · {tasks.Count(t=>t.Done)}/{tasks.Length} confirmed###task-checklist"))return;
  }
  DrawAdventureTaskList(board,stop,tasks);
 }
 private AdventureStop? adventureLayoutStop;private SharedRoster? adventureLayoutRoster;
 private (SharedCharacter Character,AdventureTask[] Tasks)[] adventureCharacterTasks=[];
 private AdventureTask[] adventureSharedTasks=[];
 private Dictionary<string,SharedHouse> adventureLayoutHouses=[];
 private void PrepareAdventureLayout(AdventureStop stop)
 {
  if(ReferenceEquals(stop,adventureLayoutStop)&&ReferenceEquals(config.SharedRoster,adventureLayoutRoster))return;
  adventureLayoutStop=stop;adventureLayoutRoster=config.SharedRoster;
  adventureLayoutHouses=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses).DistinctBy(h=>h.Id).ToDictionary(h=>h.Id);
  var characters=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>stop.CharacterIds?.Contains(c.Id)==true).ToArray();
  var groups=stop.Tasks.ToLookup(t=>AdventurePolicy.TaskCharacterId(t,characters));
  adventureCharacterTasks=characters.Select(c=>(c,groups[c.Id].ToArray())).ToArray();adventureSharedTasks=groups[""].ToArray();
 }
 private void DrawAdventureProgress(AdventureTask[] tasks)=>ImGui.ProgressBar(tasks.Length==0?0:(float)tasks.Count(t=>t.Done)/tasks.Length,new Vector2(-1,0),$"{tasks.Count(t=>t.Done)} / {tasks.Length} confirmed");
 private void DrawAdventure()
 {
  UpdateAdventure();
  if(!config.SyncEnabled||config.PairingKey.Length!=64){ImGui.TextWrapped("Pair Companion with your Journal in Settings to open the same saved adventures here.");if(ImGui.Button("Open Companion settings"))visible=true;return;}
  ImGui.TextColored(new Vector4(.65f,.8f,1,1),"YOUR PEOPLE · YOUR WORLDS");
  if(ImGui.Button("Adventure home")){adventureSelected="";adventurePrompt=null;}
  ImGui.SameLine();if(ImGui.Button("Open Journal"))Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#adventure");
  ImGui.SameLine();ImGui.BeginDisabled(adventureRequest is not null);if(ImGui.Button("Refresh"))adventureNext=default;ImGui.EndDisabled();
  if(!adventureFresh)ImGui.TextWrapped(adventureMessage);
  if(ImGui.CollapsingHeader("People, accounts & connection")){DrawAdventureFilters();ImGui.TextWrapped(adventureMessage);if(adventureFetched!=default)ImGui.TextDisabled($"Last checked {adventureFetched.ToLocalTime():h:mm:ss tt}");}
  ImGui.Separator();
  var board=adventureBoards.FirstOrDefault(b=>b.Id==adventureSelected);
  if(board is null){
   ImGui.BeginChild("AdventureChoices",new Vector2(0,0),false);
   ImGui.TextWrapped("Current adventures from your Journal. Build or resume an individual route there to share it here. Both windows share the current stop. Saved adventures update from Companion while the browser is closed. A connection to the paired service is required to save progress.");
   if(!adventureBoards.Any(AdventureInScope))ImGui.TextWrapped("No shared adventures yet. Open Adventure in the updated Journal and start or resume a route.");
   foreach(var b in adventureBoards.Where(AdventureInScope).OrderBy(b=>b.Kind).ThenBy(b=>b.Title)){
    ImGui.PushID(b.Id);ImGui.Spacing();ImGui.Separator();DrawAdventureHeading(b.Kind=="garden"?"gardening":"adventure",b.Title);
    ImGui.TextDisabled(b.Index>=b.Stops.Length?$"Round reviewed · {b.Stops.Length} stops":$"Stop {b.Index+1} of {b.Stops.Length} · {b.Stops[b.Index].Title}");
    if(ImGui.Button(b.Index>=b.Stops.Length?"Review adventure":"Resume adventure →")){adventureSelected=b.Id;adventurePrompt=null;adventureFollowSession.Reset();CacheAdventures();}
    ImGui.PopID();
   }ImGui.EndChild();return;
  }
  ImGui.TextUnformatted(board.Title);
  DrawAdventureLocation(board);
  ImGui.BeginDisabled(AdventureSaving||!adventureFresh);
  DrawAdventureNavigation(board);ImGui.EndDisabled();
  if(board.Protocol<2)ImGui.TextWrapped("This saved route still needs an upgrade. Reload Journal V7.11.177 or newer, sign in, and wait for Adventure pairing to finish. Then close it and press Refresh here.");
  var footerHeight=ImGui.GetFrameHeightWithSpacing()+ImGui.GetStyle().ItemSpacing.Y+2;
  ImGui.BeginChild("AdventureChecklist",new Vector2(0,-footerHeight),false);
  if(board.Index>=board.Stops.Length){ImGui.TextWrapped("Round reviewed. Unconfirmed work stays unconfirmed. You can revisit any stop or build another adventure in the Journal.");}
  else{
   var stop=board.Stops[board.Index];ImGui.PushID(board.Id+"|"+board.Generation+"|"+board.Index);ImGui.TextUnformatted(stop.Title);ImGui.TextWrapped(stop.Subtitle);
   if(ImGui.SmallButton("Copy stop"))ImGui.SetClipboardText(stop.Title+" · "+stop.Subtitle);
   var stepsOpen=false;
   if(board.Kind!="multi"){
    var characters=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>stop.CharacterIds?.Contains(c.Id)==true).ToArray();
    for(var i=0;i<characters.Length;i++){
     if(i==0)DrawAdventureCharacter(characters[i],stop,()=>{DrawAdventureHouseControls(stop);stepsOpen=DrawAdventureStepsHeader(stop.Tasks);});
     else DrawAdventureCharacter(characters[i],stop);
    }
    if(characters.Length==0){DrawAdventureHouseControls(stop);stepsOpen=DrawAdventureStepsHeader(stop.Tasks);}
   }else ImGui.ProgressBar(stop.Tasks.Length==0?0:(float)stop.Tasks.Count(t=>t.Done)/stop.Tasks.Length,new Vector2(-1,0),$"{stop.Tasks.Count(t=>t.Done)} / {stop.Tasks.Length} confirmed");
   if(board.Kind=="multi"){
    PrepareAdventureLayout(stop);
    foreach(var section in adventureCharacterTasks){
     ImGui.PushID("tasks-"+section.Character.Id);ImGui.Separator();var characterStepsOpen=false;
     DrawAdventureCharacter(section.Character,stop,()=>{if(section.Tasks.Length>0)characterStepsOpen=DrawAdventureStepsHeader(section.Tasks);});
     if(characterStepsOpen)DrawAdventureTaskList(board,stop,section.Tasks);
     ImGui.PopID();
    }
    if(adventureSharedTasks.Length>0){ImGui.Separator();ImGui.TextUnformatted("Shared tasks");DrawAdventureTaskSection(board,stop,adventureSharedTasks,true);}
   }else if(stepsOpen)DrawAdventureTaskList(board,stop,stop.Tasks);
   if(!string.IsNullOrEmpty(stop.HouseId))DrawAdventureGardens(stop.HouseId);
   var stopKey=board.Id+"|"+board.Generation+"|"+board.Index;
   bool? previous=adventureCompletion.TryGetValue(stopKey,out var old)?old:null;
   if(AdventurePolicy.Prompt(previous,stop))adventurePrompt=stopKey;
   adventureCompletion[stopKey]=AdventurePolicy.Complete(stop);ImGui.PopID();
  }
  ImGui.EndChild();
  var visibleStopKey=board.Id+"|"+board.Generation+"|"+board.Index;
  if(adventurePrompt==visibleStopKey&&!ImGui.IsPopupOpen("All done"))ImGui.OpenPopup("All done");
  ImGui.Separator();
  ImGui.BeginDisabled(AdventureSaving||!adventureFresh||board.Index<=0);if(ImGui.Button("← Previous"))AdventureAction(board,Math.Max(0,board.Index-1));ImGui.EndDisabled();ImGui.SameLine();
  ImGui.BeginDisabled(AdventureSaving||!adventureFresh||board.Index>=board.Stops.Length);if(ImGui.Button(board.Index==board.Stops.Length-1?"Review round":"Next stop →"))AdventureAction(board,board.Index+1);ImGui.EndDisabled();
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Navigation never marks tasks complete.");
  if(ImGui.BeginPopupModal("All done",ImGuiWindowFlags.AlwaysAutoResize)){
   ImGui.TextUnformatted("All done here. Go to the next stop?");
   if(ImGui.Button("Stay here")){adventurePrompt=null;ImGui.CloseCurrentPopup();}ImGui.SameLine();
   var currentKey=board.Id+"|"+board.Generation+"|"+board.Index;
   ImGui.BeginDisabled(AdventureSaving||!adventureFresh||adventurePrompt!=currentKey||board.Index>=board.Stops.Length||!AdventurePolicy.Complete(board.Stops[board.Index]));
   if(ImGui.Button("Next stop")){AdventureAction(board,board.Index+1);adventurePrompt=null;ImGui.CloseCurrentPopup();}ImGui.EndDisabled();ImGui.EndPopup();
  }
 }
}
