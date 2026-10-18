using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
internal sealed record SavedPlotsReply(long Revision,BidFavorite[] Plots);
public sealed partial class Plugin
{
 private Task<SavedPlotsReply>? savedRequest;
 private string savedKey="",savedRequestKey="",savedStatus="",savedNotes="",savedDistrict="Mist";
 private uint savedWorld;
 private int savedWard=1,savedPlot=1;
 private long savedRevision;
 private bool savedWriting,savedImporting,savedPopup;
 private DateTimeOffset savedNext;
 private string SavedPairingHash()=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(config.PairingKey)));
 private async Task<SavedPlotsReply> RequestSavedPlots(string key,object? body=null){
  using var request=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/saved-plots");
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
  if(body is not null)request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
  using var response=await upgradeHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);
  await response.Content.LoadIntoBufferAsync(4000000);
  if(!response.IsSuccessStatusCode)throw new HttpRequestException(response.StatusCode==System.Net.HttpStatusCode.Conflict?"Saved plots changed. Refresh, then save your draft again.":"Saved plots unavailable. Check pairing and deploy Journal V7.11.194.");
  return JsonSerializer.Deserialize<SavedPlotsReply>(await response.Content.ReadAsStringAsync(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException("Empty saved plots response");
 }
 private void ApplySavedPlots(SavedPlotsReply reply){
  savedRevision=reply.Revision;
  if(JsonSerializer.Serialize(config.BidFavorites)!=JsonSerializer.Serialize(reply.Plots)||config.SavedPlotsPairingHash!=SavedPairingHash()){
   config.BidFavorites=reply.Plots.ToList();config.SavedPlotsPairingHash=SavedPairingHash();SaveConfiguration();
   bidFavoriteKeys.Clear();bidFavoriteKeys.UnionWith(config.BidFavorites.Select(p=>p.Key));bidDirty=true;
  }
 }
 private void UpdateSavedPlots(DateTimeOffset now){
  if(savedKey!=config.PairingKey){savedKey=config.PairingKey;savedRevision=0;savedNext=default;savedStatus="";if(config.SavedPlotsMigrated&&config.SavedPlotsPairingHash!=SavedPairingHash()){config.BidFavorites=[];bidFavoriteKeys.Clear();bidDirty=true;}}
  if(savedRequest?.IsCompleted==true){
   var task=savedRequest;savedRequest=null;
   if(savedRequestKey==config.PairingKey){
    if(task.IsCompletedSuccessfully){
     savedRevision=task.Result.Revision;
     if(!config.SavedPlotsMigrated&&!savedImporting&&config.BidFavorites.Count>0){
      savedImporting=true;savedWriting=true;savedStatus="Sharing existing saved plots…";
      savedRequest=RequestSavedPlots(savedRequestKey,new{revision=savedRevision,action="import",plots=config.BidFavorites.Select(p=>SavedPlotBody(p)).ToArray()});return;
     }
     if(!config.SavedPlotsMigrated){config.SavedPlotsMigrated=true;SaveConfiguration();}
     ApplySavedPlots(task.Result);savedStatus=savedWriting?"Saved and shared.":"Shared saved plots up to date.";savedNext=now.AddSeconds(bidWindow.IsOpen?15:60);
    }else{_=task.Exception;savedStatus=task.Exception?.GetBaseException().Message??"Saved plot sync failed.";savedNext=now.AddSeconds(15);savedRevision=0;}
   }else{_=task.Exception;savedNext=default;}
   savedWriting=false;savedImporting=false;
  }
  if(!config.SyncEnabled||config.PairingKey.Length!=64||savedRequest is not null||now<savedNext)return;
  savedRequestKey=config.PairingKey;savedRequest=RequestSavedPlots(savedRequestKey);
 }
 private object SavedPlotBody(BidFavorite p)=>new{world=p.World,worldName=p.WorldName.Length>0?p.WorldName:BidWorldName(p.World),district=p.District,ward=p.Ward,plot=p.Plot,notes=p.Notes};
 private void WriteSavedPlot(BidFavorite p,bool remove=false){
  if(savedRequest is not null){savedStatus="Sync in progress. Try again in a moment; your draft is kept.";return;}
  if(savedRevision==0||!config.SyncEnabled||config.PairingKey.Length!=64){savedStatus="Refresh shared plots before saving.";savedNext=default;return;}
  savedWriting=true;savedRequestKey=config.PairingKey;savedStatus="Saving…";
  savedRequest=RequestSavedPlots(savedRequestKey,new{revision=savedRevision,action=remove?"remove":"upsert",key=p.Key,plot=SavedPlotBody(p)});
 }
 private void ToggleBidFavorite(BidFavorite p)=>WriteSavedPlot(p,bidFavoriteKeys.Contains(p.Key));
 private void EditSavedPlot(BidFavorite? p=null){
  savedWorld=p?.World??Player.CurrentWorld.RowId;savedDistrict=p?.District??"Mist";savedWard=p?.Ward??1;savedPlot=p?.Plot??1;savedNotes=p?.Notes??"";savedPopup=true;
 }
 private void DrawBidFavorites(bool expanded=false){
  if(!expanded&&!ImGui.CollapsingHeader($"Saved plots · {config.BidFavorites.Count} plots###BidFavorites"))return;
  EnsureBidWorlds();
  ImGui.TextWrapped("Shared houses to revisit — available or occupied. Notes are shared with your paired Journal and Companion.");
  if(ImGui.SmallButton("Add house address"))EditSavedPlot();
  ImGui.SameLine();
  var a=currentAddress;
  var canSave=Player.IsLoaded&&a is not null&&a.WorldId==Player.CurrentWorld.RowId&&HousingLottery.District(a.DistrictName??"") is not null&&!a.Apartment&&a.Ward is >=1 and <=30&&a.Plot is >=1 and <=60&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
  ImGui.BeginDisabled(!canSave);
  if(ImGui.SmallButton("Save current house")&&a is not null){var district=HousingLottery.District(a.DistrictName??"")??"Mist";var p=new BidFavorite(a.WorldId,district,a.Ward,a.Plot);EditSavedPlot(config.BidFavorites.FirstOrDefault(f=>f.Key==p.Key)??p);}
  ImGui.EndDisabled();if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Use the detected estate address. If outside a known plot, use Add house address.");
  ImGui.SameLine();ImGui.BeginDisabled(savedRequest is not null);if(ImGui.SmallButton("Refresh"))savedNext=default;ImGui.EndDisabled();
  if(savedStatus.Length>0)ImGui.TextWrapped(savedStatus);
  if(savedPopup){ImGui.OpenPopup("Save house###SavedPlotEditor");savedPopup=false;}
  if(ImGui.BeginPopupModal("Save house###SavedPlotEditor",ImGuiWindowFlags.AlwaysAutoResize)){
   if(ImGui.BeginCombo("World",BidWorldName(savedWorld))){foreach(var w in bidWorlds!)if(ImGui.Selectable(w.Name,w.Id==savedWorld))savedWorld=w.Id;ImGui.EndCombo();}
   if(ImGui.BeginCombo("District",savedDistrict)){foreach(var d in BidSearch.HousingLotteryDistricts)if(ImGui.Selectable(d,d==savedDistrict))savedDistrict=d;ImGui.EndCombo();}
   ImGui.InputInt("Ward",ref savedWard);ImGui.InputInt("Plot",ref savedPlot);
   ImGui.InputTextMultiline("Notes",ref savedNotes,2001,new Vector2(420,100));
   ImGui.BeginDisabled(savedRequest is not null||savedRevision==0||savedWorld==0||savedWard<1||savedWard>30||savedPlot<1||savedPlot>60);
   if(ImGui.Button("Save and share"))WriteSavedPlot(new(savedWorld,savedDistrict,savedWard,savedPlot,savedNotes,BidWorldName(savedWorld)));
   ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Close"))ImGui.CloseCurrentPopup();
   ImGui.TextWrapped(savedStatus);ImGui.EndPopup();
  }
  ImGui.BeginChild("SavedBidPlots",new Vector2(0,Math.Max(120,ImGui.GetContentRegionAvail().Y-8)),true);
  foreach(var f in config.BidFavorites){
   ImGui.PushID(f.Key);ImGui.Separator();
   ImGui.TextUnformatted($"{BidWorldName(f.World)} · {f.District} W{f.Ward} P{f.Plot}");CopyTextOnClick($"{BidWorldName(f.World)} · {f.District} · Ward {f.Ward} · Plot {f.Plot}");
   if(f.Notes.Length>0)ImGui.TextWrapped(f.Notes);
   if(ImGui.SmallButton("Notes / edit"))EditSavedPlot(f);
   ImGui.SameLine();if(ImGui.SmallButton("GO SEE"))GoSeeBid(f.World,f.District,f.Ward,f.Plot);
   ImGui.SameLine();if(ImGui.SmallButton("Search world")){bidSection=0;bidWorld=f.World;bidRegion=bidWorlds!.FirstOrDefault(w=>w.Id==bidWorld).Region;StartBidSearch();}
   ImGui.SameLine();ImGui.BeginDisabled(savedRequest is not null||savedRevision==0);if(ImGui.SmallButton("Remove"))WriteSavedPlot(f,true);ImGui.EndDisabled();ImGui.PopID();
  }
  if(config.BidFavorites.Count==0)ImGui.TextDisabled("Save any house address above, or click a save icon in Find plots.");
  ImGui.EndChild();
 }
}
