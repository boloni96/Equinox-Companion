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
 private readonly ShoppingTravelGate shoppingTravelGate=new();
 private ShoppingTrip? shoppingTravel;private DateTimeOffset shoppingTravelTick;
 private void DrawShoppingDestination(){
  var city=Math.Clamp(config.ShoppingCity,0,2);var arrival=Math.Clamp(config.ShoppingArrival,0,1);
  ImGui.SetNextItemWidth(160);if(ImGui.Combo("Shopping city",ref city,"Limsa Lominsa\0Gridania\0Ul'dah\0")){config.ShoppingCity=city;config.ShoppingArrival=0;arrival=0;SaveConfiguration();}
  ImGui.SameLine();ImGui.SetNextItemWidth(180);if(ImGui.Combo("Arrival",ref arrival,"Main aetheryte\0"+ShoppingDestination.Shard(city,1)+"\0")){config.ShoppingArrival=arrival;SaveConfiguration();}
  if(shoppingTravel is not null){ImGui.SameLine();if(ImGui.SmallButton("Stop travel")){shoppingTravel=null;StopNativeWorldTravel("Shopping travel stopped.");StopNativeAethernet("Shopping travel stopped.");try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}marketTravelStatus="Shopping travel stopped.";}}
 }
 private void BeginShoppingTravel(uint world){
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){marketTravelStatus="Wait until you are out of combat and loading, then click Travel.";return;}
  marketTravelNext=DateTimeOffset.UtcNow.AddSeconds(2);
  try{
   if(NativeGardenWalking||shoppingTravel is not null||nativeHousingTrip!=null||nativeWorldTrip!=null||nativeAethernetTrip!=null||LifestreamBusy()){marketTravelStatus="Travel is already in progress.";return;}
   var city=Math.Clamp(config.ShoppingCity,0,2);var shard=ShoppingDestination.Shard(city,config.ShoppingArrival);
   if(Player.CurrentWorld.RowId!=world&&!StartNativeWorldTravel(world,"shopping")){marketTravelStatus=nativeTravelStatus;return;}
   shoppingTravelGate.Reset();
   shoppingTravel=new(Player.ContentId,world,city,shard,DateTimeOffset.UtcNow.AddMinutes(10),NotBefore:DateTimeOffset.UtcNow.AddSeconds(2));
   marketTravelStatus=$"Travelling to {ShoppingDestination.Cities[city]}{(shard.Length>0?" → "+shard:"")} on the listing's world.";
   if(Player.CurrentWorld.RowId!=world)ShareFollowWorldIntent(world);
  }catch(Exception){marketTravelStatus="Shopping travel could not start. Check the current game prompt.";}
 }
 private void EndShoppingTravel(string message){
  shoppingTravel=null;marketTravelStatus=message;RecordTravelPrompt("Shopping journey ended",new{message});
 }
 private bool ShoppingCityArrived(uint id)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().GetRowOrDefault(id)?.Territory.RowId==Client.TerritoryType&&
  Objects.LocalPlayer is {} self&&IndependentCrystalPosition(id) is {} point&&System.Numerics.Vector3.Distance(self.Position,point)<35;
 private void UpdateShoppingTravel(DateTimeOffset now){
  if(shoppingTravel is not {} trip||now<shoppingTravelTick)return;shoppingTravelTick=now.AddMilliseconds(500);
  if(now>trip.Deadline){StopNativeWorldTravel("Shopping trip timed out");StopNativeAethernet("Shopping trip timed out");EndShoppingTravel("Shopping travel timed out. Export diagnostics to review the last step.");return;}
  if(!Player.IsLoaded){shoppingTravelGate.Ready(now,false);if(nativeWorldTrip==null&&trip.Stage==0)EndShoppingTravel("World travel stopped before login. "+nativeTravelStatus);return;}
  if(Player.ContentId!=trip.Character){StopNativeWorldTravel("Shopping character changed");StopNativeAethernet("Shopping character changed");EndShoppingTravel("Shopping travel cancelled: character changed.");return;}
  if(nativeWorldTrip!=null){marketTravelStatus=nativeTravelStatus;shoppingTravelGate.Ready(now,false);return;}
  if(Player.CurrentWorld.RowId!=trip.World){EndShoppingTravel("World travel stopped before the shopping destination. "+nativeTravelStatus);return;}
  if(Conditions[ConditionFlag.InCombat]){StopNativeAethernet("Combat interrupted shopping");EndShoppingTravel("Shopping travel stopped during combat.");return;}
  if(nativeAethernetTrip!=null){marketTravelStatus=nativeAethernetStatus;return;}
  var ready=!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]&&!Conditions[ConditionFlag.Casting]&&!Conditions[ConditionFlag.OccupiedInEvent]&&!LifestreamBusy()&&now>=trip.NotBefore;
  if(!shoppingTravelGate.Ready(now,ready)){marketTravelStatus="Waiting for arrival to settle before continuing to "+ShoppingDestination.Cities[trip.City];return;}
  try{
   var city=ShoppingDestination.Aetheryte(trip.City);
   if(trip.Stage==0){
    if(ShoppingCityArrived(city)){shoppingTravel=trip with{Stage=2};RecordTravelPrompt("Shopping city confirmed",new{city});return;}
    marketTravelStatus="Teleporting to shopping city · "+ShoppingDestination.Cities[trip.City];
    if(!shoppingTravelGate.MayRequest(now))return;
    if(!RequestNativePublicTeleport(city)){marketTravelStatus="Waiting for the game to accept the shopping-city teleport; continuing automatically";return;}
    shoppingTravel=trip with{Stage=1,NotBefore=now.AddSeconds(3)};shoppingTravelGate.Reset();return;
   }
   if(trip.Stage==1){
    if(!ShoppingCityArrived(city)){marketTravelStatus="Waiting for confirmed arrival in "+ShoppingDestination.Cities[trip.City];return;}
    shoppingTravel=trip with{Stage=2};RecordTravelPrompt("Shopping city confirmed",new{city});return;
   }
   if(trip.Stage==2){
    if(trip.Shard.Length==0){EndShoppingTravel("Arrived at "+ShoppingDestination.Cities[trip.City]+" on "+WorldName(trip.World)+".");return;}
    if(!RequestUnlockedAethernet(ResolveAethernetDestination(trip.Shard,city),out var reason)){EndShoppingTravel("Arrived in the shopping city. "+reason);return;}
    shoppingTravel=trip with{Stage=3};marketTravelStatus="Continuing to "+trip.Shard;return;
   }
   EndShoppingTravel(nativeAethernetSucceeded?"Arrived at "+trip.Shard+" on "+WorldName(trip.World)+".":"Shopping shard travel stopped: "+nativeAethernetStatus);
  }catch(Exception ex){EndShoppingTravel("Shopping travel stopped: "+ex.Message);}
 }
}
