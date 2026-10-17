using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
internal sealed record UpgradeWatch(string HouseId,string World,string HouseName,string OwnerName,string Person,string Account,bool Enabled,string State);
internal sealed record UpgradeMatch(string Key,string HouseId,string World,uint WorldId,string District,int Ward,int Plot,int Size,long Price,int? Entries,long PhaseUntil,double Updated,string OwnerName,string Person,string Account,string HouseName,string Reason);
internal sealed record UpgradeFavourite(string District,int Plot,int[] Wards,bool BothDivisions=false,string Id="");
internal sealed record UpgradeReply(UpgradeWatch[]? Watches,UpgradeMatch[]? Matches,string? Error=null,UpgradeFavourite[]? Favourites=null);
public sealed partial class Plugin
{
 private readonly HttpClient upgradeHttp=new(){Timeout=TimeSpan.FromSeconds(25)};
 private Task<UpgradeReply>? upgradeRequest;
 private string upgradeKey="",upgradeRequestKey="",upgradeRequestWorld="",upgradeStatus="Configure watches in Journal → Manage Properties → Upgrade houses.";
 private DateTimeOffset upgradeNext;
 private readonly Queue<string> upgradeWorlds=new();
 private readonly Dictionary<string,UpgradeMatch[]> upgradeMatches=new();
 private UpgradeWatch[] upgradeWatches=[];
 private ulong upgradeActor;
 private bool bidLocationFavoritesOnly;
 private int bidSection;
 private UpgradeFavourite[] upgradeFavourites=[];
 private bool IsFavouriteLocation(string district,int ward,int plot)=>upgradeFavourites.Any(f=>f.District==district&&(f.Plot==plot||f.BothDivisions&&(f.Plot-1)%30==(plot-1)%30)&&(f.Wards.Length==0||f.Wards.Contains(ward)));
 private UpgradeMatch[] upgradeShown=[];
 private int upgradePage;
 private void RebuildUpgradeRows(){upgradeShown=upgradeMatches.Values.SelectMany(x=>x).OrderBy(m=>m.OwnerName).ThenBy(m=>m.Entries??int.MaxValue).ToArray();upgradePage=0;}
 private async Task<UpgradeReply> FetchUpgrades(string key,string world)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,"https://equinoxjournal.pages.dev/api/companion/housing-upgrades"+(world.Length>0?"?world="+Uri.EscapeDataString(world):""));
  request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
  using var response=await upgradeHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);
  response.EnsureSuccessStatusCode();await response.Content.LoadIntoBufferAsync(6000000);
  return JsonSerializer.Deserialize<UpgradeReply>(await response.Content.ReadAsStringAsync(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException("Empty housing response");
 }
 private void UpdateHousingUpgrades(DateTimeOffset now)
 {
  if(upgradeKey!=config.PairingKey){upgradeKey=config.PairingKey;upgradeWorlds.Clear();upgradeMatches.Clear();upgradeShown=[];upgradeWatches=[];upgradeFavourites=[];bidDirty=true;upgradeNext=now;upgradeActor=0;favouriteRevision=0;favouriteNext=default;favouriteStatus="";}
  UpdateHousingFavourites(now);
  if(upgradeRequest?.IsCompleted==true){
   var task=upgradeRequest;upgradeRequest=null;
   if(task.IsFaulted){_=task.Exception;upgradeStatus="Housing watch check unavailable. Deploy Journal V7.11.180; previous results may be outdated.";upgradeNext=now.AddMinutes(5);upgradeWorlds.Clear();}
   else if(task.IsCompletedSuccessfully&&upgradeRequestKey==config.PairingKey){
    var reply=task.Result;
    if(upgradeRequestWorld.Length==0){upgradeWatches=reply.Watches??[];if(favouriteRevision==0)upgradeFavourites=reply.Favourites??[];bidDirty=true;var active=upgradeWatches.Where(w=>w.Enabled&&w.State=="Watching").Select(w=>w.World).Distinct().ToHashSet();foreach(var old in upgradeMatches.Keys.Where(w=>!active.Contains(w)).ToArray())upgradeMatches.Remove(old);foreach(var world in active)upgradeWorlds.Enqueue(world);upgradeStatus=active.Count==0?"No active watches. Configure Upgrade houses in the Journal.":"Checking watched worlds…";}
    else{
     var matches=(reply.Matches??[]).Where(m=>m.PhaseUntil>now.ToUnixTimeSeconds()&&m.Size is >=0 and <=2&&m.Ward is >=1 and <=30&&m.Plot is >=1 and <=60).ToArray();upgradeMatches[upgradeRequestWorld]=matches;
     var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.PairingKey)));
     if(config.HousingUpgradePairingHash!=hash){config.HousingUpgradePairingHash=hash;config.HousingUpgradeNotified=[];}
     var seen=config.HousingUpgradeNotified.ToHashSet();var fresh=matches.Where(m=>!seen.Contains(m.Key)).ToArray();
     if(fresh.Length>0){foreach(var group in fresh.GroupBy(m=>new{m.OwnerName,m.HouseName}).Take(8)){var first=group.First();Chat.Print($"[Equinox] Housing upgrade · {first.OwnerName} · {first.HouseName}: {group.Count()} options, including {first.World} · {first.District} W{first.Ward} P{first.Plot}. /bid → Upgrade matches.");}config.HousingUpgradeNotified=seen.Concat(fresh.Select(m=>m.Key)).Distinct().TakeLast(2000).ToList();SaveConfiguration();}
     upgradeStatus=$"Checked {now.ToLocalTime():HH:mm} · Paissa reports; verify at the placard.";
    }
    RebuildUpgradeRows();
    upgradeNext=upgradeWorlds.Count>0?now.AddSeconds(2):now.AddMinutes(5);
   }
  }
  if(!Player.IsLoaded){upgradeActor=0;return;}
  if(!config.SyncEnabled||config.PairingKey.Length!=64)return;
  if(upgradeActor!=Player.ContentId){upgradeActor=Player.ContentId;upgradeNext=now.AddSeconds(3);upgradeWorlds.Clear();}
  if(upgradeRequest is not null||now<upgradeNext)return;
  upgradeRequestWorld=upgradeWorlds.Count>0?upgradeWorlds.Dequeue():"";upgradeRequestKey=config.PairingKey;upgradeRequest=FetchUpgrades(upgradeRequestKey,upgradeRequestWorld);
 }
 private void OpenUpgradeJournal(string house="",string district="",int plot=0)=>Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#housing-upgrades="+Uri.EscapeDataString(house)+(district.Length>0?"&district="+Uri.EscapeDataString(district)+"&plot="+plot:""));
 private bool DrawHousingUpgrades()
 {
  if(ImGui.RadioButton("Find plots",bidSection==0))bidSection=0;ImGui.SameLine();
  if(ImGui.RadioButton($"Saved plots ({config.BidFavorites.Count})",bidSection==1))bidSection=1;ImGui.SameLine();
  if(ImGui.RadioButton($"Favourite plots ({upgradeFavourites.Length})",bidSection==2)){bidSection=2;favouriteNext=default;}ImGui.SameLine();
  if(ImGui.RadioButton("Upgrade matches",bidSection==3))bidSection=3;
  ImGui.Separator();
  DrawHousingFavouritePlots();
  if(bidSection==2)return true;
  if(bidSection==1){DrawBidFavorites(true);if(bidTravelMessage.Length>0)ImGui.TextWrapped(bidTravelMessage);DrawGoSeeHomeStop();return true;}
  if(bidSection!=3)return false;
  if(ImGui.SmallButton("Set up upgrade watches in Journal"))OpenUpgradeJournal();
  ImGui.TextWrapped(upgradeStatus);
  ImGui.BeginDisabled(upgradeRequest is not null);if(ImGui.SmallButton("Check watches")){upgradeWorlds.Clear();upgradeNext=default;}ImGui.EndDisabled();
  foreach(var w in upgradeWatches.Where(w=>w.State!="Watching"))ImGui.TextDisabled(w.OwnerName+" · "+w.HouseName+" · "+w.State);
  var now=DateTimeOffset.UtcNow;var count=0;
  if(upgradeShown.Length>50){if(ImGui.SmallButton("Previous matches"))upgradePage=Math.Max(0,upgradePage-1);ImGui.SameLine();ImGui.TextUnformatted($"Page {upgradePage+1} / {(upgradeShown.Length+49)/50}");ImGui.SameLine();if(ImGui.SmallButton("Next matches"))upgradePage=Math.Min((upgradeShown.Length-1)/50,upgradePage+1);}
  foreach(var m in upgradeShown.Skip(upgradePage*50).Take(50).Where(m=>m.PhaseUntil>now.ToUnixTimeSeconds())){
   count++;ImGui.PushID(m.Key);ImGui.Separator();ImGui.TextUnformatted(m.OwnerName+" · "+m.HouseName);ImGui.TextDisabled(m.Person+" / "+m.Account);
   ImGui.TextWrapped($"{m.World} · {m.District} W{m.Ward} P{m.Plot} · {new[]{"Small","Medium","Large"}[m.Size]} · {m.Price:N0} gil · {m.Entries?.ToString()??"?"} entries");
   ImGui.TextWrapped(m.Reason+$" · Reported {DateTimeOffset.FromUnixTimeSeconds((long)m.Updated).ToLocalTime():g}");
   if(ImGui.SmallButton("GO SEE"))GoSeeBid(m.WorldId,m.District,m.Ward,m.Plot);ImGui.SameLine();if(ImGui.SmallButton("Copy"))ImGui.SetClipboardText($"{m.World} · {m.District} · Ward {m.Ward} · Plot {m.Plot}");ImGui.SameLine();
   var favourite=new BidFavorite(m.WorldId,m.District,m.Ward,m.Plot);if(DrawSavedPlotButton("SaveUpgrade",bidFavoriteKeys.Contains(favourite.Key)))ToggleBidFavorite(favourite);
   ImGui.SameLine();if(ImGui.SmallButton("★"))ChooseFavouriteLocation(m.District,m.Ward,m.Plot);if(ImGui.IsItemHovered())ImGui.SetTooltip("Save this favourite plot here; choose any ward or this ward only.");ImGui.PopID();
  }
  if(count==0)ImGui.TextWrapped("No matching opportunities reported. Watches check all configured property worlds, on any paired character.");
  if(bidTravelMessage.Length>0)ImGui.TextWrapped(bidTravelMessage);DrawGoSeeHomeStop();return true;
 }
}
