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
 private bool favouritePopupPending,favouriteBoth=true,favouriteWriting;
 private long favouriteRevision;
 private DateTimeOffset favouriteNext,favouriteNoticeUntil;
 private async Task<HousingFavouriteReply> RequestHousingFavourites(string key,object? action=null)
 {
  using var request=new HttpRequestMessage(action is null?HttpMethod.Get:HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/housing-favourites");
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
  if(action is not null)request.Content=new StringContent(JsonSerializer.Serialize(action),Encoding.UTF8,"application/json");
  using var response=await upgradeHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);
  await response.Content.LoadIntoBufferAsync(200000);
  if(!response.IsSuccessStatusCode)throw new HttpRequestException(response.StatusCode==System.Net.HttpStatusCode.Conflict?"Favourite plots changed on another device. Refresh and try again.":"Favourite save unavailable. Check pairing and deploy Journal V7.11.182.");
  return JsonSerializer.Deserialize<HousingFavouriteReply>(await response.Content.ReadAsStringAsync(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException("Empty favourite response");
 }
 private void UpdateHousingFavourites(DateTimeOffset now)
 {
  if(favouriteRequest?.IsCompleted==true){var task=favouriteRequest;favouriteRequest=null;
   if(favouriteRequestKey==config.PairingKey){
    if(task.IsCompletedSuccessfully){favouriteRevision=task.Result.Revision;upgradeFavourites=task.Result.Favourites??[];bidDirty=true;favouriteStatus=favouriteWriting?"Favourite plots saved and shared.":"Favourite plots up to date.";favouriteNext=now.AddSeconds(bidWindow.IsOpen?30:300);}
    else{_=task.Exception;favouriteStatus=task.Exception?.GetBaseException().Message??"Favourite sync interrupted. Try again.";favouriteNext=now.AddSeconds(30);}
   }else{_=task.Exception;favouriteNext=default;}
   if(favouriteWriting)favouriteNoticeUntil=now.AddSeconds(15);
   favouriteWriting=false;
  }
  if(!Player.IsLoaded||!config.SyncEnabled||config.PairingKey.Length!=64||favouriteRequest is not null||now<favouriteNext)return;
  favouriteRequestKey=config.PairingKey;favouriteRequest=RequestHousingFavourites(favouriteRequestKey);
 }
 private void ChooseFavouriteLocation(string district,int ward,int plot)
 {
  favouriteDistrict=district;favouriteWard=ward;favouritePlot=plot;favouriteEditId="";favouriteBoth=true;favouritePopupPending=true;
 }
 private void WriteHousingFavourite(bool remove,string id="",bool includeWard=false)
 {
  if(favouriteRequest is not null)return;
  if(!config.SyncEnabled||config.PairingKey.Length!=64||favouriteRevision==0){favouriteStatus="Pair and refresh favourite plots before saving.";favouriteNext=default;return;}
  favouriteWriting=true;favouriteRequestKey=config.PairingKey;favouriteStatus=remove?"Removing favourite…":"Saving favourite…";
  favouriteRequest=RequestHousingFavourites(favouriteRequestKey,new{revision=favouriteRevision,action=remove?"remove":"upsert",id,favourite=new{district=favouriteDistrict,plot=favouritePlot,wards=includeWard?new[]{favouriteWard}:Array.Empty<int>(),bothDivisions=favouriteBoth}});
 }
 private void DrawHousingFavouritePlots()
 {
  if(favouritePopupPending){ImGui.OpenPopup("Save favourite plot###HousingFavouriteSave");favouritePopupPending=false;}
  if(ImGui.BeginPopupModal("Save favourite plot###HousingFavouriteSave",ImGuiWindowFlags.AlwaysAutoResize)){
   ImGui.TextUnformatted($"{favouriteDistrict} · Plot {favouritePlot}");
   ImGui.TextUnformatted("Include this ward, or match this location in any ward?");
   ImGui.Checkbox($"Match both plots {(favouritePlot-1)%30+1} / {(favouritePlot-1)%30+31}",ref favouriteBoth);
   ImGui.BeginDisabled(favouriteRequest is not null||favouriteRevision==0||!config.SyncEnabled||config.PairingKey.Length!=64);
   if(ImGui.Button("Save · any ward")){WriteHousingFavourite(false,favouriteEditId);ImGui.CloseCurrentPopup();}ImGui.SameLine();
   if(ImGui.Button($"Save · ward {favouriteWard} only")){WriteHousingFavourite(false,favouriteEditId,true);ImGui.CloseCurrentPopup();}ImGui.EndDisabled();
   if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();
   if(favouriteStatus.Length>0)ImGui.TextWrapped(favouriteStatus);ImGui.EndPopup();
  }
  if(bidSection!=2){if(favouriteWriting||DateTimeOffset.UtcNow<favouriteNoticeUntil)ImGui.TextDisabled(favouriteStatus);return;}
  ImGui.TextWrapped("Favourite locations for your upgrade watches. Shared with the Journal and your paired plugins.");
  if(favouriteStatus.Length>0)ImGui.TextDisabled(favouriteStatus);
  ImGui.BeginDisabled(favouriteRequest is not null);if(ImGui.SmallButton("Refresh favourites"))favouriteNext=default;ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Set up upgrades in Journal"))OpenUpgradeJournal();
  ImGui.TextDisabled("11 / 41 means the equivalent main and subdivision plots. Any ward includes every ward.");
  ImGui.BeginChild("FavouriteLocationList",new System.Numerics.Vector2(0,Math.Max(120,ImGui.GetContentRegionAvail().Y-8)),true);
  foreach(var f in upgradeFavourites){ImGui.PushID(f.Id);ImGui.Separator();
   var plot=f.BothDivisions?$"Plots {(f.Plot-1)%30+1} / {(f.Plot-1)%30+31}":$"Plot {f.Plot}";
   var label=$"{f.District} · {plot} · {(f.Wards.Length==0?"Any ward":"Wards "+string.Join(", ",f.Wards))}";
   ImGui.TextUnformatted(label);CopyTextOnClick(label);
   ImGui.BeginDisabled(favouriteRequest is not null);if(ImGui.SmallButton("Remove"))WriteHousingFavourite(true,f.Id);ImGui.EndDisabled();ImGui.PopID();
  }
  if(upgradeFavourites.Length==0)ImGui.TextDisabled("In Find plots, click the star beside a plot to add a favourite here.");
  ImGui.EndChild();
 }
}
