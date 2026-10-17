using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private string characterTravelStatus="",characterTravelStatusOwner="";
 private DateTimeOffset characterSwitchUntil;
 private ulong characterSwitchFrom;
 private ulong characterSwitchConfirmFrom;
 private string characterSwitchConfirmScope="",characterSwitchConfirmKey="",characterSwitchConfirmTarget="";
 private (string PersonId,string PersonName,string AccountId,string AccountName) CharacterSwitchScope(string id){
  foreach(var p in config.SharedRoster?.People??[])if(p.Characters.FirstOrDefault(c=>c.Id==id) is {} c)return(p.Id,p.Name,c.AccountId,c.Account);
  return("","Unknown person","","Unknown account");
 }
 private SharedCharacter? CurrentSharedCharacter(){
  if(!Player.IsLoaded)return null;
  var matches=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.ContentId==Player.ContentId.ToString()).Take(2).ToArray();
  return matches.Length==1?matches[0]:null;
 }
 private static string SwitchScopeKey((string PersonId,string PersonName,string AccountId,string AccountName) scope)=>scope.PersonId+"|"+scope.AccountId;
 private bool CharacterSwitchConsentCurrent(SharedCharacter character,(string PersonId,string PersonName,string AccountId,string AccountName) from,(string PersonId,string PersonName,string AccountId,string AccountName) to)=>
  Player.IsLoaded&&Player.ContentId==characterSwitchConfirmFrom&&config.PairingKey==characterSwitchConfirmKey&&character.Id==characterSwitchConfirmTarget&&characterSwitchConfirmScope==SwitchScopeKey(from)+">"+SwitchScopeKey(to);
 private void SwitchSharedCharacter(SharedCharacter character,bool confirmed=false)
 {
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.BoundByDuty]){characterTravelStatus="Leave combat, loading or your duty before switching characters.";return;}
  if(DateTimeOffset.UtcNow<characterSwitchUntil){characterTravelStatus="A character switch is already requested.";return;}
  if(followSession.Armed){characterTravelStatus="Stop FollowThem before switching characters.";return;}
  var actor=ReadActor();
  if(CharacterTravelPolicy.IsCurrent(character,actor)){characterTravelStatus="You are already on this character.";return;}
  var current=CurrentSharedCharacter();var from=CharacterSwitchScope(current?.Id??"");var to=CharacterSwitchScope(character.Id);
  if(CharacterTravelPolicy.NeedsConfirmation(from.PersonId,from.AccountId,to.PersonId,to.AccountId)&&(!confirmed||!CharacterSwitchConsentCurrent(character,from,to))){characterTravelStatus="Confirm the person/account change before switching. Check Lifestream’s service-account settings first.";return;}
  // Prefer the observed game identity over a manually decorated Journal name.
  var observed=config.Discoveries.LastOrDefault(e=>e.Actor.ContentId==character.ContentId)?.Actor;
  var name=observed?.Name??character.Name;var world=string.IsNullOrWhiteSpace(observed?.HomeWorldName)?character.World:observed.HomeWorldName;
  if(!CharacterTravelPolicy.ValidTarget(name,world)){characterTravelStatus="A complete in-game character name and home world are needed. Sync this character first.";return;}
  try{
   if(shoppingTravel is not null||LifestreamBusy()){characterTravelStatus="Travel is already in progress.";return;}
   Pi.GetIpcSubscriber<string,object>("Lifestream.ExecuteCommand").InvokeAction(name+"@"+world);
   characterSwitchFrom=Player.ContentId;characterSwitchUntil=DateTimeOffset.UtcNow.AddMinutes(5);
   characterTravelStatus=$"Switch requested: {name} @ {world}. Lifestream handles logout and login. The character must be accessible through this game login.";
   Chat.Print("[Equinox] "+characterTravelStatus);
  }catch(Exception){characterTravelStatus="Lifestream is unavailable. Install or enable it before switching.";}
 }
 private DateTimeOffset goSeeNativeUntil;
 private unsafe bool TryGoSeeEstateTeleport(uint world,string district,int ward,int plot)
 {
  // Use the actual character's available Teleport destinations, including tenant estates.
  // Exact address matching prevents a selected character's house from redirecting to ours.
  if(world!=Player.HomeWorld.RowId||world!=Player.CurrentWorld.RowId)return false;
  var telepo=FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance();if(telepo==null)return false;
  telepo->UpdateAetheryteList();
  foreach(var entry in telepo->TeleportList){
   if(entry.AetheryteId==0||!CharacterTravelPolicy.MatchesEstate(AddressOf(entry.HouseId),world,district,ward,plot))continue;
   if(followSession.Armed){bidTravelMessage="Stop FollowThem before starting GO SEE travel.";return true;}
   if(telepo->ActiveTeleportRequest){bidTravelMessage="A teleport is already in progress.";return true;}
   usingSharedTravel=true;
   try{
    var accepted=telepo->Teleport(entry.AetheryteId,entry.SubIndex);
    if(accepted)goSeeNativeUntil=DateTimeOffset.UtcNow.AddSeconds(10);
    bidTravelMessage=accepted?$"GO SEE requested: {district} W{ward} P{plot} using your available estate teleport. Confirm any game prompt.":"The game refused this estate teleport. Check your current state and try again.";
   }finally{usingSharedTravel=false;}
   // Do not start another travel route when a matching native request was refused.
   return true;
  }return false;
 }
 private void VisitSharedHouse(SharedHouse house)
 {
  if(DateTimeOffset.UtcNow<characterSwitchUntil){characterTravelStatus="Wait for the character switch or stop it first.";return;}
  EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>string.Equals(w.Name,house.World,StringComparison.OrdinalIgnoreCase));
  if(world.Id==0||house.Ward is <1 or >30||house.Plot is <1 or >60){characterTravelStatus="This house needs a valid world, ward and plot from the Journal.";return;}
  GoSeeBid(world.Id,house.District,house.Ward,house.Plot);characterTravelStatus=bidTravelMessage;
 }
 private void DrawCharacterTravel(SharedCharacter character,bool showHouses=true)
 {
  if(characterSwitchUntil!=default&&Player.IsLoaded&&Player.ContentId!=characterSwitchFrom){characterSwitchUntil=default;characterTravelStatus="Character changed.";}
  var switching=DateTimeOffset.UtcNow<characterSwitchUntil;
  ImGui.BeginDisabled(!Player.IsLoaded||switching);
  if(ImGui.SmallButton("Switch to this Character")){
   characterTravelStatusOwner=character.Id;
   var current=CurrentSharedCharacter();var from=CharacterSwitchScope(current?.Id??"");var to=CharacterSwitchScope(character.Id);
   if(CharacterTravelPolicy.NeedsConfirmation(from.PersonId,from.AccountId,to.PersonId,to.AccountId)){
    characterSwitchConfirmFrom=Player.ContentId;characterSwitchConfirmKey=config.PairingKey;characterSwitchConfirmTarget=character.Id;characterSwitchConfirmScope=SwitchScopeKey(from)+">"+SwitchScopeKey(to);ImGui.OpenPopup("Switch to another person or account?");
   }else SwitchSharedCharacter(character);
  }
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Log out and switch using Lifestream: "+character.Name+" @ "+character.World+"\nUses the current game login and Lifestream's service-account settings.");
  ImGui.EndDisabled();
  if(ImGui.BeginPopupModal("Switch to another person or account?",ImGuiWindowFlags.AlwaysAutoResize)){
   var current=CurrentSharedCharacter();var from=CharacterSwitchScope(current?.Id??"");var to=CharacterSwitchScope(character.Id);
   ImGui.TextUnformatted("Are you sure you want to change to a different person or account?");
   ImGui.TextUnformatted($"From: {current?.Name??"Unidentified character"} · {from.PersonName} / {from.AccountName}");
   ImGui.TextUnformatted($"To: {character.Name} @ {character.World} · {to.PersonName} / {to.AccountName}");
   ImGui.TextUnformatted("This logs out the current character. Another game login may be required.");
   ImGui.TextUnformatted("Lifestream may not be configured for this person or service account.");
   ImGui.TextUnformatted("Check its service-account settings before continuing. A different login may be needed.");
   ImGui.TextUnformatted("Companion cannot verify those login settings or enter credentials.");
   if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();ImGui.SameLine();
   var unchanged=CharacterSwitchConsentCurrent(character,from,to);
   ImGui.BeginDisabled(!unchanged);
   if(ImGui.Button("Yes, switch character")){ImGui.CloseCurrentPopup();SwitchSharedCharacter(character,true);characterSwitchConfirmTarget="";}
   ImGui.EndDisabled();if(!unchanged)ImGui.TextUnformatted("The current character or pairing changed. Cancel and choose again.");
   ImGui.EndPopup();
  }
  if(showHouses)DrawCharacterHouseIcons(character);
  if(switching){ImGui.SameLine();if(ImGui.SmallButton("Stop switch")){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}characterSwitchUntil=default;characterTravelStatus="Switch queue stopped. If already logged out, select a character in game.";}}
  if(characterTravelStatusOwner==character.Id){if(characterTravelStatus.Length>0)ImGui.TextWrapped(characterTravelStatus);DrawGoSeeHomeStop();}
 }
 private void DrawCharacterHouseIcons(SharedCharacter character,bool inline=true)
 {
  var switching=DateTimeOffset.UtcNow<characterSwitchUntil;var first=true;
  foreach(var type in new[]{"Private house","Free Company house"}){
   var houses=character.Houses.Where(h=>h.Type==type).DistinctBy(h=>h.Id).ToArray();if(houses.Length==0)continue;
   if(inline||!first)ImGui.SameLine();first=false;var size=Math.Max(28,ImGui.GetFrameHeight());var at=ImGui.GetCursorScreenPos();
   ImGui.BeginDisabled(!Player.IsLoaded||switching);
   var clicked=ImGui.InvisibleButton("Visit "+type,new Vector2(size));
   GardenImage("assets/category-icons/"+(type=="Private house"?"private-house":"fc-house")+".png",at,new Vector2(size));
   if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)){
    ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);
    ImGui.TextUnformatted("GO SEE · "+type);
    foreach(var h in houses){
     var band=h.Paused?HousingBand.Unknown:HousingStatus.Band(h.LastEntry,DateTimeOffset.UtcNow);
     ImGui.TextWrapped($"{h.Name} · {h.World} · {h.District} W{h.Ward} P{h.Plot}");
     ImGui.TextColored(BandColour(band),h.Paused?"Demolition marked suspended":BandLabel(band));
     ImGui.TextWrapped(h.LastEntry is {} entry?$"Last qualifying entry: {entry.ToLocalTime():dd/MM/yyyy HH:mm}\n45-day estimate: {entry.AddDays(45).ToLocalTime():dd/MM/yyyy HH:mm}":"No qualifying entry recorded.");
     ImGui.TextDisabled(type=="Private house"?"Entry must be by the private owner.":"Entry must be by an eligible FC member.");
    }
    ImGui.TextWrapped("Travel with your current character; does not switch characters.");
    ImGui.PopTextWrapPos();ImGui.EndTooltip();
   }
   ImGui.EndDisabled();
   if(clicked){characterTravelStatusOwner=character.Id;if(houses.Length==1)VisitSharedHouse(houses[0]);else ImGui.OpenPopup("Choose "+type);}
   if(ImGui.BeginPopup("Choose "+type)){foreach(var h in houses)if(ImGui.Selectable($"{h.Name} · {h.World} · {h.District} W{h.Ward} P{h.Plot}##{h.Id}"))VisitSharedHouse(h);ImGui.EndPopup();}
  }
 }

}
