using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private AdventureStop? houseCharacterStop;
 private SharedRoster? houseCharacterRoster;
 private SharedCharacter? adventureHouseOwner;
 private SharedCharacter[] adventureHouseMembers=[];
 private SharedHouse? adventurePortraitHouse;
 private void PrepareAdventureHouseCharacters(AdventureStop stop)
 {
  if(ReferenceEquals(houseCharacterStop,stop)&&ReferenceEquals(houseCharacterRoster,config.SharedRoster))return;
  houseCharacterStop=stop;houseCharacterRoster=config.SharedRoster;
  var all=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).ToArray();
  adventurePortraitHouse=all.SelectMany(c=>c.Houses).FirstOrDefault(h=>h.Id==stop.HouseId);
  adventureHouseOwner=adventurePortraitHouse is {} house?SharedHousePresentation.Owner(house,all):null;
  adventureHouseMembers=all.Where(c=>stop.CharacterIds?.Contains(c.Id)==true&&c.Id!=adventureHouseOwner?.Id).ToArray();
 }
 private bool DrawAdventureHouseOwner(AdventureStop stop)
 {
  PrepareAdventureHouseCharacters(stop);var expanded=false;
  if(adventureHouseOwner is {} owner)
   DrawAdventureCharacter(owner,stop,()=>{DrawAdventureHouseControls(stop);expanded=DrawAdventureStepsHeader(stop.Tasks);});
  else{
   // Missing or ambiguous ownership must never promote the first tenant to owner.
   var name=adventurePortraitHouse?.OwnerName;
   ImGui.TextWrapped(string.IsNullOrWhiteSpace(name)?"Owner not recorded":"Owner · "+name);
   DrawAdventureHouseControls(stop);expanded=DrawAdventureStepsHeader(stop.Tasks);
  }
  return expanded;
 }
 private void DrawAdventureHouseMembers(AdventureStop stop)
 {
  if(adventureHouseMembers.Length==0)return;
  ImGui.SetNextItemOpen(false,ImGuiCond.Once);
  if(ImGui.CollapsingHeader($"Tenants & FC members · {adventureHouseMembers.Length}###house-members"))
   foreach(var character in adventureHouseMembers)DrawAdventureCharacter(character,stop);
 }
 private sealed record PortraitFrame(float X=50,float Y=50,float Z=1);
 private sealed record PortraitPayload(string Image,PortraitFrame Frame);
 private sealed record LoadedPortrait(IDalamudTextureWrap Texture,PortraitFrame Frame);
 private sealed class PortraitEntry
 {
  public Task<LoadedPortrait?>? Loading;public LoadedPortrait? Picture;public long Used;public DateTimeOffset Retry;
  public void Dispose(){Picture?.Texture.Dispose();Picture=null;if(Loading is {} task){_=task.ContinueWith(t=>{if(t.IsCompletedSuccessfully)t.Result?.Texture.Dispose();else _=t.Exception;});Loading=null;}}
 }
 private readonly HttpClient portraitHttp=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
 private readonly Dictionary<string,PortraitEntry> portraitCache=[];
 private CancellationTokenSource portraitCancel=new();private string portraitPairing="";
 private void ClearAdventurePortraits(){portraitCancel.Cancel();foreach(var e in portraitCache.Values)e.Dispose();portraitCache.Clear();portraitCancel.Dispose();portraitCancel=new();}
 private void DisposeAdventurePortraits(){ClearAdventurePortraits();portraitCancel.Dispose();portraitHttp.Dispose();}
 private async Task<LoadedPortrait?> LoadAdventurePortrait(string id,string hash,string key,CancellationToken cancel)
 {
  try{
   using var request=new HttpRequestMessage(HttpMethod.Get,"https://equinoxjournal.pages.dev/api/companion/portrait?id="+Uri.EscapeDataString(id)+"&hash="+hash);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
   using var response=await portraitHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel);response.EnsureSuccessStatusCode();await response.Content.LoadIntoBufferAsync(140000);
   var payload=JsonSerializer.Deserialize<PortraitPayload>(await response.Content.ReadAsStringAsync(cancel),new JsonSerializerOptions(JsonSerializerDefaults.Web));if(payload?.Frame is not {} frame||!float.IsFinite(frame.X)||!float.IsFinite(frame.Y)||!float.IsFinite(frame.Z)||frame.X is <0 or >100||frame.Y is <0 or >100||frame.Z is <1 or >5)return null;
   byte[] bytes;
   if(payload.Image.StartsWith("data:image/png;base64,",StringComparison.Ordinal)){bytes=Convert.FromBase64String(payload.Image[22..]);if(bytes.Length>100000)return null;}
   else{
    if(!Uri.TryCreate(payload.Image,UriKind.Absolute,out var url)||url.Scheme!="https"||url.Host!="img2.finalfantasyxiv.com"||!url.IsDefaultPort||url.UserInfo.Length>0)return null;
    using var photo=await portraitHttp.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancel);photo.EnsureSuccessStatusCode();await photo.Content.LoadIntoBufferAsync(5000000);bytes=await photo.Content.ReadAsByteArrayAsync(cancel);
   }
   cancel.ThrowIfCancellationRequested();var texture=await Textures.CreateFromImageAsync(bytes,"Equinox Adventure portrait",cancel);
   if(cancel.IsCancellationRequested){texture.Dispose();return null;}return new(texture,frame);
  }catch{return null;}
 }
 private void DrawAdventureCharacter(SharedCharacter character,AdventureStop stop,Action? details=null)
 {
  if(portraitPairing!=config.PairingKey){ClearAdventurePortraits();portraitPairing=config.PairingKey;}
  var reference=stop.Portraits?.FirstOrDefault(p=>p.Id==character.Id);LoadedPortrait? picture=null;
  if(ImGui.IsRectVisible(new Vector2(ImGui.GetContentRegionAvail().X,220))&&reference is not null&&System.Text.RegularExpressions.Regex.IsMatch(reference.Hash,"^[a-f0-9]{64}$")){
   var cacheKey=character.Id+"|"+reference.Hash;
   if(!portraitCache.TryGetValue(cacheKey,out var entry)){
    if(portraitCache.Count>=24){var oldest=portraitCache.MinBy(x=>x.Value.Used);oldest.Value.Dispose();portraitCache.Remove(oldest.Key);}
    portraitCache[cacheKey]=entry=new();
   }entry.Used=Environment.TickCount64;
   if(entry.Loading?.IsCompleted==true){entry.Picture=entry.Loading.IsCompletedSuccessfully?entry.Loading.Result:null;_=entry.Loading.Exception;entry.Loading=null;entry.Retry=DateTimeOffset.UtcNow.AddMinutes(1);}
   if(entry.Picture is null&&entry.Loading is null&&DateTimeOffset.UtcNow>=entry.Retry&&portraitCache.Values.Count(e=>e.Loading is not null)<2)entry.Loading=LoadAdventurePortrait(character.Id,reference.Hash,config.PairingKey,portraitCancel.Token);
   picture=entry.Picture;
  }
  ImGui.PushID("travel-"+character.Id);var origin=ImGui.GetCursorScreenPos();var size=new Vector2(88,110);
  if(ImGui.BeginTable("portrait-header",2,ImGuiTableFlags.SizingFixedFit)){
   ImGui.TableSetupColumn("Portrait",ImGuiTableColumnFlags.WidthFixed,size.X);
   ImGui.TableSetupColumn("Character",ImGuiTableColumnFlags.WidthStretch);
   ImGui.TableNextRow();ImGui.TableNextColumn();
   if(picture is {} p){var scale=Math.Max(size.X/p.Texture.Width,size.Y/p.Texture.Height)*p.Frame.Z;var full=new Vector2(p.Texture.Width,p.Texture.Height)*scale;var offset=(full-size)*new Vector2(p.Frame.X/100,p.Frame.Y/100);ImGui.Image(p.Texture.Handle,size,offset/full,(offset+size)/full);}
   else{GardenImage("assets/category-icons/characters.png",ImGui.GetCursorScreenPos(),size);ImGui.Dummy(size);}
   ImGui.TableNextColumn();
   if(ImGui.BeginTable("character-heading",2,ImGuiTableFlags.SizingFixedFit)){
    ImGui.TableSetupColumn("Name",ImGuiTableColumnFlags.WidthStretch);
    ImGui.TableSetupColumn("Houses",ImGuiTableColumnFlags.WidthFixed,2*Math.Max(28,ImGui.GetFrameHeight())+ImGui.GetStyle().ItemSpacing.X);
    ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextWrapped(character.Name);
   var scope=CharacterSwitchScope(character.Id);ImGui.TextWrapped(scope.PersonName+" · "+character.Account);
   var here=Player.IsLoaded&&Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture)==character.ContentId;
   var reported=adventureBoards.FirstOrDefault(b=>b.Id==adventureSelected)?.Locations?.GetValueOrDefault(character.Id);
   ImGui.TextWrapped(here?"Now: "+CurrentAdventureArea():reported is not null&&!string.IsNullOrEmpty(reported.Area)?"Last reported: "+reported.Area:"Location not reported yet");
   if(ImGui.IsItemHovered())ImGui.SetTooltip(here?"Current area · "+(WorldName(Player.CurrentWorld.RowId)??character.World):reported is not null?reported.World+" · Last location shared by this character’s Companion · "+(DateTimeOffset.TryParse(reported.At,out var observed)?observed.ToLocalTime().ToString("g"):"time unavailable"):"No current location reported in this adventure yet.");
    ImGui.TableNextColumn();DrawCharacterHouseIcons(character,false);ImGui.EndTable();
   }
   DrawCharacterTravel(character,false);DrawBringCharacter(character);details?.Invoke();ImGui.EndTable();
  }
  ImGui.Spacing();ImGui.PopID();
 }
}
