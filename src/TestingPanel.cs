using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
 private sealed class TestingWindow:Window
 {
  private readonly Plugin p;
  public TestingWindow(Plugin p):base("Testing checklist · Equinox###EquinoxTests"){this.p=p;RespectCloseHotkey=false;Size=new(760,570);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(500,340),MaximumSize=new(float.MaxValue)};AllowPinning=true;}
  public override void Draw()=>p.DrawWindowWithLauncher(p.DrawTesting);
  public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Testing"));
 }
 private TestingWindow testingWindow=null!;private readonly TestingSync testingSync=new();private bool testingCommand;
 private string testingPairingKey="";
 private TestingLocal testingState=new();private string testingScope="",testingRequestScope="",testingRequestId="",testingMessage="Open the checklist to connect.",testingReceived="";
 private Task<TestingResponse>? testingRequest;private DateTimeOffset testingNext,testingPoll;
 private string testingSearch="",testingCategory="",testingFilter="",testingName="",testingNotes="",testingSelected="";private bool testingArchive;
 private PlayTest[] testingVisible=[];private readonly Dictionary<string,(string Status,bool Retest)> testingSummaries=[];
 private Task<string>? testingExport;private string testingExportPath="";
 private void InitializeTesting(){testingWindow=new(this);windows.AddWindow(testingWindow);testingCommand=Commands.AddHandler("/eqtests",new CommandInfo((_,_)=>OpenTesting()){HelpMessage="Open shared tester checklists. /equinox tests also works."});}
 private void DisposeTesting(){if(testingCommand)Commands.RemoveHandler("/eqtests");windows.RemoveWindow(testingWindow);testingSync.Dispose();}
 private void OpenTesting(){testingWindow.IsOpen=true;testingNext=default;}
 private void SaveTesting(){if(testingScope.Length==0)return;config.TestingByPairing[testingScope]=testingState;SaveConfiguration();}
 private void RebuildTesting(){testingSummaries.Clear();foreach(var t in testingState.Catalog?.Tests??[])testingSummaries[t.Id]=(TestingPolicy.Summary(t,testingState.Reports??[]),TestingPolicy.NeedsRetest(t,testingState.Reports??[]));FilterTesting();}
 private void FilterTesting()=>testingVisible=(testingState.Catalog?.Tests??[]).Where(t=>(testingArchive||!t.Archived)&&(testingCategory.Length==0||t.Category==testingCategory)&&(testingFilter.Length==0||(testingFilter=="retest"?testingSummaries[t.Id].Retest:testingSummaries[t.Id].Status==testingFilter))&&(testingSearch.Length==0||(t.Id+" "+t.Title+" "+t.Steps).Contains(testingSearch,StringComparison.OrdinalIgnoreCase))).ToArray();
 private void UpdateTesting(DateTimeOffset now)
 {
  if(now<testingPoll)return;testingPoll=now.AddMilliseconds(250);
  var scope=testingScope;
  if(testingPairingKey!=config.PairingKey){testingPairingKey=config.PairingKey;scope=config.PairingKey.Length==64?Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.PairingKey))):"";}
  if(scope!=testingScope){testingScope=scope;testingState=config.TestingByPairing.GetValueOrDefault(scope)??new();testingSelected="";testingNotes="";testingNext=default;RebuildTesting();}
  if(testingExport?.IsCompleted==true){try{testingExportPath=testingExport.Result;testingMessage="Checklist exported. Open exports folder to share it.";}catch{testingMessage="Checklist export failed. Try again.";}testingExport=null;}
  if(testingRequest?.IsCompleted==true){var reply=testingRequest.IsCompletedSuccessfully?testingRequest.Result:new TestingResponse(Error:"Testing request failed; your report is kept.");if(testingRequest.IsFaulted)_=testingRequest.Exception;
   if(testingRequestScope==testingScope){
    var before=testingState;
    if(reply.Page is {} page){testingState=TestingPolicy.Apply(testingState,page);testingReceived=now.ToLocalTime().ToString("g");testingMessage=(testingState.Pending??[]).Any(p=>p.Blocked.Length>0)?"Needs review · a saved test report conflicts":page.More?"Loading report history…":"Synced · results received from Cloudflare";}
    if(reply.Report is {} report){testingState=TestingPolicy.Acknowledge(testingState,report);testingMessage="Synced · report saved in Cloudflare";}
    if(reply.Error.Length>0){testingMessage=reply.Error;if(reply.Conflict&&testingRequestId.Length>0)testingState=testingState with {Pending=(testingState.Pending??[]).Select(p=>p.Report.Id==testingRequestId?p with {Blocked=reply.Error}:p).ToArray()};}
    if(!ReferenceEquals(before,testingState)){SaveTesting();RebuildTesting();}
    testingNext=now.AddSeconds(reply.Error.Length>0?10:reply.Page?.More==true||reply.Report is not null?0:5);
   }
   testingRequest=null;testingRequestId="";
  }
  if(testingRequest is not null||!config.SyncEnabled||scope.Length==0||now<testingNext)return;
  var pending=(testingState.Pending??[]).FirstOrDefault(p=>p.Blocked.Length==0);
  if(!testingWindow.IsOpen&&pending is null)return;
  testingRequestScope=scope;testingRequestId=pending?.Report.Id??"";
  testingRequest=pending is not null?testingSync.Send(config.PairingKey,pending.Report):testingSync.Read(config.PairingKey,testingState.Cursor);testingNext=now.AddSeconds(5);
 }
 private IEnumerable<(string Id,string Name)> TestingPeople()=>(config.SharedRoster?.People??[]).Select(p=>(Id:"person:"+p.Id,Name:p.Name)).Concat((testingState.Reports??[]).Select(r=>(Id:r.TesterId,Name:r.TesterName))).Concat(config.TestingTesterId.Length>0?[(config.TestingTesterId,config.TestingTesterName)]:Array.Empty<(string,string)>()).DistinctBy(p=>p.Item1);
 private void SelectTest(PlayTest t){testingSelected=t.Id;var mine=TestingPolicy.Current(t,testingState.Reports??[]).FirstOrDefault(r=>r.TesterId==config.TestingTesterId);testingNotes=(testingState.Pending??[]).FirstOrDefault(p=>p.Report.TestId==t.Id&&p.Report.TesterId==config.TestingTesterId)?.Report.Notes??config.TestingDrafts.GetValueOrDefault(TestingDraftKey(t))??mine?.Notes??"";}
 private string TestingDraftKey(PlayTest t)=>testingScope+"|"+config.TestingTesterId+"|"+t.Id+"|"+t.Revision;
 private void DrawTesting()
 {
  windowStatus=testingMessage;windowFooter=()=>{if(ImGui.SmallButton("Export results")&&testingExport is null)ExportTesting();ImGui.SameLine();if(ImGui.SmallButton("Open exports folder")){var path=Path.Combine(Pi.GetPluginConfigDirectory(),"exports");Directory.CreateDirectory(path);Dalamud.Utility.Util.OpenLink(path);}if(ImGui.IsItemHovered())ImGui.SetTooltip("The export includes test definitions, all downloaded reports, notes, versions and local pending results. Share it for review; no pairing key or game records are included.");};
  if(scopeMissing()){ImGui.TextWrapped("Connect to your shared Journal in Settings to receive the testing checklist.");if(ImGui.Button("Open Settings"))OpenSettings();return;}
  ImGui.SetNextItemWidth(190);
  if(ImGui.BeginCombo("Testing as",config.TestingTesterName.Length>0?AdventureText(config.TestingTesterName):"Choose tester")){
   foreach(var p in TestingPeople())if(ImGui.Selectable(AdventureText(p.Name)+"##"+p.Id,p.Id==config.TestingTesterId)){config.TestingTesterId=p.Id;config.TestingTesterName=p.Name;SaveConfiguration();var selected=testingState.Catalog?.Tests.FirstOrDefault(t=>t.Id==testingSelected);if(selected is not null)SelectTest(selected);}
   ImGui.EndCombo();
  }
  if(ImGui.IsItemHovered())ImGui.SetTooltip("Your tester label is shared. Each tester keeps a separate result; select the same label on your other client or in the Journal.");
  ImGui.SameLine();if(ImGui.SmallButton("New tester"))ImGui.OpenPopup("New test participant");
  if(ImGui.BeginPopup("New test participant")){ImGui.InputText("Display name",ref testingName,100);if(ImGui.Button("Use this name")&&testingName.Trim().Length>0){var chosenName=testingName.Trim();var chosenId=TestingPeople().FirstOrDefault(p=>string.Equals(p.Name,chosenName,StringComparison.OrdinalIgnoreCase)).Id??"tester:"+Guid.NewGuid().ToString();config.TestingTesterName=chosenName;config.TestingTesterId=chosenId;SaveConfiguration();testingSelected="";ImGui.CloseCurrentPopup();}ImGui.EndPopup();}
  ImGui.SetNextItemWidth(210);if(ImGui.InputTextWithHint("##testSearch","Find a test",ref testingSearch,150))FilterTesting();ImGui.SameLine();
  ImGui.SetNextItemWidth(160);if(ImGui.BeginCombo("##testCategory",testingCategory.Length==0?"All categories":testingCategory)){if(ImGui.Selectable("All categories",testingCategory.Length==0)){testingCategory="";FilterTesting();}foreach(var category in (testingState.Catalog?.Tests??[]).Select(t=>t.Category).Distinct())if(ImGui.Selectable(category,testingCategory==category)){testingCategory=category;FilterTesting();}ImGui.EndCombo();}
  ImGui.SameLine();ImGui.SetNextItemWidth(130);if(ImGui.BeginCombo("##testStatus",testingFilter.Length==0?"All results":testingFilter=="retest"?"Needs retest":TestingPolicy.Label(testingFilter))){foreach(var f in new[]{"","not-tested","pass","fail","later","retest"})if(ImGui.Selectable(f==""?"All results":f=="retest"?"Needs retest":TestingPolicy.Label(f),testingFilter==f)){testingFilter=f;FilterTesting();}ImGui.EndCombo();}
  if(ImGui.Checkbox("Previous reports",ref testingArchive))FilterTesting();ImGui.SameLine();ImGui.TextDisabled($"{testingVisible.Length} tests · {(testingState.Pending??[]).Length} pending");if(ImGui.IsItemHovered())ImGui.SetTooltip("Manual testing results · not game activity completion.\nLast received: "+(testingReceived.Length>0?testingReceived:"not yet")+"\nCompanion "+typeof(Plugin).Assembly.GetName().Version+" · Journal "+testingState.ServerVersion);
  if(testingState.Catalog is null){ImGui.TextWrapped(testingMessage);return;}
  if(ImGui.BeginChild("TestingRows",Vector2.Zero,false))foreach(var t in testingVisible){
   var info=testingSummaries[t.Id];ImGui.SetNextItemOpen(t.Id==testingSelected,ImGuiCond.Always);
   if(ImGui.CollapsingHeader(t.Id+" · "+AdventureText(t.Title)+" · "+(t.Archived?"Prior evidence":info.Retest?"Needs retest":TestingPolicy.Label(info.Status))+"###test-"+t.Id)){
    if(testingSelected!=t.Id)SelectTest(t);
    DrawTestDetails(t);
   }else if(testingSelected==t.Id)testingSelected="";
  }ImGui.EndChild();
  bool scopeMissing()=>testingScope.Length==0||!config.SyncEnabled;
 }
 private void DrawTestDetails(PlayTest t)
 {
  ImGui.TextWrapped(t.Steps);ImGui.TextDisabled(t.When+" · revision "+t.Revision);
  var mine=TestingPolicy.Current(t,testingState.Reports??[]).FirstOrDefault(r=>r.TesterId==config.TestingTesterId);var pending=(testingState.Pending??[]).FirstOrDefault(p=>p.Report.TestId==t.Id&&p.Report.TesterId==config.TestingTesterId);
  ImGui.TextUnformatted(pending is null?"Your result: "+TestingPolicy.Label(mine?.Status??""):pending.Blocked.Length>0?"Needs review · your draft is kept":"Saving… · kept on this device");
  if(ImGui.InputTextMultiline("##testNotes"+t.Id,ref testingNotes,4000,new Vector2(-1,58))){config.TestingDrafts[TestingDraftKey(t)]=testingNotes;SaveConfiguration();}
  ImGui.BeginDisabled(t.Archived||config.TestingTesterId.Length==0||pending is not null);
  foreach(var result in new[]{"pass","fail","later","not-tested"}){if(result!="pass")ImGui.SameLine();if(ImGui.Button(TestingPolicy.Label(result)+"##"+t.Id))QueueTest(t,result);}
  ImGui.EndDisabled();
  if(pending?.Blocked.Length>0){ImGui.TextWrapped(pending.Blocked);if(ImGui.Button("Keep my draft and retry##"+t.Id)){testingState=testingState with {Pending=(testingState.Pending??[]).Where(p=>p.Report.Id!=pending.Report.Id).ToArray()};QueueTest(t,pending.Report.Status);}if(ImGui.IsItemHovered())ImGui.SetTooltip("Read the latest tester results below first. Saves your notes as a new report against the current result and test revision.");}
  if(ImGui.TreeNode("Tester results & history##"+t.Id)){foreach(var r in (testingState.Reports??[]).Where(r=>r.TestId==t.Id).Reverse()){ImGui.TextWrapped(r.TesterName+" · "+TestingPolicy.Label(r.Status)+" · "+(DateTimeOffset.TryParse(r.At,out var at)?at.ToLocalTime().ToString("g"):r.At));if(ImGui.IsItemHovered())ImGui.SetTooltip($"Test revision {r.TestRevision} · {r.Source}\nCompanion: {r.PluginVersion}\nJournal: {r.JournalVersion}\nServer: {r.WorkerVersion}");if(r.Notes.Length>0)ImGui.TextWrapped(r.Notes);}ImGui.TreePop();}
 }
 private void QueueTest(PlayTest test,string status)
 {
  var mine=TestingPolicy.Current(test,testingState.Reports??[]).FirstOrDefault(r=>r.TesterId==config.TestingTesterId);
  var report=new TestReport(Guid.NewGuid().ToString(),test.Id,test.Revision,config.TestingTesterId,config.TestingTesterName,status,testingNotes,typeof(Plugin).Assembly.GetName().Version?.ToString()??"",testingState.ServerVersion,testingState.ServerVersion,"plugin",mine?.Id??"");
  testingState=testingState with {Pending=[..testingState.Pending??[],new(report)]};SaveTesting();testingMessage="Saving… · report queued locally";testingNext=default;
 }
 private void ExportTesting()
 {
  var snapshot=new {format="equinox-playtest-export",schema=1,exportedAt=DateTimeOffset.UtcNow,completeHistory=testingState.Complete,catalog=testingState.Catalog,reports=testingState.Reports??[],pending=testingState.Pending??[],drafts=config.TestingDrafts.Where(x=>x.Key.StartsWith(testingScope+"|",StringComparison.Ordinal)).ToDictionary(x=>x.Key[(testingScope.Length+1)..],x=>x.Value),versions=new{plugin=typeof(Plugin).Assembly.GetName().Version?.ToString(),server=testingState.ServerVersion}};
  var path=Path.Combine(Pi.GetPluginConfigDirectory(),"exports","Equinox-Test-Results-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".json");
  testingExport=Task.Run(()=>{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(snapshot,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));return path;});testingMessage="Exporting checklist…";
 }
}
