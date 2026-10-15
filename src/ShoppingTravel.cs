using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public static class ShoppingDestination
{
 public static readonly string[] Cities=["Limsa Lominsa","Gridania","Ul'dah"];
 public static uint Aetheryte(int city)=>city switch{1=>2,2=>9,_=>8};
 public static string Shard(int city,int arrival)=>arrival==0?"":city switch{1=>"Leatherworkers' Guild",2=>"Sapphire Avenue Exchange",_=>"Hawkers' Alley"};
}
public sealed partial class Plugin
{
 private sealed record ShoppingTrip(ulong Character,uint World,int City,string Shard,DateTimeOffset Deadline,int Stage=0,DateTimeOffset NotBefore=default);
 private ShoppingTrip? shoppingTravel;private DateTimeOffset shoppingTravelTick;
 private void DrawShoppingDestination(){
  var city=Math.Clamp(config.ShoppingCity,0,2);var arrival=Math.Clamp(config.ShoppingArrival,0,1);
  ImGui.SetNextItemWidth(160);if(ImGui.Combo("Shopping city",ref city,"Limsa Lominsa\0Gridania\0Ul'dah\0")){config.ShoppingCity=city;config.ShoppingArrival=0;arrival=0;SaveConfiguration();}
  ImGui.SameLine();ImGui.SetNextItemWidth(180);if(ImGui.Combo("Arrival",ref arrival,"Main aetheryte\0"+ShoppingDestination.Shard(city,1)+"\0")){config.ShoppingArrival=arrival;SaveConfiguration();}
  if(shoppingTravel is not null){ImGui.SameLine();if(ImGui.SmallButton("Stop travel")){shoppingTravel=null;try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}marketTravelStatus="Shopping travel stopped.";}}
 }
 private void BeginShoppingTravel(uint world){
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){marketTravelStatus="Wait until you are out of combat and loading, then click Travel.";return;}
  marketTravelNext=DateTimeOffset.UtcNow.AddSeconds(2);
  try{
   if(shoppingTravel is not null||LifestreamBusy()){marketTravelStatus="Travel is already in progress.";return;}
   var city=Math.Clamp(config.ShoppingCity,0,2);var shard=ShoppingDestination.Shard(city,config.ShoppingArrival);
   if(Player.CurrentWorld.RowId!=world&&!Pi.GetIpcSubscriber<uint,bool>("Lifestream.ChangeWorldById").InvokeFunc(world)){marketTravelStatus="Lifestream cannot travel to that world.";return;}
   shoppingTravel=new(Player.ContentId,world,city,shard,DateTimeOffset.UtcNow.AddMinutes(10),NotBefore:DateTimeOffset.UtcNow.AddSeconds(2));
   marketTravelStatus=$"Travelling to {ShoppingDestination.Cities[city]}{(shard.Length>0?" → "+shard:"")} on the listing's world.";
   if(Player.CurrentWorld.RowId!=world)ShareFollowWorldIntent(world);
  }catch(Exception){marketTravelStatus="Lifestream must be installed and enabled for shopping travel.";}
 }
 private void UpdateShoppingTravel(DateTimeOffset now){
  if(shoppingTravel is not {} trip||now<shoppingTravelTick)return;shoppingTravelTick=now.AddMilliseconds(500);
  if(now>trip.Deadline){shoppingTravel=null;marketTravelStatus="Shopping travel timed out. Check Lifestream, then try again.";return;}
  if(!Player.IsLoaded)return; // Cross-DC travel logs out and back in; never travel a different character.
  if(Player.ContentId!=trip.Character){shoppingTravel=null;marketTravelStatus="Shopping travel cancelled: character changed.";return;}
  if(Player.CurrentWorld.RowId!=trip.World||now<trip.NotBefore||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
  if(Conditions[ConditionFlag.InCombat]){shoppingTravel=null;marketTravelStatus="Shopping travel stopped during combat.";return;}
  try{
   if(LifestreamBusy())return;
   if(trip.Stage==0){if(!Pi.GetIpcSubscriber<uint,byte,bool>("Lifestream.Teleport").InvokeFunc(ShoppingDestination.Aetheryte(trip.City),0)){shoppingTravel=null;marketTravelStatus="City teleport unavailable. Check that the aetheryte is unlocked.";return;}shoppingTravel=trip with{Stage=1,NotBefore=now.AddSeconds(10)};return;}
   if(trip.Stage==1){var active=Pi.GetIpcSubscriber<uint>("Lifestream.GetActiveAetheryte").InvokeFunc();if(active!=ShoppingDestination.Aetheryte(trip.City)){if(now>trip.NotBefore.AddSeconds(20)){shoppingTravel=null;marketTravelStatus="City arrival could not be confirmed. Travel stopped.";}return;}
    if(trip.Shard.Length>0){if(!Pi.GetIpcSubscriber<string,bool>("Lifestream.AethernetTeleport").InvokeFunc(trip.Shard)){shoppingTravel=null;marketTravelStatus="Shard travel unavailable. Check that the shard is unlocked.";return;}marketTravelStatus="Lifestream is taking you to "+trip.Shard+".";}else marketTravelStatus="Arrived at "+ShoppingDestination.Cities[trip.City]+".";shoppingTravel=null;
   }
  }catch(Exception){shoppingTravel=null;marketTravelStatus="Lifestream travel became unavailable.";}
 }
}
