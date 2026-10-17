using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private unsafe void GoToAdventureFashion()
 {
  void Notice(string text){characterTravelStatus=text;Chat.Print("[Equinox] "+text);}
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.BoundByDuty]){Notice("Leave combat, loading or your duty before travelling to the Gold Saucer.");return;}
  if(followSession.Armed){Notice("Stop FollowThem before travelling to the Gold Saucer.");return;}
  if(goSeeHomeTrip is not null||goSeeFriendTrip is not null||shoppingTravel is not null||DateTimeOffset.UtcNow<characterSwitchUntil||DateTimeOffset.UtcNow<goSeeNativeUntil){Notice("Travel is already in progress. Wait for it to finish first.");return;}
  try{if(LifestreamBusy()){Notice("Lifestream travel is already in progress. Wait for it to finish first.");return;}}catch{/* Native teleport does not require Lifestream. */}
  const uint goldSaucerAetheryte=62;
  var destination=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().GetRowOrDefault(goldSaucerAetheryte);
  if(destination is {} row&&Client.TerritoryType==row.Territory.RowId){Notice("You are already at the Gold Saucer. Head to Masked Rose for the Fashion Report.");return;}
  var telepo=FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance();
  if(telepo==null){Notice("Teleport is not available yet. Try again once your character has loaded.");return;}
  if(telepo->ActiveTeleportRequest){Notice("A teleport is already in progress.");return;}
  telepo->UpdateAetheryteList();
  foreach(var entry in telepo->TeleportList){
   if(entry.AetheryteId!=goldSaucerAetheryte)continue;
   usingSharedTravel=true;
   try{
    var accepted=telepo->Teleport(entry.AetheryteId,entry.SubIndex);
    if(accepted)goSeeNativeUntil=DateTimeOffset.UtcNow.AddSeconds(10);
    Notice(accepted?"Gold Saucer teleport requested. Head to Masked Rose for the Fashion Report.":"The game refused the Gold Saucer teleport. Check your current state and try again.");
   }finally{usingSharedTravel=false;}
   return;
  }
  Notice("The Gold Saucer is not in this character’s available Teleport destinations. Unlock it first.");
 }
}
