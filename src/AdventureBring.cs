using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private sealed record BringTrip(ulong Source,ulong Character,uint World,uint Territory,uint Aetheryte,SharedHouse? House,string Pairing,DateTimeOffset Deadline,int Stage=0,DateTimeOffset NotBefore=default);
 private BringTrip? bringTrip;
 private string bringChoice="",bringMessage="";
 private ulong bringOpenedActor;
 private string bringOpenedPairing="";
 private DateTimeOffset bringTick;
 private AdventurePresence? bringDestination;
 private void DrawBringCharacter(bool ready,string world,string house)
 {
  ImGui.BeginDisabled(!ready||world.Length==0||bringTrip is not null);
  if(ImGui.SmallButton("Bring a character here")){bringDestination=new AdventurePresence(world,DateTimeOffset.UtcNow.ToString("O"),CurrentAdventureArea(),(uint)Client.TerritoryType,house);bringChoice=CurrentSharedCharacter()?.Id??"";bringOpenedActor=Player.ContentId;bringOpenedPairing=config.PairingKey;bringMessage="";ImGui.OpenPopup("Bring character");}
  ImGui.EndDisabled();
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Bring a paired character to your current in-game world and area or estate entrance. The destination is captured when you click, regardless of the stop you are viewing. Travel does not complete tasks or add characters to the route.");
  if(bringTrip is not null){ImGui.SameLine();if(ImGui.SmallButton("Cancel bring")){bringTrip=null;try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}characterSwitchUntil=default;bringMessage="Bring character cancelled.";}}
  if(!ImGui.BeginPopupModal("Bring character",ImGuiWindowFlags.AlwaysAutoResize))return;
  var report=bringDestination;
  ImGui.TextUnformatted("Bring a character to this location");
  ImGui.TextWrapped(report is null?"Choose your current location again.":report.Area+" · "+report.World);
  ImGui.TextWrapped("Destination captured where you clicked. Arrival uses the area aetheryte or estate entrance.");
  var people=config.SharedRoster?.People??[];
  ImGui.BeginChild("BringCharacterChoices",new System.Numerics.Vector2(480,240),true);
  foreach(var person in people){if(!ImGui.CollapsingHeader(person.Name))continue;
   foreach(var account in person.Characters.GroupBy(c=>c.Account)){
    if(!ImGui.TreeNode(account.Key+"##"+person.Id))continue;
    foreach(var c in account.Where(c=>!string.IsNullOrEmpty(c.ContentId)))if(ImGui.Selectable(c.Name+" @ "+c.World+"##bring-"+c.Id,c.Id==bringChoice))bringChoice=c.Id;
    ImGui.TreePop();
   }
  }
  ImGui.EndChild();
  var chosen=people.SelectMany(p=>p.Characters).FirstOrDefault(c=>c.Id==bringChoice);
  ImGui.TextUnformatted("Selected: "+(chosen?.Name??"Choose a character"));
  var from=CharacterSwitchScope(CurrentSharedCharacter()?.Id??"");var to=CharacterSwitchScope(chosen?.Id??"");
  var cross=CharacterTravelPolicy.NeedsConfirmation(from.PersonId,from.AccountId,to.PersonId,to.AccountId);
  if(cross)ImGui.TextWrapped("Different Person or account: this may log you out. Check Lifestream’s service-account settings; another game login may be needed.");
  if(bringMessage.Length>0)ImGui.TextWrapped(bringMessage);
  ImGui.BeginDisabled(chosen is null||report is null||bringTrip is not null||!Player.IsLoaded||Player.ContentId!=bringOpenedActor||bringOpenedPairing!=config.PairingKey);
  if(ImGui.Button(cross?"Confirm different Person/account and travel":"Switch and travel")){
   if(StartBringCharacter(chosen!,report!,cross))ImGui.CloseCurrentPopup();
  }
  ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Close"))ImGui.CloseCurrentPopup();ImGui.EndPopup();
 }
 private bool StartBringCharacter(SharedCharacter chosen,AdventurePresence destination,bool confirmed)
 {
  if(!Player.IsLoaded||Player.ContentId!=bringOpenedActor||bringOpenedPairing!=config.PairingKey||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||HelperInCutscene()||followSession.Armed){bringMessage="Wait until outside combat, duties, cutscenes and loading; stop FollowThem first.";return false;}
  if(!ulong.TryParse(chosen.ContentId,out var content)||content==0){bringMessage="Sync this character’s identity first.";return false;}
  EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==destination.World).Id;
  var house=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses).FirstOrDefault(h=>h.Id==destination.HouseId&&h.World==destination.World);
  var aetheryte=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().Where(a=>a.IsAetheryte&&a.Territory.RowId==destination.Territory).Select(a=>a.RowId).FirstOrDefault();
  if(world==0||house is null&&aetheryte==0){bringMessage="No supported teleport destination for this location. Close this picker and try from an area with an aetheryte or an identified paired estate.";return false;}
  try{if(LifestreamBusy()||shoppingTravel is not null||goSeeHomeTrip is not null||goSeeFriendTrip is not null||DateTimeOffset.UtcNow<characterSwitchUntil||DateTimeOffset.UtcNow<goSeeNativeUntil){bringMessage="Wait for the current trip to finish first.";return false;}}
  catch{bringMessage="Enable Lifestream first.";return false;}
  var source=Player.ContentId;
  if(source!=content){
   var from=CharacterSwitchScope(CurrentSharedCharacter()?.Id??"");var to=CharacterSwitchScope(chosen.Id);
   if(confirmed){characterSwitchConfirmFrom=source;characterSwitchConfirmKey=config.PairingKey;characterSwitchConfirmTarget=chosen.Id;characterSwitchConfirmScope=SwitchScopeKey(from)+">"+SwitchScopeKey(to);}
   SwitchSharedCharacter(chosen,confirmed);characterSwitchConfirmTarget="";
   if(DateTimeOffset.UtcNow>=characterSwitchUntil){bringMessage=characterTravelStatus;return false;}
  }
  bringTrip=new(source,content,world,destination.Territory,aetheryte,house,config.PairingKey,DateTimeOffset.UtcNow.AddMinutes(10));
  bringMessage="Waiting for "+chosen.Name+", then travelling to "+destination.Area+" · "+destination.World+".";Chat.Print("[Equinox] "+bringMessage);return true;
 }
 private void EndBring(string text){bringTrip=null;bringMessage=text;Chat.Print("[Equinox] "+text);}
 private void UpdateBringCharacter(DateTimeOffset now)
 {
  if(bringTrip is not {} trip||now<bringTick)return;bringTick=now.AddMilliseconds(500);
  if(now>trip.Deadline||trip.Pairing!=config.PairingKey||followSession.Armed){EndBring("Bring character cancelled or timed out.");return;}
  if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
  if(Player.ContentId!=trip.Character){if(trip.Stage==0&&Player.ContentId==trip.Source)return;EndBring("Bring character stopped: a different character logged in.");return;}
  if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||HelperInCutscene()){EndBring("Bring character stopped: combat, duty or cutscene started.");return;}
  try{
   if(LifestreamBusy()||now<trip.NotBefore)return;
   characterSwitchUntil=default;
   if(trip.Stage==0){
    if(Player.CurrentWorld.RowId!=trip.World&&!Pi.GetIpcSubscriber<uint,bool>("Lifestream.ChangeWorldById").InvokeFunc(trip.World)){EndBring("Lifestream cannot travel to that world.");return;}
    bringTrip=trip with{Stage=1,NotBefore=now.AddSeconds(3)};return;
   }
   if(Player.CurrentWorld.RowId!=trip.World){EndBring("World travel ended before arrival. Bring character stopped.");return;}
   if(trip.House is {} house){bringTrip=null;VisitSharedHouse(house);Chat.Print("[Equinox] "+characterTravelStatus);return;}
   if(Client.TerritoryType==trip.Territory){EndBring("Arrived in the requested area. Meet your partner there.");return;}
   if(trip.Stage==1){
    if(!Pi.GetIpcSubscriber<uint,byte,bool>("Lifestream.Teleport").InvokeFunc(trip.Aetheryte,0)){EndBring("Area teleport unavailable. Check that this character has unlocked it.");return;}
    bringTrip=trip with{Stage=2,NotBefore=now.AddSeconds(15)};return;
   }
   EndBring("Area arrival could not be confirmed. Bring character stopped.");
  }catch{EndBring("Bring character stopped: Lifestream unavailable.");}
 }
}
