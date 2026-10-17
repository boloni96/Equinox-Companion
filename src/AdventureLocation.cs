using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private readonly UiProjectionCache<int?> adventureLocationStop=new(maxAgeMs:0);
 private readonly UiProjectionCache<string> adventureLocationHouse=new(maxAgeMs:0);
 private void DrawAdventureLocation(AdventureBoard board)
 {
  var ready=Player.IsLoaded&&Player.ContentId!=0&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
  var world=WorldName(Player.CurrentWorld.RowId)??"";
  var house=ready?adventureLocationHouse.Get(config.SharedRoster,$"{Player.CurrentWorld.RowId}:{currentAddress}",Environment.TickCount64,
   ()=>AdventurePolicy.CurrentHouse(currentAddress,Player.CurrentWorld.RowId,(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses))):"";
  var target=adventureLocationStop.Get(board,world+"|"+house,Environment.TickCount64,()=>AdventurePolicy.HouseIndex(board,house,world));
  if(ImGui.Checkbox("Follow my character / world / house",ref adventureFollow))adventureFollowSession.Reset();
  if(ready&&target is not null){
  ImGui.BeginDisabled(AdventureSaving||!adventureFresh);
  if(ImGui.SmallButton("Open current house")&&target is {} index){adventureFocusHouse=house;if(index!=board.Index){adventurePrompt=null;AdventureAction(board,index);}}
  ImGui.EndDisabled();
  if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(target is null?"Your current house must be identified and included in this adventure.":"Open this house’s stop in the active adventure. This shares navigation with the Journal and does not mark tasks complete.");
  }
  if(!adventureFollow){adventureFollowSession.Reset();return;}
  // Observe loading even while a request is running; consume a move only when it can be sent.
  if(!ready){adventureFollowSession.Reset();return;}
  if(!adventureFresh||AdventureSaving||adventureRequest is not null)return;
  var observation=adventureFollowSession.Observe(true,$"{board.Id}:{board.Generation}:{config.PairingKey}:{Player.ContentId}:{Player.CurrentWorld.RowId}",house,DateTimeOffset.UtcNow);
  if(!observation.Changed)return;
  if(target is not null&&house.Length>0)adventureFocusHouse=house;
  int? next=target;
  if(next is null&&observation.ContextChanged){
   var actorId=Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
   var matches=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.ContentId==actorId).Take(2).ToArray();
   next=AdventurePolicy.FollowIndex(board,matches.Length==1?matches[0].Id:"",world);
  }
  if(next is {} n&&n!=board.Index){adventurePrompt=null;AdventureAction(board,n);}
 }
}
