using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private static void DrawAdventureIcon(Vector2 at,Vector2 size,bool house=false)
 {
  var d=ImGui.GetWindowDrawList();var s=size.X/64;var blue=ImGui.ColorConvertFloat4ToU32(new Vector4(.25f,.72f,1,1));var white=ImGui.ColorConvertFloat4ToU32(new Vector4(.93f,.97f,1,1));
  d.AddCircleFilled(at+size/2,size.X*.46f,0xff3d260f,32);d.AddCircle(at+size/2,size.X*.39f,blue,32,2*s);
  if(house){d.AddLine(at+new Vector2(14,30)*s,at+new Vector2(32,14)*s,white,3*s);d.AddLine(at+new Vector2(32,14)*s,at+new Vector2(50,30)*s,white,3*s);d.AddRect(at+new Vector2(20,30)*s,at+new Vector2(44,49)*s,blue,2,ImDrawFlags.None,2*s);d.AddRectFilled(at+new Vector2(29,36)*s,at+new Vector2(35,49)*s,0xff4dccff);}
  else{d.AddTriangleFilled(at+new Vector2(36,12)*s,at+new Vector2(22,39)*s,at+new Vector2(33,33)*s,white);d.AddTriangleFilled(at+new Vector2(28,52)*s,at+new Vector2(42,25)*s,at+new Vector2(33,33)*s,blue);}
 }
 private void DrawWindowLauncher()
 {
  var width=ImGui.CalcTextSize("Options").X+ImGui.GetStyle().FramePadding.X*2+24;
  ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(),ImGui.GetCursorPosX()+ImGui.GetContentRegionAvail().X-width));
  if(ImGui.Button("Options",new Vector2(width,0)))ImGui.OpenPopup("EquinoxWindows");
  if(!ImGui.BeginPopup("EquinoxWindows"))return;
  ImGui.TextDisabled("OPEN A WINDOW");ImGui.Separator();
  void Row(string name,string command,string icon,Action open){
   var size=ImGui.GetTextLineHeight()*1.65f;var at=ImGui.GetCursorScreenPos();ImGui.Dummy(new Vector2(size));
   if(icon=="cheap")DrawMarketSearchIcon(at,new Vector2(size),1);else if(icon=="items")DrawItemSearchIcon(at,new Vector2(size));else if(icon is "adventure" or "bid")DrawAdventureIcon(at,new Vector2(size),icon=="bid");else GardenImage("assets/category-icons/"+icon+".png",at,new Vector2(size));
   ImGui.SameLine();if(ImGui.Selectable(name,false,ImGuiSelectableFlags.None,new Vector2(250,size))){open();ImGui.CloseCurrentPopup();}if(ImGui.IsItemHovered())ImGui.SetTooltip(command);
  }
  Row("Adventure","/adventure","adventure",OpenAdventure);
  Row("Where is it cheapest?","/whereischeap","cheap",()=>OpenMarketSearch(""));
  Row("Do I already have it?","/doihaveit","items",()=>OpenItemSearch(""));
  Row("Housing bids","/bid","bid",OpenBidSearch);
  ImGui.Separator();
  Row("Gardening guide","/gardening","gardening",()=>OnPlantingCommand("/gardening",""));
  Row("Fashion Report","/fashionr","fashion-report",()=>OnFashionCommand("/fashionr",""));
  Row("Journal · Adventure & Properties","Open the paired Journal in your browser","characters",()=>Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#adventure"));
  ImGui.EndPopup();
 }
}
