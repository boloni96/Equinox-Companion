using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private uint pluginPopupItem;
 private string pluginPopupName="";
 private bool pluginPopupRequested,pluginPopupActive,pluginPopupArtisan;
 private (ushort Id,string Label)[] pluginPopupRecipes=[];
 private void ItemRightClick(uint id){
  if(id!=0&&ImGui.IsItemClicked(ImGuiMouseButton.Right))OpenPluginItemMenu(id);
 }
 private void OpenPluginItemMenu(uint raw){
  var id=ItemContextPolicy.Normalize(raw);
  if(id==0)return;
  var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id);
  if(row is null||row.Value.Name.IsEmpty)return;
  pluginPopupItem=id;pluginPopupName=row.Value.Name.ToString();
  // Resolve once per click, never scan sheets on every rendered frame.
  pluginPopupRecipes=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Recipe>()
   .Where(r=>r.ItemResult.RowId==id&&r.RowId<=ushort.MaxValue)
   .Select(r=>((ushort)r.RowId,$"{r.CraftType.Value.Name} · level {r.RecipeLevelTable.Value.ClassJobLevel}##{r.RowId}")).ToArray();
  pluginPopupArtisan=Pi.InstalledPlugins.Any(p=>p.IsLoaded&&p.InternalName=="Artisan");
  pluginPopupRequested=true;
 }
 private void PluginItemAction(Action action){
  try{action();}catch(Exception ex){Log.Warning(ex,"Item menu action unavailable");Chat.Print("[Equinox] That item action is unavailable right now.");}
 }
 private unsafe void OpenPluginRecipe(uint id){
  var agent=AgentRecipeNote.Instance();if(agent!=null)agent->SearchRecipeByItemId(id);
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
   if(pluginPopupRecipes.Length>0){
    ImGui.Separator();ImGui.BeginDisabled(!ready);
    if(ImGui.MenuItem("Search crafting recipes"))PluginItemAction(()=>OpenPluginRecipe(id));
    ImGui.EndDisabled();
   }
   if(pluginPopupArtisan&&ImGui.BeginMenu("Artisan")){
    if(ImGui.MenuItem("Open crafting lists"))PluginItemAction(()=>Commands.ProcessCommand("/artisan lists"));
    if(pluginPopupRecipes.Length>0&&ImGui.BeginMenu("Craft one",ready)){
     foreach(var recipe in pluginPopupRecipes)if(ImGui.MenuItem(recipe.Label))PluginItemAction(()=>{
      if(Pi.GetIpcSubscriber<bool>("Artisan.IsBusy").InvokeFunc()){Chat.Print("[Equinox] Artisan is already busy.");return;}
      Pi.GetIpcSubscriber<ushort,int,object>("Artisan.CraftItem").InvokeAction(recipe.Id,1);
     });
     ImGui.EndMenu();
    }
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
