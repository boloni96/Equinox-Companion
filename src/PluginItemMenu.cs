using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private uint openingPluginItem;
 private bool pluginItemMenuPending,pluginItemMenuSeen;
 private long pluginItemMenuRequestedAt;
 private bool YieldToPluginItemMenu(){
  if(!pluginItemMenuPending)return false;
  if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){pluginItemMenuPending=false;return false;}
  // Native game addons are below the ImGui overlay. Yield rendering without
  // closing any Companion window or altering its selection/scroll state.
  if(VisibleFollowAddon("ContextMenu")||VisibleFollowAddon("ContextSub")){
   pluginItemMenuSeen=true;return true;
  }
  if(!pluginItemMenuSeen&&Environment.TickCount64-pluginItemMenuRequestedAt<500)return true;
  pluginItemMenuPending=false;return false;
 }
 private byte[]? pluginItemMenuName;
 private void ItemRightClick(uint id){
  if(id!=0&&ImGui.IsItemClicked(ImGuiMouseButton.Right))OpenPluginItemMenu(id);
 }
 private unsafe void OpenPluginItemMenu(uint raw){
  var id=ItemContextPolicy.Normalize(raw);
  if(id==0||!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
  var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id);
  if(row is null||row.Value.Name.IsEmpty)return;
  var context=AgentContext.Instance();var item=AgentRecipeItemContext.Instance();
  if(context==null||item==null)return;
  var bytes=Encoding.UTF8.GetBytes(row.Value.Name.ToString()+"\0");
  // The menu's native callbacks may retain its name until the next menu.
  pluginItemMenuName=GC.AllocateArray<byte>(bytes.Length,pinned:true);bytes.CopyTo(pluginItemMenuName,0);
  var oldHovered=GameGui.HoveredItem;
  try{
   context->ClearMenu();context->OwnerAddon=0;context->TargetContentId=0;context->TargetAccountId=0;context->TargetObjectId=default;
   openingPluginItem=id;GameGui.HoveredItem=id;item->ResultItemId=id;
   fixed(byte* name=pluginItemMenuName)item->AddItemContextMenuEntries(id,2,name);
   context->OpenAtPosition=0;
   context->OpenContextMenu(false);
   pluginItemMenuRequestedAt=Environment.TickCount64;
   pluginItemMenuSeen=false;pluginItemMenuPending=true;
  }catch(Exception ex){pluginItemMenuPending=false;Log.Warning(ex,"Could not open item context menu");Chat.Print("[Equinox] Item menu unavailable. Try again after closing the current game menu.");}
  finally{openingPluginItem=0;GameGui.HoveredItem=oldHovered;}
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
