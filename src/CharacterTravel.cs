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
  if(loginMenuReturn.Active){characterTravelStatus="Returning to the main menu. Wait or stop the return before choosing a character.";return;}
  var mode=ReadCharacterLoginMode(true);
  if(mode==CharacterLoginMode.Unavailable){characterTravelStatus=Player.IsLoaded?"Leave combat, loading or your duty before switching characters.":"Wait for the title or character-selection screen and enable Lifestream before logging in.";return;}
  if(DateTimeOffset.UtcNow<characterSwitchUntil){characterTravelStatus="A character switch is already requested.";return;}
  if(followSession.Armed){characterTravelStatus="Stop FollowThem before switching characters.";return;}
  if(mode==CharacterLoginMode.InGame&&CharacterTravelPolicy.IsCurrent(character,ReadActor())){characterTravelStatus="You are already on this character.";return;}
  var from=CurrentCharacterSwitchScope(mode,true);var to=CharacterSwitchScope(character.Id);
  var relationship=LoginRelationship(character,from,to);
  var action=confirmed?CharacterLoginPolicy.Action(mode,relationship,true,CharacterSwitchConsentCurrent(character,from,to))
   :CharacterLoginPolicy.InitialAction(mode,relationship,from.PersonId.Length>0&&from.AccountId.Length>0);
  if(action==CharacterLoginAction.IdentifyLogin){StartCurrentLoginIdentification(character);return;}
  if(action==CharacterLoginAction.Confirm){characterTravelStatus="Confirm that this character is available on the current Square Enix login. A different login needs its own game client.";return;}
  if(action==CharacterLoginAction.CheckList){StartTitleLoginCheck(character);return;}
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
  if(showHouses&&CharacterHouseIconCount(character)>0){
   var stack=ImGui.GetContentRegionAvail().X<ImGui.GetFontSize()*27;
   if(ImGui.BeginTable("character-travel-actions",2,ImGuiTableFlags.SizingFixedFit)){
    ImGui.TableSetupColumn("Character",ImGuiTableColumnFlags.WidthStretch);
    ImGui.TableSetupColumn("Houses",ImGuiTableColumnFlags.WidthFixed,CharacterHouseIconWidth(character,stack));
    ImGui.TableNextRow();ImGui.TableNextColumn();DrawCharacterTravel(character,false);
    ImGui.TableNextColumn();DrawCharacterHouseIcons(character,false,stack);ImGui.EndTable();
   }
   return;
  }
  if(characterSwitchUntil!=default&&Player.IsLoaded&&Player.ContentId!=0&&Player.ContentId!=characterSwitchFrom){characterSwitchUntil=default;characterTravelStatus=characterSwitchTarget is {} requested&&CharacterTravelPolicy.IsCurrent(requested,ReadActor())?"Logged in as "+Player.CharacterName+".":"A different character logged in.";characterSwitchTarget=null;}
  var switching=loginMenuReturn.Active||titleLoginProbe.Active||DateTimeOffset.UtcNow<characterSwitchUntil;
  var mode=ReadCharacterLoginMode();
  ImGui.BeginDisabled(mode==CharacterLoginMode.Unavailable||switching);
  var travelLabel=CharacterLoginPolicy.Label(Player.IsLoaded);
  if(ButtonWidth(travelLabel)>ImGui.GetContentRegionAvail().X)travelLabel=Player.IsLoaded?"Switch":"Log in";
  if(ImGui.SmallButton(travelLabel+"###character-travel")){
   characterTravelStatusOwner=character.Id;
   var clickMode=ReadCharacterLoginMode(true);var from=CurrentCharacterSwitchScope(clickMode,true);var to=CharacterSwitchScope(character.Id);
   if(CharacterLoginPolicy.InitialAction(clickMode,LoginRelationship(character,from,to),from.PersonId.Length>0&&from.AccountId.Length>0)==CharacterLoginAction.Confirm)
    OpenCharacterLoginConfirmation(character,clickMode);
   else SwitchSharedCharacter(character);
  }
  var travelHovered=ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
  ImGui.EndDisabled();
  if(travelHovered)DrawCharacterTravelTooltip(character,mode,switching);
  if(queuedLoginConfirmation==character.Id&&!switching&&mode!=CharacterLoginMode.Unavailable){
   queuedLoginConfirmation="";OpenCharacterLoginConfirmation(character,mode,true);
  }
  if(ImGui.BeginPopupModal("Confirm character###EquinoxCharacterSwitch",ImGuiWindowFlags.AlwaysAutoResize)){
   ImGui.PushTextWrapPos(ImGui.GetFontSize()*32);
   var current=CurrentSharedCharacter();var from=CurrentCharacterSwitchScope(mode);var to=CharacterSwitchScope(character.Id);
   var knownAccount=from.PersonId.Length>0&&from.AccountId.Length>0;
   ImGui.TextUnformatted("Is this character on the current Square Enix login?");
   if(characterSwitchConfirmFrom!=0)ImGui.TextUnformatted($"From: {current?.Name??"Unidentified character"} · {from.PersonName} / {from.AccountName}");
   else if(knownAccount)ImGui.TextUnformatted($"From: {from.PersonName} / {from.AccountName}");
   ImGui.TextUnformatted($"To: {character.Name} @ {character.World} · {to.PersonName} / {to.AccountName}");
   if(characterSwitchConfirmFrom!=0)ImGui.TextUnformatted("This logs out the current character.");
   else if(!knownAccount)ImGui.TextUnformatted("Current login is not verified yet. Open your character list or log in once to identify it.");
   ImGui.TextWrapped("No search or login for this character has started. Continue only if it belongs to the Square Enix login used to launch this client.");
   ImGui.TextWrapped("Different service accounts can share one Square Enix login. Once a character from each account logs into this game client, Equinox remembers the link for every character in those accounts.");
   ImGui.TextWrapped("A different Square Enix login needs you to exit and sign in again, or use its already signed-in game client.");
   var unchanged=CharacterSwitchConsentCurrent(character,from,to);
   if(ImGui.Button("Cancel")){CancelCharacterLoginConfirmation(mode,unchanged);ImGui.CloseCurrentPopup();}ImGui.SameLine();
   ImGui.BeginDisabled(!unchanged||mode==CharacterLoginMode.Unavailable||switching);
   if(ImGui.Button(mode==CharacterLoginMode.Title?"Same login — check character list":characterSwitchConfirmFrom==0?"Same login — log in":"Same login — switch")){characterSwitchReturnOnCancel=false;ImGui.CloseCurrentPopup();SwitchSharedCharacter(character,true);characterSwitchConfirmTarget="";}
   ImGui.EndDisabled();if(!unchanged)ImGui.TextUnformatted("The current character, account or pairing changed. Cancel and choose again.");
   ImGui.PopTextWrapPos();ImGui.EndPopup();
  }
  if(switching){
   SameLineIfFits(ButtonWidth(loginMenuReturn.Active?"Stop return":titleLoginProbe.Active?"Stop check":"Stop switch"));
   if(ImGui.SmallButton(loginMenuReturn.Active?"Stop return":titleLoginProbe.Active?"Stop check":"Stop switch")){
    if(loginMenuReturn.Active){loginMenuReturn.Stop();characterTravelStatus="Return stopped. No character login was requested.";}
    else{
     bringTrip=null;
     if(titleLoginProbe.Active)StopTitleLoginCheck("Login check stopped. No login was requested.");
     else{
      try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}
      characterSwitchUntil=default;characterSwitchTarget=null;
      characterTravelStatus="Switch queue stopped. If already logged out, select a character in game.";
     }
    }
   }
  }
  if(characterTravelStatusOwner==character.Id){if(characterTravelStatus.Length>0)ImGui.TextWrapped(characterTravelStatus);DrawGoSeeHomeStop();}
 }
 private void OpenCharacterLoginConfirmation(SharedCharacter character,CharacterLoginMode mode,bool openedFromTitle=false)
 {
  characterSwitchReturnOnCancel=openedFromTitle;
  var from=CurrentCharacterSwitchScope(mode,true);var to=CharacterSwitchScope(character.Id);
  characterSwitchConfirmFrom=Player.IsLoaded?Player.ContentId:0;characterSwitchConfirmServiceAccount=CurrentLoginServiceAccount();
  characterSwitchConfirmKey=config.PairingKey;characterSwitchConfirmTarget=character.Id;
  characterSwitchConfirmScope=SwitchScopeKey(from)+">"+SwitchScopeKey(to);
  ImGui.OpenPopup("Confirm character###EquinoxCharacterSwitch");
 }
 private void DrawCharacterTravelTooltip(SharedCharacter character,CharacterLoginMode mode,bool switching)
 {
  // Use the same account evidence and decision as the button; hover never starts travel.
  var from=CurrentCharacterSwitchScope(titleLoginProbe.Active?CharacterLoginMode.Title:mode);var to=CharacterSwitchScope(character.Id);
  var currentKnown=!string.IsNullOrWhiteSpace(from.PersonId)&&!string.IsNullOrWhiteSpace(from.AccountId);
  var targetKnown=!string.IsNullOrWhiteSpace(to.PersonId)&&!string.IsNullOrWhiteSpace(to.AccountId);
  var relationship=LoginRelationship(character,from,to);
  var same=relationship!=CharacterLoginRelationship.Unverified;
  ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);
  ImGui.TextUnformatted(character.Name+" @ "+character.World);
  if(loginMenuReturn.Active)ImGui.TextColored(new Vector4(1,.75f,.35f,1),"Returning to main menu · no login requested");
  else if(titleLoginProbe.Active)ImGui.TextColored(new Vector4(1,.75f,.35f,1),currentLoginIdentification is not null?"Identifying current login · no character selected":"Checking character list · no login requested");
  else if(relationship==CharacterLoginRelationship.SameServiceAccount)ImGui.TextColored(new Vector4(.45f,.85f,.63f,1),"Same service account · no confirmation");
  else if(relationship==CharacterLoginRelationship.SameSquareEnixLogin)ImGui.TextColored(new Vector4(.45f,.85f,.63f,1),"Same Square Enix login · no confirmation");
  else if(mode==CharacterLoginMode.Title&&!currentKnown)ImGui.TextColored(new Vector4(1,.75f,.35f,1),"Identify current login automatically");
  else ImGui.TextColored(new Vector4(1,.75f,.35f,1),"Square Enix login not verified · confirmation before any search");
  ImGui.TextUnformatted("Current: "+(currentKnown?from.PersonName+" / "+from.AccountName:"Not verified"));
  ImGui.TextUnformatted("Target: "+(targetKnown?to.PersonName+" / "+to.AccountName:"Not verified"));
  ImGui.Separator();
  ImGui.TextWrapped(Player.IsLoaded?"Log out and switch using Lifestream.":"Log in using Lifestream and the current game login.");
  if(relationship==CharacterLoginRelationship.SameSquareEnixLogin)ImGui.TextWrapped("Service accounts verified under this login. The link covers every character in those Person/accounts. Lifestream selects the configured service account.");
  else if(!same)ImGui.TextWrapped("A successful login from each service account in this game client links their whole Person/accounts locally. No Journal refresh is needed. A different Square Enix login requires exiting and signing in again.");
  if(!targetKnown)ImGui.TextWrapped("Sync this character's Person/account information first.");
  if(titleLoginProbe.Active)ImGui.TextWrapped($"Checking the current character list before login. Stops in {Math.Max(0,(int)Math.Ceiling((titleLoginProbe.Deadline-DateTimeOffset.UtcNow).TotalSeconds))}s. Stop check cancels this request.");
  else if(loginMenuReturn.Active)ImGui.TextWrapped("Cancel is returning to the main menu. Stop return cancels this request.");
  else if(switching)ImGui.TextWrapped("A character switch is already requested.");
  else if(mode==CharacterLoginMode.Title&&!currentKnown&&!same)ImGui.TextWrapped("Opens START and reads this game's character list first. Your verified login continues automatically; an unverified account needs confirmation before any target search or login.");
  else if(mode==CharacterLoginMode.Title&&!same)ImGui.TextWrapped("Shows a warning before sending anything to Lifestream. If you confirm this is the same login, checks the exact character in the game list before requesting login. A different Square Enix login needs its own game client.");
  else if(mode==CharacterLoginMode.Unavailable)ImGui.TextWrapped(Player.IsLoaded?"Unavailable during combat, duties or loading.":"Wait for the title or character-selection screen and enable Lifestream.");
  else if(mode==CharacterLoginMode.CharacterSelect)ImGui.TextWrapped("The character must be in the loaded list. Use the title screen for another world or service account.");
  ImGui.PopTextWrapPos();ImGui.EndTooltip();
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
 private static int CharacterHouseIconCount(SharedCharacter character) =>
  (character.Houses.Any(h=>h.Type=="Private house")?1:0)+(character.Houses.Any(h=>h.Type=="Free Company house")?1:0);
 private static float CharacterHouseIconWidth(SharedCharacter character,bool stack)
 {
  var count=CharacterHouseIconCount(character);if(count==0)return 0;
  var size=Math.Max(28,ImGui.GetFrameHeight());return stack?size:count*size+(count-1)*ImGui.GetStyle().ItemSpacing.X;
 }
 private void DrawCharacterHouseIcons(SharedCharacter character,bool inline=true,bool stack=false)
 {
  var switching=DateTimeOffset.UtcNow<characterSwitchUntil;var first=true;
  foreach(var type in new[]{"Private house","Free Company house"}){
   var houses=character.Houses.Where(h=>h.Type==type).DistinctBy(h=>h.Id).ToArray();if(houses.Length==0)continue;
   if((inline&&first)||(!first&&!stack))ImGui.SameLine();first=false;var size=Math.Max(28,ImGui.GetFrameHeight());var at=ImGui.GetCursorScreenPos();
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



