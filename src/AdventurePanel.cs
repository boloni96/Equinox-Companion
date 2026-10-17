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
  public AdventureWindow(Plugin p):base("Adventure · Equinox###EquinoxAdventure"){this.p=p;Size=new(740,590);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(510,330),MaximumSize=new(float.MaxValue)};AllowPinning=true;}
  public override void Draw()=>p.DrawAdventure();
 }
 private AdventureWindow adventureWindow=null!;private readonly AdventureSync adventureSync=new();private bool adventureCommand;
 private Task<AdventureReply>? adventureRequest;private bool adventureMutation;private string adventureRequestKey="",adventureConnectionKey="",adventureMessage="Checking your paired Journal…";
 private string adventurePerson="",adventureAccount="";
 private AdventureBoard[] adventureBoards=[];private string adventureSelected="";private DateTimeOffset adventureNext,adventureFetched;private bool adventureFresh;
 private readonly Dictionary<string,bool> adventureCompletion=[];private string? adventurePrompt;private bool adventureFollow=true;private readonly BidWorldSession adventureFollowSession=new();
 private static string AdventureText(string value)=>value.Replace("##","");
 private void InitializeAdventures(){adventureWindow=new(this);windows.AddWindow(adventureWindow);adventureCommand=Commands.AddHandler("/adventure",new CommandInfo((_,_)=>OpenAdventure()){HelpMessage="Open shared Garden and Character adventures from your paired Journal. /equinox adventure also works."});}
 private void DisposeAdventures(){if(adventureCommand)Commands.RemoveHandler("/adventure");windows.RemoveWindow(adventureWindow);adventureSync.Dispose();}
 private void OpenAdventure(){adventureWindow.IsOpen=true;adventureNext=default;nextRosterRead=default;}
 private void AdventureAction(AdventureBoard b,int? index=null,AdventureTask? task=null,bool value=false)
 {
  if(adventureRequest is not null||!adventureFresh)return;
  adventureMutation=true;adventureRequestKey=config.PairingKey;
  adventureRequest=index is {} n?adventureSync.Move(config.PairingKey,b,n):adventureSync.Check(config.PairingKey,b,task!.Id,value);
 }
 private void UpdateAdventure()
 {
  var now=DateTimeOffset.UtcNow;
  if(adventureConnectionKey!=config.PairingKey){adventureConnectionKey=config.PairingKey;adventureBoards=[];adventureSelected="";adventurePerson="";adventureAccount="";adventureCompletion.Clear();adventurePrompt=null;adventureFresh=false;adventureNext=default;}
  if(adventureRequest?.IsCompleted==true){
   var reply=adventureRequest.IsCompletedSuccessfully?adventureRequest.Result:new AdventureReply(null,"Adventure request failed.");
   if(adventureRequest.IsFaulted)_=adventureRequest.Exception;
   if(adventureRequestKey==config.PairingKey){adventureMessage=reply.Status;adventureFresh=reply.Boards is not null;if(reply.Boards is {} boards){adventureBoards=adventureMutation?adventureBoards.Where(b=>boards.All(n=>n.Id!=b.Id)).Concat(boards).ToArray():boards;adventureFetched=now;}
    adventureNext=now.AddSeconds(reply.Boards is null?5:3);
   }adventureRequest=null;adventureMutation=false;
  }
  if(adventureRequest is null&&config.SyncEnabled&&config.PairingKey.Length==64&&now>=adventureNext){adventureRequestKey=config.PairingKey;adventureRequest=adventureSync.Read(config.PairingKey);adventureNext=now.AddSeconds(3);}
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
 private void DrawAdventure()
 {
  UpdateAdventure();
  if(!config.SyncEnabled||config.PairingKey.Length!=64){ImGui.TextWrapped("Pair Companion with your Journal in Settings to open the same saved adventures here.");if(ImGui.Button("Open Companion settings"))visible=true;return;}
  ImGui.TextColored(new Vector4(.65f,.8f,1,1),"YOUR PEOPLE · YOUR WORLDS");
  if(ImGui.Button("Adventure home")){adventureSelected="";adventurePrompt=null;}
  ImGui.SameLine();if(ImGui.Button("Open Journal"))Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#adventure");
  ImGui.SameLine();ImGui.BeginDisabled(adventureRequest is not null);if(ImGui.Button("Refresh"))adventureNext=default;ImGui.EndDisabled();
  ImGui.TextWrapped(adventureMessage);
  if(adventureFetched!=default)ImGui.TextDisabled($"Last checked {adventureFetched.ToLocalTime():h:mm:ss tt} · same pairing key");
  DrawAdventureFilters();
  ImGui.Separator();
  var board=adventureBoards.FirstOrDefault(b=>b.Id==adventureSelected);
  if(board is null){
   ImGui.BeginChild("AdventureChoices",new Vector2(0,0),false);
   ImGui.TextWrapped("Current adventures from your Journal. Build or resume an individual route there to share it here. Both windows share the current stop. Keep the Journal open for live checklist updates and manual check confirmation.");
   if(!adventureBoards.Any(AdventureInScope))ImGui.TextWrapped("No shared adventures yet. Open Adventure in the updated Journal and start or resume a route.");
   foreach(var b in adventureBoards.Where(AdventureInScope).OrderBy(b=>b.Kind).ThenBy(b=>b.Title)){
    ImGui.PushID(b.Id);ImGui.Spacing();ImGui.Separator();ImGui.TextUnformatted(b.Title);
    ImGui.TextDisabled(b.Index>=b.Stops.Length?$"Round reviewed · {b.Stops.Length} stops":$"Stop {b.Index+1} of {b.Stops.Length} · {b.Stops[b.Index].Title}");
    if(ImGui.Button(b.Index>=b.Stops.Length?"Review adventure":"Resume adventure →")){adventureSelected=b.Id;adventurePrompt=null;adventureFollowSession.Reset();}
    ImGui.PopID();
   }ImGui.EndChild();return;
  }
  ImGui.TextUnformatted(board.Title);
  if(ImGui.Checkbox("Follow my character / world",ref adventureFollow))adventureFollowSession.Reset();
  if(adventureFollow&&adventureFresh&&adventureRequest is null){
   var ready=Player.IsLoaded&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
   var observation=adventureFollowSession.Observe(ready,Player.ContentId,Player.CurrentWorld.RowId,DateTimeOffset.UtcNow);
   if(observation.Changed&&observation.Ready){EnsureBidWorlds();var world=BidWorldName(observation.World);var actorId=Player.ContentId.ToString();var matches=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.ContentId==actorId).Take(2).ToArray();var cid=matches.Length==1?matches[0].Id:"";var next=AdventurePolicy.FollowIndex(board,cid,world);if(next is {} n)AdventureAction(board,n);}
  }
  ImGui.BeginDisabled(adventureRequest is not null||!adventureFresh);
  if(ImGui.BeginCombo("Jump to stop",board.Index>=board.Stops.Length?"Round reviewed":$"{board.Index+1} · {AdventureText(board.Stops[board.Index].Title)}")){
   for(var i=0;i<board.Stops.Length;i++){var stop=board.Stops[i];if(ImGui.Selectable($"{(AdventurePolicy.Complete(stop)?"✓ ":"")}{i+1} · {AdventureText(stop.Title)}##{i}",i==board.Index))AdventureAction(board,i);}ImGui.EndCombo();
  }ImGui.EndDisabled();
  ImGui.BeginChild("AdventureChecklist",new Vector2(0,-70),false);
  if(board.Index>=board.Stops.Length){ImGui.TextWrapped("Round reviewed. Unconfirmed work stays unconfirmed. You can revisit any stop or build another adventure in the Journal.");}
  else{
   var stop=board.Stops[board.Index];ImGui.TextUnformatted(stop.Title);ImGui.TextWrapped(stop.Subtitle);
   if(ImGui.SmallButton("Copy stop"))ImGui.SetClipboardText(stop.Title+" · "+stop.Subtitle);
   if(!string.IsNullOrEmpty(stop.HouseId)){
    ImGui.SameLine();if(ImGui.SmallButton("Garden guide")){OnPlantingCommand("/gardening","");}
    var house=config.SharedRoster?.People.SelectMany(p=>p.Characters).SelectMany(c=>c.Houses).FirstOrDefault(h=>h.Id==stop.HouseId);
    if(house is not null){ImGui.SameLine();if(ImGui.SmallButton("GO SEE")){EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==house.World);if(world.Id>0)GoSeeBid(world.Id,house.District,house.Ward,house.Plot);}
     if(!string.IsNullOrEmpty(bidTravelMessage))ImGui.TextWrapped(bidTravelMessage);}
   }
   ImGui.TextDisabled($"{stop.Tasks.Count(t=>t.Done)} / {stop.Tasks.Length} confirmed");
   foreach(var group in stop.Tasks.GroupBy(t=>t.Group)){
    ImGui.SetNextItemOpen(true,ImGuiCond.Once);
    if(!ImGui.CollapsingHeader(AdventureText(group.Key)))continue;
    foreach(var task in group){ImGui.PushID(task.Id);var pending=board.Pending?.LastOrDefault(p=>p.TaskId==task.Id);var check=pending?.Checked??task.Done;
     ImGui.BeginDisabled(!task.CanCheck||pending is not null||adventureRequest is not null||!adventureFresh);
     if(ImGui.Checkbox("##done",ref check))AdventureAction(board,task:task,value:check);ImGui.EndDisabled();ImGui.SameLine();ImGui.TextWrapped(task.Label);
     if(pending is not null)ImGui.TextColored(new Vector4(1,.8f,.4f,1),"Pending Journal confirmation");
     if(ImGui.SmallButton("Copy"))ImGui.SetClipboardText(task.Group+" · "+task.Label);ImGui.PopID();
    }
   }
   var stopKey=board.Id+"|"+board.Generation+"|"+board.Index;
   bool? previous=adventureCompletion.TryGetValue(stopKey,out var old)?old:null;
   if(AdventurePolicy.Prompt(previous,stop))adventurePrompt=stopKey;
   adventureCompletion[stopKey]=AdventurePolicy.Complete(stop);
   if(adventurePrompt==stopKey&&!ImGui.IsPopupOpen("All done"))ImGui.OpenPopup("All done");
  }
  ImGui.EndChild();ImGui.Separator();
  ImGui.BeginDisabled(adventureRequest is not null||!adventureFresh||board.Index<=0);if(ImGui.Button("← Previous"))AdventureAction(board,Math.Max(0,board.Index-1));ImGui.EndDisabled();ImGui.SameLine();
  ImGui.BeginDisabled(adventureRequest is not null||!adventureFresh||board.Index>=board.Stops.Length);if(ImGui.Button(board.Index==board.Stops.Length-1?"Review round":"Next stop →"))AdventureAction(board,board.Index+1);ImGui.EndDisabled();
  ImGui.TextDisabled("Navigation never marks tasks complete.");
  if(ImGui.BeginPopupModal("All done",ImGuiWindowFlags.AlwaysAutoResize)){
   ImGui.TextUnformatted("All done here. Go to the next stop?");
   if(ImGui.Button("Stay here")){adventurePrompt=null;ImGui.CloseCurrentPopup();}ImGui.SameLine();
   var currentKey=board.Id+"|"+board.Generation+"|"+board.Index;
   ImGui.BeginDisabled(adventureRequest is not null||!adventureFresh||adventurePrompt!=currentKey||board.Index>=board.Stops.Length||!AdventurePolicy.Complete(board.Stops[board.Index]));
   if(ImGui.Button("Next stop")){AdventureAction(board,board.Index+1);adventurePrompt=null;ImGui.CloseCurrentPopup();}ImGui.EndDisabled();ImGui.EndPopup();
  }
 }
}
