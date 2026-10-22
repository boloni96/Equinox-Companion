using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private Action? windowFooter;
 private string windowStatus="";
 private readonly Dictionary<System.Reflection.MethodInfo,string> windowPerformanceLabels=[];
 // One shared footer: actions and status on the left, launcher on the right.
 private void DrawWindowWithLauncher(Action content)
 {
  RememberItemMenuWindow();windowFooter=null;windowStatus="";
  var height=Math.Max(32,ImGui.GetFrameHeight());
  var start=ImGui.GetCursorPos();var available=ImGui.GetContentRegionAvail();
  var contentHeight=Math.Max(1,available.Y-height-ImGui.GetStyle().ItemSpacing.Y);
  var visible=ImGui.BeginChild("EquinoxWindowContent",new Vector2(0,contentHeight),false);
  try
  {
   if(visible)
   {
    if(!windowPerformanceLabels.TryGetValue(content.Method,out var stage))windowPerformanceLabels[content.Method]=stage="Window: "+(content.Method.Name switch
    {
     "DrawAdventure"=>"Adventure","DrawGardenPlansWindow"=>"Gardening","DrawBidSearch"=>"Housing bids",
     "DrawMarketSearch"=>"Where is it cheapest?","DrawItemSearch"=>"Do I already have it?",
     "DrawContent"=>content.Target?.GetType().Name switch {"FashionReportWindow"=>"Fashion Report","WelcomeWindow"=>"Welcome","NewCharacterWindow"=>"New character",var type=>type??"Content"},var name=>name
    });
    var started=System.Diagnostics.Stopwatch.GetTimestamp();
    try{content();}finally{RecordPerformanceElapsed(stage,started);}
   }
  }
  finally{ImGui.EndChild();}
  ImGui.SetCursorPos(new Vector2(start.X,start.Y+contentHeight+ImGui.GetStyle().ItemSpacing.Y));
  if(windowFooter is {} footer){footer();ImGui.SameLine();}
  if(windowStatus.Length>0){
   var remaining=ImGui.GetContentRegionAvail().X-height-ImGui.GetStyle().ItemSpacing.X;
   if(remaining>12){var full=windowStatus.Replace("\n"," ");var shown=full;while(shown.Length>0&&ImGui.CalcTextSize(shown+"…").X>remaining)shown=shown[..^1];if(shown!=full)shown+="…";ImGui.TextUnformatted(shown);if(ImGui.IsItemHovered())ImGui.SetTooltip(windowStatus);ImGui.SameLine();}
  }
  DrawWindowLauncher();
 }
 private void DrawWindowLauncher()
 {
  var width=Math.Max(32,ImGui.GetFrameHeight());
  ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(),ImGui.GetCursorPosX()+ImGui.GetContentRegionAvail().X-width));
  var launcherAt=ImGui.GetCursorScreenPos();
  if(ImGui.InvisibleButton("Open windows##GoddessOptions",new Vector2(width)))ImGui.OpenPopup("EquinoxWindows");
  if(ImGui.IsItemHovered())ImGui.SetTooltip("Open windows · Adventure, market, items, housing and more");
  GardenImage("assets/category-icons/options.png",launcherAt,new Vector2(width));
  if(!ImGui.BeginPopup("EquinoxWindows"))return;
  ImGui.TextDisabled("OPEN A WINDOW");ImGui.Separator();
  void Row(string name,string command,string icon,Action open,bool isOpen=false){
   if(isOpen)return;
   var size=ImGui.GetTextLineHeight()*1.65f;var at=ImGui.GetCursorScreenPos();ImGui.Dummy(new Vector2(size));
   GardenImage("assets/category-icons/"+icon+".png",at,new Vector2(size));
   ImGui.SameLine();if(ImGui.Selectable(name,false,ImGuiSelectableFlags.None,new Vector2(250,size))){open();ImGui.CloseCurrentPopup();}if(ImGui.IsItemHovered())ImGui.SetTooltip(command);
  }
  Row("Companion","/equinox","characters",()=>{visible=true;mainWindow.IsOpen=true;},mainWindow.IsOpen);
  Row("Testing checklist","/eqtests · /equinox tests","adventure",OpenTesting,testingWindow.IsOpen);
  Row("Adventure","/adventure","adventure",OpenAdventure,adventureWindow.IsOpen);
  Row("Where is it cheapest?","/whereischeap","market-search",()=>OpenMarketSearch(""),marketWindow.IsOpen);
  Row("Do I already have it?","/doihaveit","item-search",()=>OpenItemSearch(""),itemSearchWindow.IsOpen);
  Row("Housing bids","/bid","housing-bids",OpenBidSearch,bidWindow.IsOpen);
  ImGui.Separator();
  Row("Gardening guide","/gardening","gardening",()=>OnPlantingCommand("/gardening",""),plantingWindow.IsOpen);
  Row("Fashion Report","/fashionr","fashion-report",()=>OnFashionCommand("/fashionr",""),fashionWindow.IsOpen);
  Row("Journal · Adventure & Properties","Open the paired Journal in your browser","characters",()=>Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#adventure"));
  Row("Welcome & guide","Companion guide","characters",()=>welcomeWindow.IsOpen=true,welcomeWindow.IsOpen);
  Row("Settings","Open Companion settings","options",OpenSettings);
  if(registrationSnapshot is {} snapshot && Player.IsLoaded && snapshot.Actor.ContentId==Player.ContentId.ToString())
   Row("New character","/equinox register","characters",()=>registrationWindow.IsOpen=true,registrationWindow.IsOpen);
  ImGui.EndPopup();
 }
}


