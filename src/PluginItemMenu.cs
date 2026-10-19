using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private uint pluginPopupItem,openingPluginItem;
 private byte[]? pluginItemMenuName;
 private Vector2 pluginDrawingWindowPosition,pluginDrawingWindowSize,pluginItemWindowPosition,pluginItemWindowSize;
 private void RememberItemMenuWindow(){pluginDrawingWindowPosition=ImGui.GetWindowPos();pluginDrawingWindowSize=ImGui.GetWindowSize();}

 private string pluginPopupName="";
 private bool pluginPopupRequested,pluginPopupActive,pluginPopupArtisan;
 private bool pluginPopupHasRecipe;
 private void ItemRightClick(uint id){
  if(id!=0&&ImGui.IsItemClicked(ImGuiMouseButton.Right))OpenPluginItemMenu(id);
 }
 private void OpenPluginItemMenu(uint raw){
  var id=ItemContextPolicy.Normalize(raw);
  if(id==0)return;
  var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id);
  if(row is null||row.Value.Name.IsEmpty)return;
  pluginItemWindowPosition=pluginDrawingWindowPosition;pluginItemWindowSize=pluginDrawingWindowSize;
  pluginPopupItem=id;pluginPopupName=row.Value.Name.ToString();
  // Resolve once per click, never scan sheets on every rendered frame.
  pluginPopupHasRecipe=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Recipe>()
   .Any(r=>r.ItemResult.RowId==id);
  pluginPopupArtisan=Pi.InstalledPlugins.Any(p=>p.IsLoaded&&p.InternalName=="Artisan");
  pluginPopupRequested=true;
 }
 private void PluginItemAction(Action action){
  try{action();}catch(Exception ex){Log.Warning(ex,"Item menu action unavailable");PrintEquinox("[Equinox] That item action is unavailable right now.");}
 }
 private unsafe void OpenPluginRecipe(uint id){
  var agent=AgentRecipeNote.Instance();if(agent!=null)agent->SearchRecipeByItemId(id);
 }
 private unsafe void OpenNativePluginItemMenu(uint id){
  if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
  var context=AgentContext.Instance();var item=AgentRecipeItemContext.Instance();
  if(context==null||item==null)return;
  var viewport=ImGui.GetMainViewport();var origin=viewport.Pos;var area=viewport.Size;
  var local=pluginItemWindowPosition-origin;var size=pluginItemWindowSize;
  // Keep the native UI beside the source window rather than hiding Companion.
  // Native menus cannot render over Dalamud's ImGui layer.
  const float menuWidth=440,menuHeight=580,gap=12;
  float x,y;
  if(local.X+size.X+gap+menuWidth<=area.X){x=local.X+size.X+gap;y=local.Y;}
  else if(local.X>=menuWidth+gap){x=local.X-menuWidth-gap;y=local.Y;}
  else if(local.Y+size.Y+gap+menuHeight<=area.Y){x=local.X;y=local.Y+size.Y+gap;}
  else if(local.Y>=menuHeight+gap){x=local.X;y=local.Y-menuHeight-gap;}
  else {PrintEquinox("[Equinox] Move or resize this window to leave room beside it for the game/plugin menu, then try again.");return;}
  x=Math.Clamp(x,0,Math.Max(0,area.X-menuWidth));y=Math.Clamp(y,0,Math.Max(0,area.Y-menuHeight));
  var bytes=Encoding.UTF8.GetBytes(pluginPopupName+"\0");
  pluginItemMenuName=GC.AllocateArray<byte>(bytes.Length,pinned:true);bytes.CopyTo(pluginItemMenuName,0);
  var previous=GameGui.HoveredItem;
  try{
   context->ClearMenu();context->OwnerAddon=0;context->TargetContentId=0;context->TargetAccountId=0;context->TargetObjectId=default;
   openingPluginItem=id;GameGui.HoveredItem=id;item->ResultItemId=id;
   fixed(byte* name=pluginItemMenuName)item->AddItemContextMenuEntries(id,2,name);
   context->SetPosition((int)x,(int)y);
   context->OpenContextMenu(false);
  }finally{openingPluginItem=0;GameGui.HoveredItem=previous;}
 }
 private void DrawPluginItemPopup(){
  if(!pluginPopupRequested&&!pluginPopupActive)return;
  // A stable host keeps popup identity independent of the originating window,
  // list clipping, item ID stack, and updates to garden artwork.
  ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(Vector2.One);
  ImGui.Begin("##EquinoxItemMenuHost",ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoBackground|ImGuiWindowFlags.NoInputs|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing);
  if(pluginPopupRequested){ImGui.OpenPopup("Item actions");pluginPopupRequested=false;}
  if(ImGui.BeginPopup("Item actions")){
   var id=pluginPopupItem;var name=pluginPopupName;
   ImGui.TextUnformatted(name);ImGui.Separator();
   if(ImGui.MenuItem("Where is it cheapest?"))PluginItemAction(()=>OpenMarketItem(id));
   if(ImGui.MenuItem("Do I already have it?"))PluginItemAction(()=>OpenItemSearch(id.ToString()));
   if(ImGui.MenuItem("Copy item name"))ImGui.SetClipboardText(name);
   var ready=Player.IsLoaded&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
   if(pluginPopupHasRecipe){
    ImGui.Separator();ImGui.BeginDisabled(!ready);
    if(ImGui.MenuItem("Search crafting recipes"))PluginItemAction(()=>OpenPluginRecipe(id));
    ImGui.EndDisabled();
   }
   ImGui.Separator();ImGui.BeginDisabled(!ready);
   if(ImGui.MenuItem("Game & plugin actions"))PluginItemAction(()=>OpenNativePluginItemMenu(id));
   if(ImGui.IsItemHovered())ImGui.SetTooltip("Open the real game item menu beside Companion. Installed plugins provide their supported actions here.");
   ImGui.EndDisabled();
   if(pluginPopupArtisan&&ImGui.BeginMenu("Artisan")){
    if(ImGui.MenuItem("Open crafting lists"))PluginItemAction(()=>Commands.ProcessCommand("/artisan lists"));
    ImGui.EndMenu();
   }
   ImGui.EndPopup();
  }
  pluginPopupActive=ImGui.IsPopupOpen("Item actions");
  ImGui.End();
 }
 private void GardenItemRightClick(string label,bool seed){
  if(!ImGui.IsItemClicked(ImGuiMouseButton.Right))return;
  var name=label.Trim();
  if(name.EndsWith(" seed",StringComparison.OrdinalIgnoreCase))name=name[..^5];
  var names=seed?new[]{name+" Seeds",name+" Seed",name}:new[]{name};
  foreach(var candidate in names){
   var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).FirstOrDefault(x=>string.Equals(x.Name.ToString(),candidate,StringComparison.OrdinalIgnoreCase));
   if(row.RowId>0){OpenPluginItemMenu(row.RowId);return;}
  }
 }
}
