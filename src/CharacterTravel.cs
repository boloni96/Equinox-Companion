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
 private int characterSwitchConfirmServiceAccount=-1;
 private string characterSwitchConfirmScope="",characterSwitchConfirmKey="",characterSwitchConfirmTarget="";
 private SharedCharacter? characterSwitchTarget;
 private CharacterLoginMode characterLoginMode;
 private DateTimeOffset characterLoginProbeAt;
 private CharacterLoginMode ReadCharacterLoginMode(bool refresh=false)
 {
  if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.BoundByDuty])return CharacterLoginMode.Unavailable;
  if(Player.IsLoaded&&Player.ContentId!=0)return CharacterLoginMode.InGame;
  if(Client.IsLoggedIn)return CharacterLoginMode.Unavailable;
  var now=DateTimeOffset.UtcNow;
  if(!refresh&&now<characterLoginProbeAt)return characterLoginMode;
  characterLoginProbeAt=now.AddMilliseconds(500);characterLoginMode=CharacterLoginMode.Unavailable;
  try{
   if(LifestreamBusy())return characterLoginMode;
   if(Pi.GetIpcSubscriber<bool>("Lifestream.CanAutoLogin").InvokeFunc())characterLoginMode=CharacterLoginMode.Title;
   else if(Pi.GetIpcSubscriber<bool>("Lifestream.CanInitiateTravelFromCharaSelectList").InvokeFunc())characterLoginMode=CharacterLoginMode.CharacterSelect;
  }catch{/* Optional Lifestream login IPC is unavailable; keep the saved roster usable. */}
  return characterLoginMode;
 }
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
  CharacterLoginPolicy.SameSource(characterSwitchConfirmFrom,Player.IsLoaded,Player.ContentId)&&(characterSwitchConfirmFrom!=0||characterSwitchConfirmServiceAccount==CurrentLoginServiceAccount())&&config.PairingKey==characterSwitchConfirmKey&&character.Id==characterSwitchConfirmTarget&&characterSwitchConfirmScope==SwitchScopeKey(from)+">"+SwitchScopeKey(to);
 private void SwitchSharedCharacter(SharedCharacter character,bool confirmed=false)
 {
  var mode=ReadCharacterLoginMode(true);
  if(mode==CharacterLoginMode.Unavailable){characterTravelStatus=Player.IsLoaded?"Leave combat, loading or your duty before switching characters.":"Wait for the title or character-selection screen and enable Lifestream before logging in.";return;}
  if(DateTimeOffset.UtcNow<characterSwitchUntil){characterTravelStatus="A character switch is already requested.";return;}
  if(followSession.Armed){characterTravelStatus="Stop FollowThem before switching characters.";return;}
  if(mode==CharacterLoginMode.InGame&&CharacterTravelPolicy.IsCurrent(character,ReadActor())){characterTravelStatus="You are already on this character.";return;}
  var from=CurrentCharacterSwitchScope(mode,true);var to=CharacterSwitchScope(character.Id);
  if(CharacterTravelPolicy.NeedsConfirmation(from.PersonId,from.AccountId,to.PersonId,to.AccountId)&&(!confirmed||!CharacterSwitchConsentCurrent(character,from,to))){characterTravelStatus="Confirm the person/account change before switching. Check Lifestream’s service-account settings first.";return;}
  // Prefer the observed game identity over a manually decorated Journal name.
  var observed=config.Discoveries.LastOrDefault(e=>e.Actor.ContentId==character.ContentId)?.Actor;
  var name=observed?.Name??character.Name;var world=string.IsNullOrWhiteSpace(observed?.HomeWorldName)?character.World:observed.HomeWorldName;
  if(!CharacterTravelPolicy.ValidTarget(name,world)){characterTravelStatus="A complete in-game character name and home world are needed. Sync this character first.";return;}
  try{
   if(shoppingTravel is not null||LifestreamBusy()){characterTravelStatus="Travel is already in progress.";return;}
   var accepted=CharacterLoginPolicy.Request(mode,name,world,
    command=>Pi.GetIpcSubscriber<string,object>("Lifestream.ExecuteCommand").InvokeAction(command),
    (n,w)=>Pi.GetIpcSubscriber<string,string,bool>("Lifestream.ConnectAndLogin").InvokeFunc(n,w),
    (n,w)=>Pi.GetIpcSubscriber<string,string,bool>("Lifestream.InitiateLoginFromCharaSelectScreen").InvokeFunc(n,w));
   if(!accepted){characterTravelStatus=mode==CharacterLoginMode.CharacterSelect?"Lifestream could not find this character in the current list. Return to the title screen and choose Log in as this character again.":"Lifestream could not start login from this screen. Wait for the title menu and try again.";return;}
   characterSwitchFrom=mode==CharacterLoginMode.InGame?Player.ContentId:0;characterSwitchTarget=character;characterSwitchUntil=DateTimeOffset.UtcNow.AddMinutes(5);
   characterTravelStatus=$"{(mode==CharacterLoginMode.InGame?"Switch":"Login")} requested: {name} @ {world}. Lifestream uses this game login and its service-account settings.";
   PrintEquinox("[Equinox] "+characterTravelStatus);
  }catch(Exception){characterTravelStatus="Lifestream's character login is unavailable. Update or enable Lifestream and try again.";}
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
  if(characterSwitchUntil!=default&&Player.IsLoaded&&Player.ContentId!=0&&Player.ContentId!=characterSwitchFrom){characterSwitchUntil=default;characterTravelStatus=characterSwitchTarget is {} requested&&CharacterTravelPolicy.IsCurrent(requested,ReadActor())?"Logged in as "+Player.CharacterName+".":"A different character logged in.";characterSwitchTarget=null;}
  var switching=DateTimeOffset.UtcNow<characterSwitchUntil;
  var mode=ReadCharacterLoginMode();
  ImGui.BeginDisabled(mode==CharacterLoginMode.Unavailable||switching);
  if(ImGui.SmallButton(CharacterLoginPolicy.Label(Player.IsLoaded))){
   characterTravelStatusOwner=character.Id;
   var from=CurrentCharacterSwitchScope(ReadCharacterLoginMode(true),true);var to=CharacterSwitchScope(character.Id);
   if(CharacterTravelPolicy.NeedsConfirmation(from.PersonId,from.AccountId,to.PersonId,to.AccountId)){
    characterSwitchConfirmFrom=Player.IsLoaded?Player.ContentId:0;characterSwitchConfirmServiceAccount=CurrentLoginServiceAccount();characterSwitchConfirmKey=config.PairingKey;characterSwitchConfirmTarget=character.Id;characterSwitchConfirmScope=SwitchScopeKey(from)+">"+SwitchScopeKey(to);ImGui.OpenPopup("Confirm character###EquinoxCharacterSwitch");
   }else SwitchSharedCharacter(character);
  }
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip((Player.IsLoaded?"Log out and switch using Lifestream: ":"Log in using Lifestream: ")+character.Name+" @ "+character.World+"\nUses the current game login and Lifestream's service-account settings."+(mode==CharacterLoginMode.Unavailable?"\nWait until loading finishes. At the title screen, Lifestream must be enabled.":mode==CharacterLoginMode.CharacterSelect?"\nThe character must be in the loaded list; use the title screen for another world or service account.":""));
  ImGui.EndDisabled();
  if(ImGui.BeginPopupModal("Confirm character###EquinoxCharacterSwitch",ImGuiWindowFlags.AlwaysAutoResize)){
   var current=CurrentSharedCharacter();var from=CurrentCharacterSwitchScope(mode);var to=CharacterSwitchScope(character.Id);
   var knownAccount=from.PersonId.Length>0&&from.AccountId.Length>0;
   ImGui.TextUnformatted(characterSwitchConfirmFrom!=0||knownAccount?"Change to a different Person or account?":"Log in as this character using the current game login?");
   if(characterSwitchConfirmFrom!=0)ImGui.TextUnformatted($"From: {current?.Name??"Unidentified character"} · {from.PersonName} / {from.AccountName}");
   else if(knownAccount)ImGui.TextUnformatted($"From: {from.PersonName} / {from.AccountName}");
   ImGui.TextUnformatted($"To: {character.Name} @ {character.World} · {to.PersonName} / {to.AccountName}");
   if(characterSwitchConfirmFrom!=0)ImGui.TextUnformatted("This logs out the current character.");
   else if(!knownAccount)ImGui.TextUnformatted("Current account is not verified yet. Open your character list or log in once to identify it.");
   ImGui.TextUnformatted("Check Lifestream's service-account settings for this Person/account.");
   ImGui.TextUnformatted("A different game account still needs its own game login.");
   if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();ImGui.SameLine();
   var unchanged=CharacterSwitchConsentCurrent(character,from,to);
   ImGui.BeginDisabled(!unchanged||mode==CharacterLoginMode.Unavailable||switching);
   if(ImGui.Button(characterSwitchConfirmFrom==0?"Yes, log in":"Yes, switch character")){ImGui.CloseCurrentPopup();SwitchSharedCharacter(character,true);characterSwitchConfirmTarget="";}
   ImGui.EndDisabled();if(!unchanged)ImGui.TextUnformatted("The current character, account or pairing changed. Cancel and choose again.");
   ImGui.EndPopup();
  }
  if(showHouses)DrawCharacterHouseIcons(character);
  if(switching){ImGui.SameLine();if(ImGui.SmallButton("Stop switch")){bringTrip=null;try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}characterSwitchUntil=default;characterTravelStatus="Switch queue stopped. If already logged out, select a character in game.";}}
  if(characterTravelStatusOwner==character.Id){if(characterTravelStatus.Length>0)ImGui.TextWrapped(characterTravelStatus);DrawGoSeeHomeStop();}
 }
 private static string CharacterHouseRole(SharedCharacter character,SharedHouse house)
 {
  if(house.Type=="Private house"&&string.IsNullOrWhiteSpace(house.OwnerName))return "Private · owner unknown";
  var owner=!string.IsNullOrWhiteSpace(house.OwnerName)&&string.Equals(character.Name.Trim(),house.OwnerName.Trim(),StringComparison.OrdinalIgnoreCase);
  return SharedHousePresentation.Label(character,house,owner);
 }
 private static string CharacterHouseHeading(SharedCharacter character,SharedHouse house)
 {
  var role=CharacterHouseRole(character,house);
  var owner=house.Type=="Free Company house"?house.FcName:house.OwnerName;
  return role+" · "+(string.IsNullOrWhiteSpace(owner)?"Owner not recorded":owner);
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
    ImGui.TextUnformatted("GO SEE · "+(type=="Private house"?"Private & shared houses":"FC houses"));
    ImGui.TextDisabled("For "+character.Name);
    foreach(var h in houses){
     ImGui.Separator();
     ImGui.TextUnformatted(CharacterHouseHeading(character,h));
     var band=h.Paused?HousingBand.Unknown:HousingStatus.Band(h.LastEntry,DateTimeOffset.UtcNow);
     ImGui.TextWrapped($"{h.Name} · {h.World} · {h.District} W{h.Ward} P{h.Plot}");
     ImGui.TextColored(BandColour(band),h.Paused?"Demolition marked suspended":BandLabel(band));
     ImGui.TextWrapped(h.LastEntry is {} entry?$"Last qualifying entry: {DisplayTime.Local(entry)}\n45-day estimate: {DisplayTime.Local(entry.AddDays(45))}":"No qualifying entry recorded.");
     ImGui.TextDisabled(type=="Private house"?"Entry must be by the private owner.":"Entry must be by an eligible FC member.");
    }
    ImGui.TextWrapped("Travel with your current character; does not switch characters.");
    ImGui.PopTextWrapPos();ImGui.EndTooltip();
   }
   ImGui.EndDisabled();
   if(clicked){characterTravelStatusOwner=character.Id;if(houses.Length==1)VisitSharedHouse(houses[0]);else ImGui.OpenPopup("Choose "+type);}
   if(ImGui.BeginPopup("Choose "+type)){foreach(var h in houses)if(ImGui.Selectable($"{CharacterHouseHeading(character,h)} · {h.Name} · {h.World} · {h.District} W{h.Ward} P{h.Plot}##{h.Id}"))VisitSharedHouse(h);ImGui.EndPopup();}
  }
 }

}
