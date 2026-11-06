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
  public AdventureWindow(Plugin p):base("Adventure · Equinox###EquinoxAdventure"){this.p=p;RespectCloseHotkey=false;Size=new(540,530);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(360,280),MaximumSize=new(float.MaxValue)};AllowPinning=true;}
  public override void Draw()=>p.MeasureWindow(WindowName,DrawMeasuredContent);
        private void DrawMeasuredContent()=>p.DrawWindowWithLauncher(p.DrawAdventure);
  public override void PostDraw()=>p.MeasureWindow(WindowName,PostDrawMeasured,"PostDraw");
        private void PostDrawMeasured()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Adventure"));
 }
 private readonly WindowSystem adventureCutsceneWindows=new("EquinoxCompanion");
 private AdventureWindow adventureWindow=null!;private readonly AdventureSync adventureSync=new();private bool adventureCommand;
 private Task<AdventureReply>? adventureRequest;private bool adventureMutation;private string adventureRequestKey="",adventureConnectionKey="",adventureMessage="Checking your paired Journal…";
 private bool adventureFilterAll=true;
 private readonly HashSet<string> adventureFilterAccounts=[];
 private AdventureBoard? adventureFilterSource,adventureFilterBoard;
 private SharedRoster? adventureFilterRoster;
 private int[] adventureFilterIndices=[];
 private string adventureFilterKey="";
 private readonly AdventureViewState adventureView=new();
 private AdventureBoard[] adventureBoards=[];private string adventureSelected="";private DateTimeOffset adventureNext,adventureFetched;private bool adventureFresh;
 private readonly Dictionary<string,bool> adventureCompletion=[];private string? adventurePrompt;private bool adventureFollow=true;private readonly AdventureLocationSession adventureFollowSession=new();
 private static string AdventureText(string value)=>value.Replace("##","");
 private void InitializeAdventures(){adventureWindow=new(this);windows.AddWindow(adventureWindow);adventureCutsceneWindows.AddWindow(adventureWindow);Pi.UiBuilder.DisableCutsceneUiHide=true;adventureCommand=Commands.AddHandler("/adventure",new CommandInfo((_,_)=>OpenAdventure()){HelpMessage="Open shared Garden and Character adventures from your paired Journal. /equinox adventure also works."});}
 private void DisposeAdventures(){if(adventureCommand)Commands.RemoveHandler("/adventure");windows.RemoveWindow(adventureWindow);adventureCutsceneWindows.RemoveAllWindows();Pi.UiBuilder.DisableCutsceneUiHide=false;adventureSync.Dispose();DisposeAdventurePortraits();}
 private void OpenAdventure(){adventureWindow.IsOpen=true;adventureNext=default;nextRosterRead=default;}
 private (string Id,string Generation,int? Index,string? Task,bool Value)? adventureQueuedAction;
 private bool AdventureSaving=>adventureMutation||adventureQueuedAction is not null;
 private void AdventureAction(AdventureBoard b,int? index=null,AdventureTask? task=null,bool value=false)
 {
  if(index is {} localIndex){
   if(ReferenceEquals(b,adventureFilterBoard))localIndex=localIndex>=adventureFilterIndices.Length?adventureFilterSource!.Stops.Length:adventureFilterIndices[Math.Max(0,localIndex)];
   ClearAdventureFollowIntent();
   var current=adventureBoards.FirstOrDefault(x=>x.Id==b.Id&&x.Generation==b.Generation);
   if(current is null)return;
   var local=adventureView.Move(current,localIndex);
   adventureBoards=adventureBoards.Select(x=>x.Id==local.Id?local:x).ToArray();
   adventurePrompt=null;CacheAdventures();return;
  }
  if(AdventureSaving||!adventureFresh)return;
  if(adventureRequest is not null){adventureQueuedAction=(b.Id,b.Generation,index,task?.Id,value);return;}
  adventureMutation=true;adventureRequestKey=config.PairingKey;
  adventureRequest=adventureSync.Check(config.PairingKey,b,task!.Id,value);
 }
 private void UpdateAdventure()
 {
  var started=System.Diagnostics.Stopwatch.GetTimestamp();
  try{UpdateAdventureCore();}finally{RecordPerformanceElapsed("Adventure sync",started);}
 }
 private void UpdateAdventureCore()
 {
  var now=DateTimeOffset.UtcNow;
  if(adventureConnectionKey!=config.PairingKey){adventureConnectionKey=config.PairingKey;adventureView.Clear();ClearAdventureFollowIntent();adventureQueuedAction=null;adventureBoards=[];adventureSelected="";adventureFilterAll=true;adventureFilterAccounts.Clear();adventureCompletion.Clear();adventurePrompt=null;adventureFresh=false;adventureNext=default;
   var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.PairingKey)));
   if(config.AdventurePairingHash==hash){adventureBoards=config.CachedAdventures.Where(AdventurePolicy.Valid).Select(adventureView.Apply).ToArray();adventureSelected=config.SelectedAdventure;}
  }
  if(adventureRequest?.IsCompleted==true){
   var reply=adventureRequest.IsCompletedSuccessfully?adventureRequest.Result:new AdventureReply(null,"Adventure request failed.");
   if(adventureRequest.IsFaulted)_=adventureRequest.Exception;
   if(adventureRequestKey==config.PairingKey){adventureMessage=reply.Status;adventureFresh=reply.Boards is not null;if(reply.Boards is null){if(adventureFollowReply is {} retryFollow)adventurePendingTravel=retryFollow;adventureFollowReply=null;adventurePresenceSession.Reset();}if(reply.Boards is {} boards){adventureBoards=AdventurePolicy.Reconcile(adventureBoards,boards,adventureMutation).Select(adventureView.Apply).ToArray();adventureFetched=now;ApplyAdventureFollowReply();CacheAdventures();}
    adventureNext=now.AddSeconds(AdventurePollPolicy.Seconds(adventureWindow.IsOpen,gardenTendRoute!=null,reply.Boards is null));
   }adventureRequest=null;adventureMutation=false;
  }
  if(adventureRequest is null&&adventureQueuedAction is {} queued){
   adventureQueuedAction=null;
   var latest=adventureBoards.FirstOrDefault(b=>b.Id==queued.Id&&b.Generation==queued.Generation);
   var task=latest?.Stops.SelectMany(s=>s.Tasks).FirstOrDefault(t=>t.Id==queued.Task);
   if(adventureFresh&&latest is not null&&(queued.Index is {} index&&index>=0&&index<=latest.Stops.Length||task is not null&&AdventurePolicy.CanEdit(task)))AdventureAction(latest,queued.Index,task,queued.Value);
  }
  UpdateAdventurePresence(now);
  if(adventureRequest is null&&config.SyncEnabled&&config.PairingKey.Length==64&&now>=adventureNext){adventureRequestKey=config.PairingKey;adventureRequest=adventureSync.Read(config.PairingKey);adventureNext=now.AddSeconds(AdventurePollPolicy.Seconds(adventureWindow.IsOpen,gardenTendRoute!=null,false));}
 }

 private void CacheAdventures()
 {
  var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.PairingKey)));
  var old=config.CachedAdventures;
  if(config.AdventurePairingHash==hash&&old.Length==adventureBoards.Length&&old.Zip(adventureBoards).All(x=>x.First.Id==x.Second.Id&&x.First.Revision==x.Second.Revision&&x.First.Index==x.Second.Index)&&config.SelectedAdventure==adventureSelected)return;
  config.AdventurePairingHash=hash;config.CachedAdventures=adventureBoards;config.SelectedAdventure=adventureSelected;SaveConfiguration();
 }
 private readonly UiProjectionCache<AdventureProgress> adventureProgress=new(maxAgeMs:0);
 private void DrawAdventureTotal(AdventureBoard board)=>MeasureWork("Adventure: summary",()=>DrawAdventureTotalMeasured(board));
 private void DrawAdventureTotalMeasured(AdventureBoard board)
 {
  var progress=adventureProgress.Get(board,"",Environment.TickCount64,()=>AdventureProgress.Count(board));
  ImGui.ProgressBar(progress.Total==0?0:(float)progress.Done/progress.Total,new Vector2(-1,ImGui.GetFrameHeight()),$"{progress.Done} / {progress.Total} done");
  if(ImGui.IsItemHovered())ImGui.SetTooltip(board.Title+"\nCompleted activities in the displayed account selection. Shared activities count once.\n"+$"{progress.StopsDone} / {progress.Stops} stops complete. Visiting a stop does not complete its activities.");
 }
 private void DrawAdventureNavigation(AdventureBoard board,float width)
 {
  ImGui.SetNextItemWidth(width);
  if(board.Kind!="garden"){
   if(ImGui.BeginCombo("##WorldCharacter",board.Index>=board.Stops.Length?"Round reviewed":AdventureText(board.Stops[board.Index].Title))){
    for(var i=0;i<board.Stops.Length;i++){var s=board.Stops[i];if(ImGui.Selectable($"{s.Tasks.Count(t=>t.Done)}/{s.Tasks.Length} · {AdventureText(s.Title)}##stop{i}",i==board.Index))AdventureAction(board,i);}ImGui.EndCombo();
   }if(ImGui.IsItemHovered())ImGui.SetTooltip("World / character · destination for Bring a character here");return;
  }
  var current=board.Index<board.Stops.Length?board.Stops[board.Index]:board.Stops.FirstOrDefault();if(current is null)return;
  var worlds=board.Stops.Select(s=>s.World).Distinct().ToArray();
  if(ImGui.BeginCombo("##GardenWorld",AdventureText(current.World))){foreach(var world in worlds){var stops=board.Stops.Select((s,i)=>(Stop:s,Index:i)).Where(x=>x.Stop.World==world).ToArray();if(ImGui.Selectable($"{stops.Count(x=>AdventurePolicy.Complete(x.Stop))}/{stops.Length} · {AdventureText(world)}",world==current.World)){var next=stops.FirstOrDefault(x=>!AdventurePolicy.Complete(x.Stop));AdventureAction(board,next.Stop is null?stops[0].Index:next.Index);}}ImGui.EndCombo();}
  if(ImGui.IsItemHovered())ImGui.SetTooltip("World · "+current.World);
  string Label(AdventureStop stop){var names=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters.Where(c=>stop.CharacterIds?.Contains(c.Id)==true).Take(1).Select(c=>p.Name+" / "+c.Account+" / "+c.Name));return $"{stop.Tasks.Count(t=>t.Done)}/{stop.Tasks.Length} · {string.Join(", ",names)} · {stop.Title} · {stop.Subtitle}";}
  ImGui.SetNextItemWidth(width);
  if(ImGui.BeginCombo("##GardenHouse",AdventureText(Label(current)))){for(var i=0;i<board.Stops.Length;i++){var stop=board.Stops[i];if(stop.World!=current.World)continue;if(ImGui.Selectable(AdventureText(Label(stop))+"##house"+i,i==board.Index))AdventureAction(board,i);}ImGui.EndCombo();}
  if(ImGui.IsItemHovered())ImGui.SetTooltip(Label(current));
 }
 private static string AdventureAccountKey(string person,string account)=>person+"|"+account;
 private HashSet<string> AdventureFilterCharacters()=> (config.SharedRoster?.People??[])
  .SelectMany(p=>p.Characters.Where(c=>adventureFilterAll||adventureFilterAccounts.Contains(AdventureAccountKey(p.Id,c.AccountId))))
  .Select(c=>c.Id).ToHashSet();
 private bool AdventureInScope(AdventureBoard board)=>adventureFilterAll||board.Stops.Any(s=>s.CharacterIds?.Any(AdventureFilterCharacters().Contains)==true);
 private AdventureBoard FilterAdventure(AdventureBoard board)
 {
  if(adventureFilterAll)return board;
  var key=string.Join(",",adventureFilterAccounts.Order());
  if(ReferenceEquals(board,adventureFilterSource)&&ReferenceEquals(config.SharedRoster,adventureFilterRoster)&&key==adventureFilterKey)return adventureFilterBoard!;
  var ids=AdventureFilterCharacters();var characters=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).ToArray();
  var selected=board.Stops.Select((stop,index)=>(stop,index)).Where(x=>x.stop.CharacterIds?.Any(ids.Contains)==true).ToArray();
  var stops=selected.Select(x=>x.stop with {
   CharacterIds=x.stop.CharacterIds?.Where(ids.Contains).ToArray(),
   Subtitle=string.Join(", ",characters.Where(c=>x.stop.CharacterIds?.Contains(c.Id)==true&&ids.Contains(c.Id)).Select(c=>c.Name+" · "+c.Account)),
   Tasks=x.stop.Tasks.Where(t=>{var owner=AdventurePolicy.TaskCharacterId(t,characters);return owner.Length==0||ids.Contains(owner);}).ToArray()
  }).ToArray();
  adventureFilterSource=board;adventureFilterRoster=config.SharedRoster;adventureFilterKey=key;adventureFilterIndices=selected.Select(x=>x.index).ToArray();
  var index=board.Index>=board.Stops.Length?stops.Length:Array.IndexOf(adventureFilterIndices,board.Index);
  if(index<0)index=0;
  return adventureFilterBoard=board with {Stops=stops,Index=index};
 }
 private string AdventureFilterSummary()=>adventureFilterAll?"Showing: all people / accounts":"Showing: "+(adventureFilterAccounts.Count==0?"none":string.Join(", ",(config.SharedRoster?.People??[]).SelectMany(p=>(p.Accounts??[]).Where(a=>adventureFilterAccounts.Contains(AdventureAccountKey(p.Id,a.Id))).Select(a=>p.Name+" / "+a.Name))));
 private void DrawAdventureFilters()=>MeasureWork("Adventure: filters",()=>DrawAdventureFiltersMeasured());
 private void DrawAdventureFiltersMeasured()
 {
  if(ImGui.SmallButton("Select all accounts")){adventureFilterAll=true;adventureFilterAccounts.Clear();adventureFollowSession.Reset();}
  ImGui.SameLine();if(ImGui.SmallButton("Clear all")){adventureFilterAll=false;adventureFilterAccounts.Clear();adventureFollowSession.Reset();}
  var people=config.SharedRoster?.People??[];
  foreach(var person in people){
   ImGui.TextUnformatted(AdventureText(person.Name));
   foreach(var account in person.Accounts??[]){
    var key=AdventureAccountKey(person.Id,account.Id);var selected=adventureFilterAll||adventureFilterAccounts.Contains(key);
    if(ImGui.Checkbox(AdventureText(account.Name)+"##scope"+key,ref selected)){
     if(adventureFilterAll){foreach(var p in people)foreach(var a in p.Accounts??[])adventureFilterAccounts.Add(AdventureAccountKey(p.Id,a.Id));adventureFilterAll=false;}
     if(selected)adventureFilterAccounts.Add(key);else adventureFilterAccounts.Remove(key);
     adventureFollowSession.Reset();adventurePrompt=null;ClearAdventureFollowIntent();
    }
   }
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
 // Presentation only: retain the original task, IDs and confirmation state for syncing.
 private static string AdventureTaskLabel(AdventureTask task)
 {
  return AdventureTaskPresentation.Split(task.Label,task.Rule?.Kind).Label;
 }
 private static string AdventureTaskHint(AdventureTask task)=>task.Rule?.Kind switch {
  "event"=>"Complete event activity.",
  "reward"=>"Collect reward.",
  _=>""
 };
 private void DrawAdventureTaskList(AdventureBoard board,AdventureStop stop,AdventureTask[] tasks)=>MeasureWork("Adventure: task list",()=>DrawAdventureTaskListMeasured(board,stop,tasks));
 private void DrawAdventureTaskListMeasured(AdventureBoard board,AdventureStop stop,AdventureTask[] tasks)
 {
    PrepareAdventureLayout(stop);
    foreach(var task in tasks){ImGui.PushID(task.Id);
     var house=AdventurePolicy.IsHouseVisit(task)?adventureLayoutHouses.GetValueOrDefault(AdventurePolicy.TaskHouseId(task)):null;
     var label=house is null?AdventureTaskLabel(task):$"{(task.Done?"Entered":"Enter")} · {house.Name} · {house.District} W{house.Ward} P{house.Plot}";var pending=board.Pending?.LastOrDefault(p=>p.TaskId==task.Id);var check=task.ServerConfirmed?task.Done:pending?.Checked??task.Done;
     ImGui.BeginDisabled(!AdventurePolicy.CanEdit(task)||(board.Protocol<2&&pending is not null)||AdventureSaving||!adventureFresh);
     if(ImGui.Checkbox("##done",ref check))AdventureAction(board,task:task,value:check);ImGui.EndDisabled();
     var hover=ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);ImGui.SameLine();
     ImGui.TextWrapped(label);
     var gardenLink=AdventureGardenNavigation.TaskTarget(task,stop);
     if(ImGui.IsItemClicked()){if(gardenLink is {} clicked)adventureOpenBed=clicked;else ImGui.SetClipboardText(label);}
     if(hover||ImGui.IsItemHovered()){ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*32);ImGui.TextUnformatted((gardenLink is {} link?link.Bed>0?"Click to open this bed’s batch.\n":"Click to open this batch.\n":"Click to copy this label.\n")+(AdventureTaskHint(task) is {Length:>0} hint?hint+"\n":"")+(AdventureTaskPresentation.Split(task.Label,task.Rule?.Kind).Details is {Length:>0} details?details+"\n":"")+(task.Status.Length>0?DisplayTime.InText(task.Status):task.Done?"Completion recorded in the Journal":"Waiting for completion")+(task.ServerConfirmed?"\nConfirmed automatically from game records. This check cannot be removed manually.":!task.CanCheck?"\nThis task updates automatically.":"\nManual completion can be changed here."));ImGui.PopTextWrapPos();ImGui.EndTooltip();}
     if(!check&&(house is not null||AdventurePolicy.IsFashion(task))){
      var right=ImGui.GetItemRectMax().X+ImGui.GetStyle().ItemSpacing.X;
      if(right+ImGui.CalcTextSize("Go").X+ImGui.GetStyle().FramePadding.X*2<ImGui.GetCursorScreenPos().X+ImGui.GetContentRegionAvail().X)ImGui.SameLine();
      ImGui.BeginDisabled(!Player.IsLoaded);
      if(ImGui.SmallButton("Go")){characterTravelStatusOwner=task.Rule?.Cid??"";if(house is not null)VisitSharedHouse(house);else GoToAdventureFashion();}
      ImGui.EndDisabled();
      if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(house is null?"Teleport your current character to the Gold Saucer for the Fashion Report. Requires an unlocked destination.":$"Go to {house.Name} · {house.World} · {house.District} W{house.Ward} P{house.Plot}.\nUses the current character. Enter the house to confirm the visit.");
     }
     DrawAdventureMarketActions(task);
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
  var started=System.Diagnostics.Stopwatch.GetTimestamp();
  try{DrawAdventureCore();}finally{RecordPerformanceElapsed("Adventure window",started);}
 }
 private void DrawAdventureCore()
 {
  if(!config.SyncEnabled||config.PairingKey.Length!=64){ImGui.TextWrapped("Pair Companion with your Journal in Settings to open the same saved adventures here.");if(ImGui.Button("Open Companion settings"))visible=true;return;}
  if(ImGui.Button("Item delivery…"))OpenItemDelivery();ImGui.SameLine();
  if(ImGui.Button("Adventure home")){adventureSelected="";adventurePrompt=null;}
  SameLineIfFits(ButtonWidth("Open Journal"));if(ImGui.Button("Open Journal"))Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#adventure");
  if(!adventureFresh)ImGui.TextWrapped(adventureMessage);
  if(ImGui.CollapsingHeader("People, accounts & connection")){ImGui.BeginDisabled(adventureRequest is not null);if(ImGui.SmallButton("Refresh"))adventureNext=default;ImGui.EndDisabled();DrawAdventureFilters();ImGui.TextWrapped(adventureMessage);if(adventureFetched!=default)ImGui.TextDisabled($"Last checked {adventureFetched.ToLocalTime():h:mm:ss tt}");}
  ImGui.TextWrapped(AdventureFilterSummary());
  ImGui.Separator();
  var board=adventureBoards.FirstOrDefault(b=>b.Id==adventureSelected);
  if(board is null){
   ImGui.BeginChild("AdventureChoices",new Vector2(0,0),false);
   ImGui.TextWrapped("Current adventures from your Journal. Build or resume a Character & Multi Adventure there to share it here. Each game client keeps its own current stop; task progress is shared. Saved adventures update from Companion while the browser is closed. A connection to the paired service is required to save progress.");
   if(!adventureBoards.Any(AdventureInScope))ImGui.TextWrapped("No shared adventures yet. Open Adventure in the updated Journal and start or resume a route.");
   foreach(var b in adventureBoards.Where(AdventureInScope).OrderBy(b=>b.Kind).ThenBy(b=>b.Title)){
    ImGui.PushID(b.Id);ImGui.Spacing();ImGui.Separator();DrawAdventureHeading(b.Kind=="garden"?"gardening":"adventure",b.Title);
    ImGui.TextDisabled(b.Index>=b.Stops.Length?$"Round reviewed · {b.Stops.Length} stops":$"Stop {b.Index+1} of {b.Stops.Length} · {b.Stops[b.Index].Title}");
    if(ImGui.Button(b.Index>=b.Stops.Length?"Review adventure":"Resume adventure →")){adventureSelected=b.Id;adventurePrompt=null;adventureFollowSession.Reset();CacheAdventures();}
    ImGui.PopID();
   }ImGui.EndChild();return;
  }
  board=FilterAdventure(board);
  if(board.Stops.Length==0){ImGui.TextWrapped("No characters in this adventure match the selected accounts.");return;}
  DrawTendAdventure(board);
  DrawAdventureTotal(board);
  DrawAdventureLocation(board);
  // Resolve game selection before any collapsed house/character section is drawn.
  FollowAdventureGarden(board);
  if(board.Protocol<2)ImGui.TextWrapped("This saved route still needs an upgrade. Reload Journal V7.11.177 or newer, sign in, and wait for Adventure pairing to finish. Then close it and press Refresh here.");
  ImGui.BeginChild("AdventureChecklist",new Vector2(0,0),false);
  if(board.Index>=board.Stops.Length){ImGui.TextWrapped("Round reviewed. Unconfirmed work stays unconfirmed. You can revisit any stop or build another adventure in the Journal.");}
  else{
   var stop=board.Stops[board.Index];ImGui.PushID(board.Id+"|"+board.Generation+"|"+board.Index);ImGui.TextWrapped(stop.Title+(stop.Subtitle.Length>0?" · "+stop.Subtitle:""));CopyTextOnClick(stop.Title+" · "+stop.Subtitle);
   var stepsOpen=false;
   if(board.Kind!="multi"&&!string.IsNullOrEmpty(stop.HouseId))stepsOpen=DrawAdventureHouseOwner(stop);
   else if(board.Kind!="multi"){
    var characters=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>stop.CharacterIds?.Contains(c.Id)==true).ToArray();
    for(var i=0;i<characters.Length;i++){
     if(i==0)DrawAdventureCharacter(characters[i],stop,()=>{DrawAdventureHouseControls(stop);stepsOpen=DrawAdventureStepsHeader(stop.Tasks);});
     else DrawAdventureCharacter(characters[i],stop);
    }
    if(characters.Length==0){DrawAdventureHouseControls(stop);stepsOpen=DrawAdventureStepsHeader(stop.Tasks);}
   }else ImGui.TextDisabled($"This stop · {stop.Tasks.Count(t=>t.Done)} / {stop.Tasks.Length} confirmed");
   if(board.Kind=="multi"){
    PrepareAdventureLayout(stop);
    foreach(var section in adventureCharacterTasks){
     ImGui.PushID("tasks-"+section.Character.Id);ImGui.Separator();var characterStepsOpen=false;
     DrawAdventureCharacter(section.Character,stop,()=>{if(section.Tasks.Length>0)characterStepsOpen=DrawAdventureStepsHeader(section.Tasks);});
     if(characterStepsOpen)DrawAdventureTaskList(board,stop,section.Tasks);
     DrawAdventureMultiGardens(section.Tasks);
     ImGui.PopID();
    }
    if(adventureSharedTasks.Length>0){ImGui.Separator();ImGui.TextUnformatted("Shared tasks");DrawAdventureTaskSection(board,stop,adventureSharedTasks,true);DrawAdventureMultiGardens(adventureSharedTasks);}
   }else if(stepsOpen)DrawAdventureTaskList(board,stop,stop.Tasks);
   if(!string.IsNullOrEmpty(stop.HouseId)){
    if(board.Kind!="multi")DrawAdventureHouseMembers(stop);
    DrawAdventureGardens(stop.HouseId);
   }
   var stopKey=board.Id+"|"+board.Generation+"|"+board.Index;
   bool? previous=adventureCompletion.TryGetValue(stopKey,out var old)?old:null;
   if(AdventurePolicy.Prompt(previous,stop))adventurePrompt=stopKey;
   adventureCompletion[stopKey]=AdventurePolicy.Complete(stop);ImGui.PopID();
  }
  ImGui.EndChild();
  var visibleStopKey=board.Id+"|"+board.Generation+"|"+board.Index;
  if(adventurePrompt==visibleStopKey&&!ImGui.IsPopupOpen("All done"))ImGui.OpenPopup("All done");
  windowStatus=bringMessage.Length>0?bringMessage:adventureMessage;
  windowFooter=()=>{
  ImGui.BeginDisabled(board.Index<=0);if(ImGui.Button("← Previous"))AdventureAction(board,Math.Max(0,board.Index-1));ImGui.EndDisabled();ImGui.SameLine();
  ImGui.BeginDisabled(board.Index>=board.Stops.Length);if(ImGui.Button(board.Index==board.Stops.Length-1?"Review round":"Next stop →"))AdventureAction(board,board.Index+1);ImGui.EndDisabled();
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Navigation changes only this game client’s page. Task progress remains shared.");
  };
  if(ImGui.BeginPopupModal("All done",ImGuiWindowFlags.AlwaysAutoResize)){
   ImGui.TextUnformatted("All done here. Go to the next stop?");
   if(ImGui.Button("Stay here")){adventurePrompt=null;ImGui.CloseCurrentPopup();}ImGui.SameLine();
   var currentKey=board.Id+"|"+board.Generation+"|"+board.Index;
   ImGui.BeginDisabled(adventurePrompt!=currentKey||board.Index>=board.Stops.Length||!AdventurePolicy.Complete(board.Stops[board.Index]));
   if(ImGui.Button("Next stop")){AdventureAction(board,board.Index+1);adventurePrompt=null;ImGui.CloseCurrentPopup();}ImGui.EndDisabled();ImGui.EndPopup();
  }
 }
}






