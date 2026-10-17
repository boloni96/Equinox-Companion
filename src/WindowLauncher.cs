using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
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
  void Row(string name,string command,string icon,Action open){
   var size=ImGui.GetTextLineHeight()*1.65f;var at=ImGui.GetCursorScreenPos();ImGui.Dummy(new Vector2(size));
   GardenImage("assets/category-icons/"+icon+".png",at,new Vector2(size));
   ImGui.SameLine();if(ImGui.Selectable(name,false,ImGuiSelectableFlags.None,new Vector2(250,size))){open();ImGui.CloseCurrentPopup();}if(ImGui.IsItemHovered())ImGui.SetTooltip(command);
  }
  Row("Adventure","/adventure","adventure",OpenAdventure);
  Row("Where is it cheapest?","/whereischeap","market-search",()=>OpenMarketSearch(""));
  Row("Do I already have it?","/doihaveit","item-search",()=>OpenItemSearch(""));
  Row("Housing bids","/bid","housing-bids",OpenBidSearch);
  ImGui.Separator();
  Row("Gardening guide","/gardening","gardening",()=>OnPlantingCommand("/gardening",""));
  Row("Fashion Report","/fashionr","fashion-report",()=>OnFashionCommand("/fashionr",""));
  Row("Journal · Adventure & Properties","Open the paired Journal in your browser","characters",()=>Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#adventure"));
  ImGui.EndPopup();
 }
}
