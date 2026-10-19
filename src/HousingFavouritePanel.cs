using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
internal sealed record HousingFavouriteReply(long Revision,UpgradeFavourite[] Favourites);
public sealed partial class Plugin
{
 private Task<HousingFavouriteReply>? favouriteRequest;
 private string favouriteRequestKey="",favouriteStatus="",favouriteDistrict="",favouriteEditId="";
 private int favouritePlot,favouriteWard;
 private string favouriteNotes="",favouriteNotesId="";
 private bool favouriteNotesPopup,favouriteCloseEditor;
 private string favouriteWards="";private uint favouriteTravelWorld;private int favouriteTravelWard=1,favouriteTravelPlot=1;private string favouriteTravelDistrict="Mist";private bool favouriteTravelPopup;
 private bool favouritePopupPending,favouriteBoth=true,favouriteWriting;
 private long favouriteRevision;
 private DateTimeOffset favouriteNext,favouriteNoticeUntil;
 private async Task<HousingFavouriteReply> RequestHousingFavourites(string key,object? action=null)
 {
  using var request=new HttpRequestMessage(action is null?HttpMethod.Get:HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/housing-favourites");
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
  if(action is not null)request.Content=new StringContent(JsonSerializer.Serialize(action),Encoding.UTF8,"application/json");
  using var response=await upgradeHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);
  await response.Content.LoadIntoBufferAsync(2000000);
  if(!response.IsSuccessStatusCode)throw new HttpRequestException(response.StatusCode==System.Net.HttpStatusCode.Conflict?"Favourite plots changed on another device. Refresh and try again.":"Favourite save unavailable. Check pairing and deploy Journal V7.11.182.");
  return JsonSerializer.Deserialize<HousingFavouriteReply>(await response.Content.ReadAsStringAsync(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException("Empty favourite response");
 }
 private void UpdateHousingFavourites(DateTimeOffset now)
 {
  if(favouriteRequest?.IsCompleted==true){var task=favouriteRequest;favouriteRequest=null;
   if(favouriteRequestKey==config.PairingKey){
    if(task.IsCompletedSuccessfully){if(favouriteWriting)favouriteCloseEditor=true;favouriteRevision=task.Result.Revision;upgradeFavourites=task.Result.Favourites??[];bidDirty=true;favouriteStatus=favouriteWriting?"Favourite plots saved and shared.":"Favourite plots up to date.";favouriteNext=now.AddSeconds(bidWindow.IsOpen?30:300);}
    else{_=task.Exception;favouriteStatus=task.Exception?.GetBaseException().Message??"Favourite sync interrupted. Try again.";favouriteNext=now.AddSeconds(2);}
   }else{_=task.Exception;favouriteNext=default;}
   if(favouriteWriting)favouriteNoticeUntil=now.AddSeconds(15);
   favouriteWriting=false;
  }
  if(!Player.IsLoaded||!config.SyncEnabled||config.PairingKey.Length!=64||favouriteRequest is not null||now<favouriteNext)return;
  favouriteRequestKey=config.PairingKey;favouriteRequest=RequestHousingFavourites(favouriteRequestKey);
 }
 private void ChooseFavouriteLocation(string district,int ward,int plot)
 {
  favouriteDistrict=district;favouriteWard=ward;favouritePlot=plot;favouriteEditId="";favouriteBoth=true;favouriteNotes="";favouriteWards="";favouritePopupPending=true;favouriteCloseEditor=false;
  var existing=upgradeFavourites.FirstOrDefault(f=>f.District==district&&(f.Plot==plot||f.BothDivisions&&(f.Plot-1)%30==(plot-1)%30)&&(f.Wards.Length==0||f.Wards.Contains(ward)));if(existing is not null){favouriteNotes=existing.Notes;}
 }
 private void WriteHousingFavourite(bool remove,string id="",bool includeWard=false)
 {
  if(favouriteRequest is not null)return;
  if(!config.SyncEnabled||config.PairingKey.Length!=64||favouriteRevision==0){favouriteStatus="Pair and refresh favourite plots before saving.";favouriteNext=default;return;}
  favouriteWriting=true;favouriteRequestKey=config.PairingKey;favouriteStatus=remove?"Removing favourite…":"Saving favourite…";
  favouriteRequest=RequestHousingFavourites(favouriteRequestKey,new{revision=favouriteRevision,action=remove?"remove":"upsert",id,favourite=new{district=favouriteDistrict,plot=favouritePlot,wards=includeWard?new[]{favouriteWard}:FavouriteWardSelection(),bothDivisions=favouriteBoth,notes=favouriteNotes}});
 }
 private int[] FavouriteWardSelection()=>favouriteWards.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(x=>int.TryParse(x,out var n)?n:0).Distinct().Order().ToArray();
 private void EditFavourite(UpgradeFavourite? f=null){ChooseFavouriteLocation(f?.District??"Mist",f?.Wards.FirstOrDefault()??1,f?.Plot??1);favouriteEditId=f?.Id??"";favouriteBoth=f?.BothDivisions??true;favouriteNotes=f?.Notes??"";favouriteWards=string.Join(", ",f?.Wards??[]);}
 private void DrawHousingFavouritePlots()
 {
  if(favouritePopupPending){ImGui.OpenPopup("Save favourite plot###HousingFavouriteSave");favouritePopupPending=false;}
  if(ImGui.BeginPopupModal("Save favourite plot###HousingFavouriteSave",ImGuiWindowFlags.AlwaysAutoResize)){
   if(favouriteCloseEditor){favouriteCloseEditor=false;ImGui.CloseCurrentPopup();}
   if(ImGui.BeginCombo("District",favouriteDistrict)){foreach(var d in BidSearch.HousingLotteryDistricts)if(ImGui.Selectable(d,d==favouriteDistrict))favouriteDistrict=d;ImGui.EndCombo();}
   ImGui.InputInt("Plot",ref favouritePlot);
   ImGui.Checkbox("Match equivalent main / subdivision plots",ref favouriteBoth);
   ImGui.InputText("Wards (blank = any)",ref favouriteWards,120);
   ImGui.InputTextMultiline("Notes",ref favouriteNotes,2001,new System.Numerics.Vector2(420,80));
   var valid=favouritePlot is >=1 and <=60&&FavouriteWardSelection().All(n=>n is >=1 and <=30);
   ImGui.BeginDisabled(!valid||favouriteRequest is not null||favouriteRevision==0||!config.SyncEnabled||config.PairingKey.Length!=64);
   if(ImGui.Button("Save and share")){WriteHousingFavourite(false,favouriteEditId);}
   if(favouriteEditId.Length==0&&favouriteWard is >=1 and <=30){ImGui.SameLine();if(ImGui.Button($"Save · ward {favouriteWard} only")){WriteHousingFavourite(false,favouriteEditId,true);}}
   ImGui.EndDisabled();
   if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();
   if(favouriteStatus.Length>0)ImGui.TextWrapped(favouriteStatus);ImGui.EndPopup();
  }
  if(favouriteNotesPopup){ImGui.OpenPopup("Favourite notes");favouriteNotesPopup=false;}
  if(ImGui.BeginPopupModal("Favourite notes",ImGuiWindowFlags.AlwaysAutoResize)){
   ImGui.InputTextMultiline("Notes",ref favouriteNotes,2001,new System.Numerics.Vector2(420,100));
   ImGui.BeginDisabled(favouriteRequest is not null||favouriteRevision==0);
   if(ImGui.Button("Save and share")){favouriteWriting=true;favouriteRequestKey=config.PairingKey;favouriteRequest=RequestHousingFavourites(favouriteRequestKey,new{revision=favouriteRevision,action="notes",id=favouriteNotesId,notes=favouriteNotes});}
   ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Close"))ImGui.CloseCurrentPopup();ImGui.TextWrapped(favouriteStatus);ImGui.EndPopup();
  }
  if(favouriteTravelPopup){ImGui.OpenPopup("Visit favourite plot");favouriteTravelPopup=false;}
  if(ImGui.BeginPopupModal("Visit favourite plot",ImGuiWindowFlags.AlwaysAutoResize)){
   EnsureBidWorlds();if(ImGui.BeginCombo("World",BidWorldName(favouriteTravelWorld))){foreach(var w in bidWorlds!)if(ImGui.Selectable(w.Name,w.Id==favouriteTravelWorld))favouriteTravelWorld=w.Id;ImGui.EndCombo();}
   ImGui.TextUnformatted(favouriteTravelDistrict);ImGui.InputInt("Ward",ref favouriteTravelWard);ImGui.InputInt("Plot",ref favouriteTravelPlot);
   ImGui.BeginDisabled(favouriteTravelWorld==0||favouriteTravelWard<1||favouriteTravelWard>30||favouriteTravelPlot<1||favouriteTravelPlot>60);
   if(ImGui.Button("GO SEE")){GoSeeBid(favouriteTravelWorld,favouriteTravelDistrict,favouriteTravelWard,favouriteTravelPlot);ImGui.CloseCurrentPopup();}ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();ImGui.EndPopup();
  }
  if(bidSection!=2){if(favouriteWriting||DateTimeOffset.UtcNow<favouriteNoticeUntil)ImGui.TextDisabled(favouriteStatus);return;}
  EnsureBidWorlds();
  if(ImGui.SmallButton("Add favourite"))EditFavourite();ImGui.SameLine();
  var a=currentAddress;var canSave=Player.IsLoaded&&a is not null&&a.WorldId==Player.CurrentWorld.RowId&&HousingLottery.District(a.DistrictName??"") is not null&&!a.Apartment&&a.Ward is >=1 and <=30&&a.Plot is >=1 and <=60&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
  ImGui.BeginDisabled(!canSave);if(ImGui.SmallButton("Favourite current house")&&a is not null)ChooseFavouriteLocation(HousingLottery.District(a.DistrictName??"")!,a.Ward,a.Plot);ImGui.EndDisabled();
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Use the detected estate address; choose any ward or specific wards before sharing.");
  ImGui.TextWrapped("Favourite locations for your upgrade watches. Shared with the Journal and your paired plugins.");
  if(favouriteStatus.Length>0)ImGui.TextDisabled(favouriteStatus);
  ImGui.BeginDisabled(favouriteRequest is not null);if(ImGui.SmallButton("Refresh favourites"))favouriteNext=default;ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Set up upgrades in Journal"))OpenUpgradeJournal();
  ImGui.TextDisabled("11 / 41 means the equivalent main and subdivision plots. Any ward includes every ward.");
  ImGui.BeginChild("FavouriteLocationList",new System.Numerics.Vector2(0,Math.Max(120,ImGui.GetContentRegionAvail().Y-8)),true);
  foreach(var f in upgradeFavourites){ImGui.PushID(f.Id);ImGui.Separator();
   var plot=f.BothDivisions?$"Plots {(f.Plot-1)%30+1} / {(f.Plot-1)%30+31}":$"Plot {f.Plot}";
   var label=$"{f.District} · {plot} · {(f.Wards.Length==0?"Any ward":"Wards "+string.Join(", ",f.Wards))}";
   var expanded=ImGui.TreeNodeEx("##details",ImGuiTreeNodeFlags.None," ");ImGui.SameLine();
   ImGui.TextUnformatted(label);CopyTextOnClick(label);if(ImGui.IsItemHovered()&&f.Notes.Length>0)ImGui.SetTooltip(f.Notes);
   ImGui.SameLine();if(ImGui.SmallButton("GO SEE")){favouriteTravelWorld=Player.IsLoaded?Player.CurrentWorld.RowId:bidWorld;favouriteTravelDistrict=f.District;favouriteTravelWard=f.Wards.FirstOrDefault(1);favouriteTravelPlot=f.Plot;favouriteTravelPopup=true;}
   if(expanded){if(ImGui.SmallButton("Edit"))EditFavourite(f);ImGui.SameLine();
   if(ImGui.SmallButton("Notes")){favouriteNotes=f.Notes;favouriteNotesId=f.Id;favouriteNotesPopup=true;}ImGui.SameLine();
   ImGui.BeginDisabled(favouriteRequest is not null);if(ImGui.SmallButton("Remove"))WriteHousingFavourite(true,f.Id);ImGui.EndDisabled();ImGui.TreePop();}ImGui.PopID();
  }
  if(upgradeFavourites.Length==0)ImGui.TextDisabled("Add a favourite above, use your current house, or click a star in Find plots.");
  ImGui.EndChild();
 }
}
