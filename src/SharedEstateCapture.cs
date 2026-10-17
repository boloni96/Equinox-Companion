using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private readonly SharedEstateObservationGate sharedEstateGate=new();
 private unsafe void ObserveSharedEstates(Actor actor,DateTimeOffset now)
 {
  var state=UIState.Instance();
  var ready=Player.IsLoaded&&state!=null&&state->PlayerState.IsLoaded&&now>=characterReadyAt&&actor.CurrentWorldId==actor.HomeWorldId&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
  var raw=ready?new[]{HousingManager.GetOwnedHouseId(EstateType.SharedEstate,0),HousingManager.GetOwnedHouseId(EstateType.SharedEstate,1)}:[];
  var houses=raw.Select(AddressOf).OfType<Address>().ToArray();
  // A valid owned/shared estate proves that the housing identity cache is populated.
  // All-zero unavailable data cannot revoke manual tenant links.
  ready=ready&&raw.All(id=>id.Id==0||id.Id==ulong.MaxValue||AddressOf(id) is {Apartment:false,Workshop:false,Room:0} a&&a.WorldId==actor.HomeWorldId)&&
   (houses.Length>0||AddressOf(HousingManager.GetOwnedHouseId(EstateType.PersonalEstate)) is {} own&&own.WorldId==actor.HomeWorldId||AddressOf(HousingManager.GetOwnedHouseId(EstateType.FreeCompanyEstate)) is {} fc&&fc.WorldId==actor.HomeWorldId);
  var stamp=string.Join(",",raw.Select(id=>id.Id.ToString("X16")));
  if(!sharedEstateGate.Ready(actor.ContentId,stamp,ready,now))return;
  KeepDiscovery(new(Guid.NewGuid().ToString("N"),"house.shared-estates",now,actor,null,SharedEstates:new("shared-estate-slots",houses)));
 }
}
