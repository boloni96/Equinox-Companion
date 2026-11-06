using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private string PublicAdventureWorld()
 {
  var ready=Player.IsLoaded&&Player.ContentId!=0&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
  return AdventureWorldName.Read(ready,Player.CurrentWorld);
 }
 private readonly UiProjectionCache<int?> adventureLocationStop=new(maxAgeMs:0);
 private readonly UiProjectionCache<string> adventureLocationHouse=new(maxAgeMs:0);
 private readonly UiProjectionCache<string> adventureAreaLabel=new(maxAgeMs:0);
 private unsafe string CurrentAdventureArea()
 {
  var manager=FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
  var inside=manager!=null&&manager->IsInside();var address=currentAddress;
  return adventureAreaLabel.Get(config.SharedRoster,$"{Client.TerritoryType}:{inside}:{address}",Environment.TickCount64,()=>{
   var area=DistrictName(Client.TerritoryType)??"Area unavailable";
   if(!inside||address is null)return area;
   var district=address.DistrictName??area;
   if(address.Workshop)return "FC workshop · "+district;
   if(address.Apartment)return "Apartment · "+district;
   if(address.Room>0)return "Private chambers · "+district;
   var houses=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses);
   var id=AdventurePolicy.CurrentHouse(address,Player.CurrentWorld.RowId,houses);
   var house=houses.FirstOrDefault(h=>h.Id==id);
   return (house?.Type=="Free Company house"?"FC house":house?.Type=="Private house"?"Private house":"House")+" · "+district;
  });
 }
 private readonly AdventureLocationSession adventurePresenceSession=new();
 private DateTimeOffset adventurePresenceTick;
 private void UpdateAdventurePresence(DateTimeOffset now)
 {
  if(now<adventurePresenceTick)return;adventurePresenceTick=now.AddMilliseconds(500);
  if(!config.SyncEnabled||config.PairingKey.Length!=64||!Player.IsLoaded||Player.ContentId==0||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){adventurePresenceSession.Reset();return;}
  if(!adventureFresh||adventureRequest is not null||AdventureSaving)return;
  var character=CurrentSharedCharacter();if(character is null)return;
  var world=PublicAdventureWorld();if(world.Length==0){adventurePresenceSession.Reset();return;}
  var area=CurrentAdventureArea();var territory=(uint)Client.TerritoryType;
  var house=adventureLocationHouse.Get(config.SharedRoster,$"{Player.CurrentWorld.RowId}:{currentAddress}",Environment.TickCount64,
   ()=>AdventurePolicy.CurrentHouse(currentAddress,Player.CurrentWorld.RowId,(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses)));
  var board=adventureBoards.FirstOrDefault(b=>b.PresenceReporting&&b.Kind=="multi"&&b.Stops.Any(s=>s.CharacterIds?.Contains(character.Id)==true)&&
   (b.Locations?.GetValueOrDefault(character.Id) is not {} location||location.World!=world||location.Area!=area||location.Territory!=territory||location.HouseId!=house));
  if(board is null){adventurePresenceSession.Reset();return;}
  var observation=adventurePresenceSession.Observe(true,$"{config.PairingKey}:{board.Id}:{board.Generation}:{Player.ContentId}:{world}:{territory}:{area}",house,now);
  if(!observation.Changed)return;
  adventureMutation=true;adventureRequestKey=config.PairingKey;
  adventureRequest=adventureSync.Location(config.PairingKey,board,character.Id,world,area,territory,house);
 }
 private void DrawAdventureLocation(AdventureBoard board)=>MeasureWork("Adventure: location",()=>DrawAdventureLocationMeasured(board));
 private void DrawAdventureLocationMeasured(AdventureBoard board)
 {
  var world=PublicAdventureWorld();var ready=world.Length>0;
  var house=ready?adventureLocationHouse.Get(config.SharedRoster,$"{Player.CurrentWorld.RowId}:{currentAddress}",Environment.TickCount64,
   ()=>AdventurePolicy.CurrentHouse(currentAddress,Player.CurrentWorld.RowId,(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses))):"";
  var target=adventureLocationStop.Get(board,world+"|"+house,Environment.TickCount64,()=>AdventurePolicy.HouseIndex(board,house,world));
  if(ImGui.Checkbox("Follow character",ref adventureFollow))adventureFollowSession.Reset();
  if(ImGui.IsItemHovered())ImGui.SetTooltip("Follow my character / world / house. Automatically open the matching stop when my location changes.");
  if(ready&&target is not null){
  ImGui.BeginDisabled(false);
  if(ImGui.SmallButton("Open current house")&&target is {} index){adventureFocusHouse=house;if(index!=board.Index){adventurePrompt=null;AdventureAction(board,index);}}
  ImGui.EndDisabled();
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(target is null?"Your current house must be identified and included in this adventure.":"Open this house’s stop in the active adventure. This changes only this game client’s page and does not mark tasks complete.");
  }
  var navigation=CompactLayout.Navigation(ImGui.GetContentRegionAvail().X,BringControlsWidth,ImGui.GetStyle().ItemSpacing.X,ImGui.GetFontSize());
  DrawAdventureNavigation(board,navigation.SelectorWidth);
  if(!navigation.WrapActions)ImGui.SameLine();
  var selected=board.Stops.ElementAtOrDefault(board.Index);
  DrawBringCharacter(ready,selected?.World??world,selected?.World==world?house:"");
  if(!adventureFollow){adventureFollowSession.Reset();ClearAdventureFollowIntent();return;}
  if(!ready){adventureFollowSession.Reset();ClearAdventureFollowIntent();return;}
  // Local page following must not wait for a cloud read, checkmark or presence upload.
  var cid=CurrentSharedCharacter()?.Id??"";
  var observation=adventureFollowSession.Observe(true,$"{board.Id}:{board.Generation}:{config.PairingKey}:{Player.ContentId}:{cid}:{Player.CurrentWorld.RowId}:{Client.TerritoryType}",house,DateTimeOffset.UtcNow);
  if(observation.Changed){
   var next=AdventureFollowSelection.Index(board,cid,world,house);
   if(target is not null&&house.Length>0)adventureFocusHouse=house;
   if(next is {} n&&n!=board.Index)AdventureAction(board,n);
   if(observation.ContextChanged&&board.TravelGrouping&&board.Kind=="multi"&&cid.Length>0&&board.Stops.Any(s=>s.CharacterIds?.Contains(cid)==true))
    adventurePendingTravel=new(board.Id,board.Generation,Player.ContentId,cid,world,house,CurrentAdventureArea(),Client.TerritoryType);
  }
  SendAdventureFollowTravel(board,world,house);
 }
 private sealed record AdventureFollowIntent(string Board,string Generation,ulong Actor,string Character,string World,string House,string Area,uint Territory);
 private AdventureFollowIntent? adventurePendingTravel,adventureFollowReply;
 private void ClearAdventureFollowIntent(){adventurePendingTravel=null;adventureFollowReply=null;}
 private bool AdventureFollowIntentCurrent(AdventureFollowIntent intent)=>adventureFollow&&Player.IsLoaded&&Player.ContentId==intent.Actor&&adventureSelected==intent.Board&&PublicAdventureWorld()==intent.World&&Client.TerritoryType==intent.Territory;
 private void SendAdventureFollowTravel(AdventureBoard board,string world,string house)
 {
  if(adventurePendingTravel is not {} intent)return;
  if(!AdventureFollowIntentCurrent(intent)||intent.Generation!=board.Generation||intent.World!=world||intent.House!=house){adventurePendingTravel=null;return;}
  if(!adventureFresh||AdventureSaving||adventureRequest is not null)return;
  adventurePendingTravel=null;adventureFollowReply=intent;
  adventureMutation=true;adventureRequestKey=config.PairingKey;
  adventureRequest=adventureSync.Travel(config.PairingKey,board,intent.Character,intent.World,intent.Area,intent.Territory,intent.House);
 }
 private void ApplyAdventureFollowReply()
 {
  if(adventureFollowReply is not {} intent)return;adventureFollowReply=null;
  if(!AdventureFollowIntentCurrent(intent))return;
  var board=adventureBoards.FirstOrDefault(b=>b.Id==intent.Board&&b.Generation==intent.Generation);if(board is null)return;
  var house=AdventurePolicy.CurrentHouse(currentAddress,Player.CurrentWorld.RowId,(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses));
  if(house!=intent.House)return;
  var index=AdventureFollowSelection.Index(board,intent.Character,intent.World,intent.House);
  if(index is not {} n)return;
  var local=adventureView.Move(board,n);adventureBoards=adventureBoards.Select(b=>b.Id==local.Id?local:b).ToArray();adventurePrompt=null;
 }
}
