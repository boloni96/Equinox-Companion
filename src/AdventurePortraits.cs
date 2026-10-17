using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
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
 private void DrawAdventureCharacter(SharedCharacter character,AdventureStop stop)
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
  ImGui.BeginGroup();
  if(picture is {} p){var scale=Math.Max(size.X/p.Texture.Width,size.Y/p.Texture.Height)*p.Frame.Z;var full=new Vector2(p.Texture.Width,p.Texture.Height)*scale;var offset=(full-size)*new Vector2(p.Frame.X/100,p.Frame.Y/100);ImGui.Image(p.Texture.Handle,size,offset/full,(offset+size)/full);}
  else{GardenImage("assets/category-icons/characters.png",origin,size);ImGui.Dummy(size);}
  ImGui.EndGroup();ImGui.SameLine();ImGui.BeginGroup();
  ImGui.TextUnformatted(character.Name);var scope=CharacterSwitchScope(character.Id);ImGui.TextWrapped(scope.PersonName+" · "+character.Account+" · "+character.World);
  DrawCharacterTravel(character);ImGui.EndGroup();ImGui.Spacing();ImGui.PopID();
 }
}
