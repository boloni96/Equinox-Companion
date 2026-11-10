using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
 private float BringControlsWidth => ButtonWidth("Bring here") + (bringTrip is null ? 0 : ImGui.GetStyle().ItemSpacing.X + ButtonWidth("Cancel bring"));
 private void DrawBringCharacter(bool ready,string world,string house)
 {
  ImGui.BeginDisabled(!ready||world.Length==0||bringTrip is not null);
  if(ImGui.SmallButton("Bring here")){bringDestination=new AdventurePresence(world,DateTimeOffset.UtcNow.ToString("O"),world==PublicAdventureWorld()?CurrentAdventureArea():"",world==PublicAdventureWorld()?(uint)Client.TerritoryType:0,house);bringChoice=CurrentSharedCharacter()?.Id??"";bringOpenedActor=Player.ContentId;bringOpenedPairing=config.PairingKey;bringMessage="";ImGui.OpenPopup("Bring character");}
  ImGui.EndDisabled();
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Bring a paired character to the world selected above. When it is your current world, also return to your current area or estate entrance. Travel does not complete tasks or add characters to the route.");
  if(bringTrip is not null){SameLineIfFits(ButtonWidth("Cancel bring"));if(ImGui.SmallButton("Cancel bring")){bringTrip=null;StopNativeWorldTravel("Bring character cancelled.");CancelAssignedLogin("Bring character cancelled.");try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}characterSwitchUntil=default;bringMessage="Bring character cancelled.";}}
  if(!ImGui.BeginPopupModal("Bring character",ImGuiWindowFlags.AlwaysAutoResize))return;
  var report=bringDestination;
  ImGui.TextUnformatted("Bring a character to this location");
  ImGui.TextWrapped(report is null?"Choose your current location again.":report.Area+" · "+report.World);
  ImGui.TextWrapped("Destination captured from your selected world. Current-world trips also use your current area or estate entrance.");
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
  var cross=chosen is null||CharacterLoginNeedsConfirmation(chosen,from,to);
  if(cross)ImGui.TextWrapped("Square Enix login not verified: another service account can use this login; a different login needs its own game client.");
  if(bringMessage.Length>0)ImGui.TextWrapped(bringMessage);
  ImGui.BeginDisabled(chosen is null||report is null||bringTrip is not null||!Player.IsLoaded||Player.ContentId!=bringOpenedActor||bringOpenedPairing!=config.PairingKey);
  if(ImGui.Button(cross?"Confirm same login and travel":"Switch and travel")){
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
  if(world==0||destination.Territory!=0&&house is null&&aetheryte==0){bringMessage="No supported teleport destination for this location. Close this picker and try from an area with an aetheryte or an identified paired estate.";return false;}
  try{if(nativeHousingTrip!=null||nativeWorldTrip!=null||nativeAethernetTrip!=null||LifestreamBusy()||shoppingTravel is not null||goSeeHomeTrip is not null||goSeeFriendTrip is not null||DateTimeOffset.UtcNow<characterSwitchUntil||DateTimeOffset.UtcNow<goSeeNativeUntil){bringMessage="Wait for the current trip to finish first.";return false;}}
  catch{bringMessage="Travel state is unavailable. Wait for the game to settle.";return false;}
  var home=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().FirstOrDefault(w=>w.Name.ToString()==chosen.World);
  var targetWorld=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(world);
  if(home.RowId==0||targetWorld==null||!targetWorld.Value.IsPublic||!NativeTravelPolicy.RegionReachable(home.LogicalDc().Region.RowId,targetWorld.Value.LogicalDc().Region.RowId)){bringMessage="This character cannot travel to the selected world under the supported regional travel rules.";return false;}
  var source=Player.ContentId;
  if(source!=content){
   var from=CharacterSwitchScope(CurrentSharedCharacter()?.Id??"");var to=CharacterSwitchScope(chosen.Id);
   if(confirmed){characterSwitchConfirmFrom=source;characterSwitchConfirmKey=config.PairingKey;characterSwitchConfirmTarget=chosen.Id;characterSwitchConfirmScope=SwitchScopeKey(from)+">"+SwitchScopeKey(to);}
   SwitchSharedCharacter(chosen,confirmed);characterSwitchConfirmTarget="";
   if(DateTimeOffset.UtcNow>=characterSwitchUntil){bringMessage=characterTravelStatus;return false;}
  }
  bringTrip=new(source,content,world,destination.Territory,aetheryte,house,config.PairingKey,DateTimeOffset.UtcNow.AddMinutes(30));
  bringMessage="Waiting for "+chosen.Name+", then travelling to "+destination.Area+" · "+destination.World+".";PrintEquinox("[Equinox] "+bringMessage);return true;
 }
 private void EndBring(string text){
  var trip=bringTrip;bringTrip=null;bringMessage=text;
  if(nativeWorldTrip?.Owner=="bring")StopNativeWorldTravel(text);
  if(trip!=null&&assignedLogin?.Target.ContentId==trip.Character.ToString())CancelAssignedLogin(text);
  characterSwitchUntil=default;characterSwitchTarget=null;
  PrintEquinox("[Equinox] "+text);
 }
 // Only hand over after AssignedLogin has selected and verified the exact account
 // and character. Never use cached presence as the source of a DC transfer.
 private unsafe bool TryPrepareBringBeforeLogin(AssignedLogin run)
 {
  if(bringTrip is not {} bring||bring.Stage!=0||run.Target.ContentId!=bring.Character.ToString())return false;
  if(bring.Pairing!=config.PairingKey||DateTimeOffset.UtcNow>=bring.Deadline)throw new InvalidOperationException("Bring destination expired or pairing changed.");
  var lobby=AgentLobby.Instance();
  if(lobby==null||lobby->TemporaryLocked||lobby->ServiceAccountIndex!=run.ServiceAccount||SellerAddon("SelectYesno")!=null||SellerAddon("SelectOk")!=null||SellerAddon("ContextMenu")!=null)return true;
  var home=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().FirstOrDefault(w=>w.Name.ToString()==run.Target.World).RowId;
  var trip=new NativeWorldTrip{Owner="bring",Character=bring.Character,Name=run.Target.Name,Home=home,Source=0,Destination=bring.World,Leg=bring.World,Service=run.ServiceAccount,Started=DateTimeOffset.UtcNow,
   PreferredGateway=TravelCityPolicy.Preferred(config.TravelCity,config.ShoppingCity,false)};
  if(NativeTravelCharacterIndex(trip,out var source)<0)return true;
  var sourceDc=TravelDc(source);var destinationDc=TravelDc(bring.World);
  if(home==0||sourceDc==0||destinationDc==0||sourceDc!=TravelDc(lobby->WorldId))throw new InvalidOperationException("Character world could not be verified before travel.");
  if(UsesWorldVisit(source,bring.World)||!NativeTravelPolicy.TransferBeforeLogin(source,bring.World,sourceDc,destinationDc))return false;
  if(nativeWorldTrip!=null||nativeHousingTrip!=null||nativeAethernetTrip!=null)throw new InvalidOperationException("Another trip started before character travel.");
  trip.Source=source;trip.ReturnHome=NativeTravelPolicy.ReturnFirst(source,home,sourceDc,destinationDc);trip.Leg=trip.ReturnHome?home:bring.World;
  assignedLogin=null;characterSwitchUntil=default;characterSwitchTarget=null;
  nativeWorldTrip=trip;
  // Stage 1 prevents an interrupted native transfer from being silently retried.
  bringTrip=bring with{Stage=1};
  bringMessage="Preparing "+run.Target.Name+" for "+WorldName(bring.World)+" before login.";
  characterTravelStatus=bringMessage;
  SetNativeTravelStage(trip,NativeTravelStage.CharacterMenu,trip.ReturnHome?"Returning the selected character home before login":"Preparing destination before login");
  RecordTravelPrompt("Bring handed to DC travel before login",new{trip.Character,trip.Service,trip.Source,trip.Home,trip.Destination});
  return true;
 }
 private void UpdateBringCharacter(DateTimeOffset now)
 {
  if(bringTrip is not {} trip||now<bringTick)return;bringTick=now.AddMilliseconds(500);
  if(now>trip.Deadline||trip.Pairing!=config.PairingKey||followSession.Armed){EndBring("Bring character cancelled or timed out.");return;}
  if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
  if(Player.ContentId!=trip.Character){if(trip.Stage==0&&Player.ContentId==trip.Source)return;EndBring("Bring character stopped: a different character logged in.");return;}
  if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||HelperInCutscene()){EndBring("Bring character stopped: combat, duty or cutscene started.");return;}
  try{
   if(nativeHousingTrip!=null||nativeWorldTrip!=null||LifestreamBusy()||now<trip.NotBefore)return;
   characterSwitchUntil=default;
   if(trip.Stage==0){
    if(Player.CurrentWorld.RowId!=trip.World&&!StartNativeWorldTravel(trip.World,"bring")){EndBring(nativeTravelStatus);return;}
    bringTrip=trip with{Stage=1,NotBefore=now.AddSeconds(3)};return;
   }
   if(Player.CurrentWorld.RowId!=trip.World){EndBring("World travel ended before arrival. Bring character stopped.");return;}
   if(trip.Territory==0){EndBring("Arrived in the selected world.");return;}
   if(trip.House is {} house){bringTrip=null;VisitSharedHouse(house);PrintEquinox("[Equinox] "+characterTravelStatus);return;}
   if(Client.TerritoryType==trip.Territory){EndBring("Arrived in the requested area. Meet your partner there.");return;}
   if(trip.Stage==1){
    if(!RequestNativePublicTeleport(trip.Aetheryte)){EndBring("Area teleport unavailable. Check that this character has unlocked it.");return;}
    bringTrip=trip with{Stage=2,NotBefore=now.AddSeconds(15)};return;
   }
   EndBring("Area arrival could not be confirmed. Bring character stopped.");
  }catch{EndBring("Bring character stopped: native travel unavailable.");}
 }
}

