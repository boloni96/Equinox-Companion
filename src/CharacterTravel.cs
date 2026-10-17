using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private string characterTravelStatus="";
 private DateTimeOffset characterSwitchUntil;
 private ulong characterSwitchFrom;
 private ulong characterSwitchConfirmFrom;
 private string characterSwitchConfirmScope="";
 private (string PersonId,string PersonName,string AccountId,string AccountName) CharacterSwitchScope(string id){
  foreach(var p in config.SharedRoster?.People??[])if(p.Characters.FirstOrDefault(c=>c.Id==id) is {} c)return(p.Id,p.Name,c.AccountId,c.Account);
  return("","Unknown person","","Unknown account");
 }
 private SharedCharacter? CurrentSharedCharacter()=>Player.IsLoaded?(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).FirstOrDefault(c=>c.ContentId==Player.ContentId.ToString()):null;
 private static string SwitchScopeKey((string PersonId,string PersonName,string AccountId,string AccountName) scope)=>scope.PersonId+"|"+scope.AccountId;
 private void SwitchSharedCharacter(SharedCharacter character)
 {
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.BoundByDuty]){characterTravelStatus="Leave combat, loading or your duty before switching characters.";return;}
  if(DateTimeOffset.UtcNow<characterSwitchUntil){characterTravelStatus="A character switch is already requested.";return;}
  if(followSession.Armed){characterTravelStatus="Stop FollowThem before switching characters.";return;}
  var actor=ReadActor();
  if(CharacterTravelPolicy.IsCurrent(character,actor)){characterTravelStatus="You are already on this character.";return;}
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
 private unsafe bool TryGoSeeOwnEstate(uint world,string district,int ward,int plot)
 {
  // Resolve ownership and available teleports from the currently logged-in game,
  // never from a selected roster character or a stale Journal owner name.
  if(world!=Player.HomeWorld.RowId)return false;
  var telepo=FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance();if(telepo==null)return false;
  telepo->UpdateAetheryteList();
  foreach(var type in new[]{FFXIVClientStructs.FFXIV.Client.Game.EstateType.PersonalEstate,FFXIVClientStructs.FFXIV.Client.Game.EstateType.FreeCompanyEstate}){
   var owned=FFXIVClientStructs.FFXIV.Client.Game.HousingManager.GetOwnedHouseId(type);
   if(!CharacterTravelPolicy.MatchesEstate(AddressOf(owned),world,district,ward,plot))continue;
   foreach(var entry in telepo->TeleportList){
    if(entry.HouseId.Id!=owned.Id)continue;
    var command=type==FFXIVClientStructs.FFXIV.Client.Game.EstateType.PersonalEstate?"private":"fc";
    Pi.GetIpcSubscriber<string,object>("Lifestream.ExecuteCommand").InvokeAction(command);
    bidTravelMessage=$"GO SEE requested: your {(command=="private"?"private":"Free Company")} estate via /li {command}.";return true;
   }
  }return false;
 }
 private void VisitSharedHouse(SharedHouse house)
 {
  if(DateTimeOffset.UtcNow<characterSwitchUntil){characterTravelStatus="Wait for the character switch or stop it first.";return;}
  EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>string.Equals(w.Name,house.World,StringComparison.OrdinalIgnoreCase));
  if(world.Id==0||house.Ward is <1 or >30||house.Plot is <1 or >60){characterTravelStatus="This house needs a valid world, ward and plot from the Journal.";return;}
  GoSeeBid(world.Id,house.District,house.Ward,house.Plot);characterTravelStatus=bidTravelMessage;
 }
 private void DrawCharacterTravel(SharedCharacter character)
 {
  if(characterSwitchUntil!=default&&Player.IsLoaded&&Player.ContentId!=characterSwitchFrom){characterSwitchUntil=default;characterTravelStatus="Character changed.";}
  var switching=DateTimeOffset.UtcNow<characterSwitchUntil;
  ImGui.BeginDisabled(!Player.IsLoaded||switching);
  if(ImGui.SmallButton("Switch to this Character")){
   var current=CurrentSharedCharacter();var from=CharacterSwitchScope(current?.Id??"");var to=CharacterSwitchScope(character.Id);
   if(CharacterTravelPolicy.NeedsConfirmation(from.PersonId,from.AccountId,to.PersonId,to.AccountId)){
    characterSwitchConfirmFrom=Player.ContentId;characterSwitchConfirmScope=SwitchScopeKey(from)+">"+SwitchScopeKey(to);ImGui.OpenPopup("Switch to another person or account?");
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
   ImGui.TextUnformatted("Lifestream uses its configured service accounts; Companion does not enter login credentials.");
   if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();ImGui.SameLine();
   var unchanged=Player.IsLoaded&&Player.ContentId==characterSwitchConfirmFrom&&characterSwitchConfirmScope==SwitchScopeKey(from)+">"+SwitchScopeKey(to);
   ImGui.BeginDisabled(!unchanged);
   if(ImGui.Button("Switch character")){ImGui.CloseCurrentPopup();SwitchSharedCharacter(character);}
   ImGui.EndDisabled();if(!unchanged)ImGui.TextUnformatted("The current character or pairing changed. Cancel and choose again.");
   ImGui.EndPopup();
  }
  foreach(var type in new[]{"Private house","Free Company house"}){
   var houses=character.Houses.Where(h=>h.Type==type).DistinctBy(h=>h.Id).ToArray();if(houses.Length==0)continue;
   ImGui.SameLine();var size=Math.Max(28,ImGui.GetFrameHeight());var at=ImGui.GetCursorScreenPos();
   ImGui.BeginDisabled(!Player.IsLoaded||switching);
   var clicked=ImGui.InvisibleButton("Visit "+type,new Vector2(size));
   GardenImage("assets/category-icons/"+(type=="Private house"?"private-house":"fc-house")+".png",at,new Vector2(size));
   if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("GO SEE · "+type+"\nTravel with your current character; does not switch characters.\n"+string.Join("\n",houses.Select(h=>$"{h.World} · {h.District} W{h.Ward} P{h.Plot}")));
   ImGui.EndDisabled();
   if(clicked){if(houses.Length==1)VisitSharedHouse(houses[0]);else ImGui.OpenPopup("Choose "+type);}
   if(ImGui.BeginPopup("Choose "+type)){foreach(var h in houses)if(ImGui.Selectable($"{h.Name} · {h.World} · {h.District} W{h.Ward} P{h.Plot}##{h.Id}"))VisitSharedHouse(h);ImGui.EndPopup();}
  }
  if(switching){ImGui.SameLine();if(ImGui.SmallButton("Stop switch")){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}characterSwitchUntil=default;characterTravelStatus="Switch queue stopped. If already logged out, select a character in game.";}}
  if(characterTravelStatus.Length>0)ImGui.TextWrapped(characterTravelStatus);
 }
}
