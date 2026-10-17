using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private sealed record GoSeeHomeTrip(ulong Actor,uint World,string District,int Ward,int Plot,DateTimeOffset Started,string Pairing);
 private GoSeeHomeTrip? goSeeHomeTrip;private DateTimeOffset goSeeHomeTick;
 private bool StartGoSeeHome(uint world,string district,int ward,int plot)
 {
  if(world==0||world==Player.CurrentWorld.RowId||!CharacterTravelPolicy.NeedsHomeReturn(world,Player.HomeWorld.RowId,Player.CurrentWorld.RowId)&&GoSeeFriendCandidates(world,district,ward,plot).Length==0)return false;
  if(followSession.Armed){bidTravelMessage="Stop FollowThem before starting GO SEE travel.";return true;}
  if(Conditions[ConditionFlag.BoundByDuty]){bidTravelMessage="Leave your duty before starting estate travel.";return true;}
  var trip=new GoSeeHomeTrip(Player.ContentId,world,district,ward,plot,DateTimeOffset.UtcNow,config.PairingKey);
  if(!Pi.GetIpcSubscriber<uint,bool>("Lifestream.ChangeWorldById").InvokeFunc(world)){
   bidTravelMessage="Lifestream could not start the return to your home world. Check its travel settings, then try GO SEE again.";return true;
  }
  goSeeHomeTrip=trip;goSeeHomeTick=trip.Started.AddSeconds(2);
  bidTravelMessage=$"Traveling to {BidWorldName(world)} first, then checking available estate and friends-list teleports for {district} W{ward} P{plot}.";return true;
 }
 private void UpdateGoSeeHome(DateTimeOffset now)
 {
  if(goSeeHomeTrip is not {} trip||now<goSeeHomeTick)return;goSeeHomeTick=now.AddMilliseconds(500);
  if(now-trip.Started>TimeSpan.FromMinutes(10)||trip.Pairing!=config.PairingKey||followSession.Armed||now<characterSwitchUntil){EndGoSeeHome("Estate travel continuation cancelled.");return;}
  if(!Player.IsLoaded)return; // DC travel can log out and back into the same character.
  if(!CharacterTravelPolicy.SameHomeTraveler(trip.Actor,Player.ContentId)){EndGoSeeHome("Estate travel cancelled: character changed.");return;}
  if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
  if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||shoppingTravel is not null){EndGoSeeHome("Estate travel stopped because your character or travel state changed.");return;}
  try{
   if(LifestreamBusy())return;
   if(Player.CurrentWorld.RowId!=trip.World){if(now-trip.Started>TimeSpan.FromSeconds(5))EndGoSeeHome("Return home stopped before arrival. Click GO SEE to try again.");return;}
   goSeeHomeTrip=null;
   if(!TryGoSeeEstateTeleport(trip.World,trip.District,trip.Ward,trip.Plot)&&!StartGoSeeFriend(trip.World,trip.District,trip.Ward,trip.Plot))GoSeeAddress(trip.World,trip.District,trip.Ward,trip.Plot);
   characterTravelStatus=bidTravelMessage;
  }catch(Exception){EndGoSeeHome("Estate travel stopped: Lifestream became unavailable.");}
 }
 private void EndGoSeeHome(string message){goSeeHomeTrip=null;bidTravelMessage=message;characterTravelStatus=message;}
 private void DrawGoSeeHomeStop()
 {
  if(goSeeHomeTrip is null)return;
  if(ImGui.SmallButton("Stop GO SEE")){
   goSeeHomeTrip=null;
   try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}
   EndGoSeeHome("GO SEE stopped.");
  }
 }
}
