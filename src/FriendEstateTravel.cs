using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private sealed record GoSeeFriendCandidate(ulong Id,string Name,string Kind);
 private sealed class GoSeeFriendTrip
 {
  public required Address Target;public required ulong Actor;public required GoSeeFriendCandidate[] Candidates;
  public DateTimeOffset Started,Next,Opened;public GoSeeFriendCandidate? Friend;public bool MenuSeen;
 }
 private GoSeeFriendTrip? goSeeFriendTrip;
 private unsafe bool StartGoSeeFriend(uint world,string district,int ward,int plot)
 {
  if(world!=Player.CurrentWorld.RowId||followSession.Armed||VisibleFollowAddon("TeleportHousingFriend")||VisibleFollowAddon("SelectYesno"))return false;
  var candidates=new List<GoSeeFriendCandidate>();
  foreach(var c in (config.SharedRoster?.People??[]).SelectMany(p=>p.Characters)){
   if(!ulong.TryParse(c.ContentId,out var id)||id==0||id==Player.ContentId)continue;
   foreach(var h in c.Houses){
    if(!string.Equals(h.World,BidWorldName(world),StringComparison.OrdinalIgnoreCase)||!CharacterTravelPolicy.MatchesEstate(new("",(ushort)world,0,h.Ward,h.Plot,0,false,false,h.World,h.District),world,district,ward,plot))continue;
    var observed=config.Discoveries.LastOrDefault(e=>e.Actor.ContentId==c.ContentId)?.Actor.Name??c.Name;
    var own=h.Type=="Private house"&&string.Equals(h.OwnerName.Trim(),observed.Trim(),StringComparison.OrdinalIgnoreCase);
    var fc=h.Type=="Free Company house"&&c.FcMember!=false&&c.FcId.Length>0&&c.FcId==h.FcId;
    if(own||fc)candidates.Add(new(id,observed,own?"Private Estate":"Free Company Estate"));
   }
  }
  if(candidates.Count==0)return false;
  var friends=InfoProxyFriendList.Instance();if(friends==null)return false;
  var now=DateTimeOffset.UtcNow;
  goSeeFriendTrip=new(){Actor=Player.ContentId,Target=new("",(ushort)world,0,ward,plot,0,false,false,BidWorldName(world),district),Candidates=candidates.Distinct().ToArray(),Started=now,Next=now};
  if(friends->CharData==null||friends->EntryCount==0)friends->RequestData();
  bidTravelMessage="Checking Friends List for a direct estate teleport…";return true;
 }
 private unsafe void UpdateGoSeeFriend(DateTimeOffset now)
 {
  var trip=goSeeFriendTrip;if(trip==null||now<trip.Next)return;trip.Next=now.AddMilliseconds(500);
  if(!FriendEstateTravelPolicy.SameSession(trip.Actor,trip.Target.WorldId,Player.ContentId,Player.CurrentWorld.RowId,Player.IsLoaded,Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.BoundByDuty])||followSession.Armed||DateTimeOffset.UtcNow<characterSwitchUntil){goSeeFriendTrip=null;bidTravelMessage="Estate lookup cancelled: character or travel state changed.";return;}
  try{
   if(shoppingTravel is not null||LifestreamBusy()){goSeeFriendTrip=null;bidTravelMessage="Estate lookup stopped because another trip started.";return;}
   if(trip.Friend==null){
    var friends=InfoProxyFriendList.Instance();
    if(friends!=null&&friends->CharData!=null&&friends->EntryCount is >0 and <=200){
     foreach(var friend in friends->CharDataSpan){var candidate=trip.Candidates.FirstOrDefault(x=>x.Id==friend.ContentId);if(candidate is null)continue;
      if(VisibleFollowAddon("TeleportHousingFriend")||VisibleFollowAddon("SelectYesno")){goSeeFriendTrip=null;bidTravelMessage="Close the open travel dialog and click GO SEE again.";return;}
      var agent=AgentFriendlist.Instance();if(agent==null)break;
      trip.Friend=candidate with {Name=friend.NameString};trip.Opened=now;
      usingSharedTravel=true;try{agent->OpenFriendEstateTeleportation(candidate.Id);}finally{usingSharedTravel=false;}
      bidTravelMessage="Checking "+trip.Friend.Name+"’s estate teleport…";return;
     }
     FallBackGoSeeFriend(trip);return;
    }
    if(now-trip.Started>=TimeSpan.FromSeconds(4))FallBackGoSeeFriend(trip);return;
   }
   if(VisibleFollowAddon("SelectYesno")){goSeeFriendTrip=null;bidTravelMessage="A game confirmation is open. Resolve it before starting another trip.";return;}
   var menuVisible=VisibleFollowAddon("TeleportHousingFriend");
   if(trip.MenuSeen&&!menuVisible){goSeeFriendTrip=null;bidTravelMessage="Estate travel cancelled when its window closed.";return;}
   trip.MenuSeen|=menuVisible;
   if(TrySelectFriendEstateRow(trip.Friend.Id,trip.Friend.Kind,trip.Target,uint.MaxValue)){
    goSeeFriendTrip=null;bidTravelMessage="Direct estate teleport requested through "+trip.Friend.Name+"’s Friends List entry. Confirm any teleport prompt in game.";characterTravelStatus=bidTravelMessage;return;
   }
   if(now-trip.Opened>=TimeSpan.FromSeconds(5))FallBackGoSeeFriend(trip);
  }catch(Exception){FallBackGoSeeFriend(trip);}
 }
 private unsafe void FallBackGoSeeFriend(GoSeeFriendTrip trip)
 {
  goSeeFriendTrip=null;
  var addon=(AtkUnitBase*)GardenGui.GetAddonByName("TeleportHousingFriend").Address;
  if(addon!=null&&addon->IsVisible){
   if(trip.Friend==null||!FriendEstateMenuBelongsTo(addon,trip.Friend.Name)){bidTravelMessage="Estate window changed. Close it and click GO SEE again.";return;}
   addon->Close(true);
  }
  if(VisibleFollowAddon("SelectYesno")){bidTravelMessage="Resolve the game confirmation before starting another trip.";return;}
  GoSeeAddress(trip.Target.WorldId,trip.Target.DistrictName??"",trip.Target.Ward,trip.Target.Plot);
  bidTravelMessage="Direct friend teleport unavailable. "+bidTravelMessage;characterTravelStatus=bidTravelMessage;
 }
}
