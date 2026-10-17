using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private int upgradeScope;
 private readonly HashSet<string> upgradeSelected=new();
 private sealed record UpgradeCheckResult(UpgradeMatch[] Matches,string[] Failed,int Total,string Status);
 private Task<UpgradeCheckResult>? upgradeManualTask;
 private string[] upgradeRetryWorlds=[];
 private int upgradeRetryAttempt,upgradeCheckTotal;
 private DateTimeOffset upgradeRetryAt;
 private string upgradeCheckCharacter="";
 private HashSet<string> upgradeCheckSelected=new();
 private UpgradeMatch[] upgradeManualRows=[];
 private bool upgradeManualResults;
 private string upgradeManualKey="",upgradeManualStatus="";
 private ulong upgradeManualActor;
 private int upgradeManualScope;
 private async Task<UpgradeCheckResult> CheckUpgradeScope(string key,int scope,string characterId,HashSet<string> selected,string[]? retryWorlds=null)
 {
  string[] worlds;
  if(retryWorlds is not null)worlds=retryWorlds;
  else{
  var overview=await FetchUpgrades(key,"",scope!=1);
  if(scope!=1&&overview.Worlds is null)throw new InvalidOperationException("Deploy Journal V7.11.183 for all-character and current-character checks.");
  var watches=(overview.Watches??[]).Where(w=>w.Enabled&&w.State=="Watching"&&(scope!=1||selected.Contains(w.HouseId))&&(scope!=2||w.OwnerId==characterId)).ToArray();
  if(watches.Length==0)return new([],[],0,scope==1?"No selected active watches. Select a watch below.":"No matching watches or favourite plots. Add favourite plots or configure an upgrade watch.");
  worlds=(scope==0?overview.Worlds??[]:watches.Select(w=>w.World).ToArray()).Distinct().ToArray();
  }
  var matches=new List<UpgradeMatch>();var failed=new List<string>();
  foreach(var world in worlds){
   try{var reply=await FetchUpgrades(key,world,scope!=1);matches.AddRange((reply.Matches??[]).Where(m=>(scope!=1||selected.Contains(m.HouseId))&&(scope!=2||m.OwnerId==characterId)));}
   catch(Exception error) when(UpgradeRetryPolicy.IsTransient(error)){failed.Add(world);}
  }
  var rows=matches.Where(m=>m.PhaseUntil>DateTimeOffset.UtcNow.ToUnixTimeSeconds()&&m.Size is >=0 and <=2&&m.Ward is >=1 and <=30&&m.Plot is >=1 and <=60).OrderBy(m=>m.OwnerName).ThenBy(m=>m.Entries??int.MaxValue).ToArray();
  return new(rows,failed.ToArray(),worlds.Length,"");
 }
 private void UpdateManualUpgradeCheck()
 {
  if(upgradeManualTask is null&&upgradeManualResults&&(upgradeManualKey!=config.PairingKey||upgradeManualScope==2&&upgradeManualActor!=Player.ContentId)){ResetUpgradeScope("Character or pairing changed. Check again for the current selection.");}
  if(upgradeManualTask is null&&upgradeRetryAt!=default&&DateTimeOffset.UtcNow>=upgradeRetryAt&&Player.IsLoaded&&config.SyncEnabled&&config.PairingKey.Length==64){
   upgradeRetryAt=default;
   upgradeManualTask=CheckUpgradeScope(upgradeManualKey,upgradeManualScope,upgradeCheckCharacter,upgradeCheckSelected,upgradeRetryWorlds.Length>0?upgradeRetryWorlds:null);
  }
  if(upgradeManualTask?.IsCompleted!=true)return;
  var task=upgradeManualTask;upgradeManualTask=null;
  if(upgradeManualKey!=config.PairingKey||upgradeManualScope==2&&upgradeManualActor!=Player.ContentId){_=task.Exception;ResetUpgradeScope("Character or pairing changed. Check again for the current selection.");return;}
  upgradeManualResults=true;
  if(task.IsCompletedSuccessfully){
   var result=task.Result;if(upgradeRetryWorlds.Length==0)upgradeCheckTotal=result.Total;
   upgradeManualRows=upgradeManualRows.Concat(result.Matches).DistinctBy(m=>m.Key).Where(m=>m.PhaseUntil>DateTimeOffset.UtcNow.ToUnixTimeSeconds()).OrderBy(m=>m.OwnerName).ThenBy(m=>m.Entries??int.MaxValue).ToArray();
   upgradeRetryWorlds=result.Failed;
   upgradeManualStatus=result.Status.Length>0?result.Status:$"{upgradeManualRows.Length} matches · {upgradeCheckTotal-result.Failed.Length}/{upgradeCheckTotal} worlds checked";
   if(result.Failed.Length>0){upgradeManualStatus+=" · Unavailable: "+string.Join(", ",result.Failed);ScheduleUpgradeRetry();}else{upgradeRetryAt=default;upgradeRetryAttempt=0;}
  }
  else{var error=task.Exception?.GetBaseException();upgradeManualStatus=error?.Message??"Check interrupted. Please try again.";if(UpgradeRetryPolicy.IsTransient(error))ScheduleUpgradeRetry();}
 }
 private void ScheduleUpgradeRetry(){upgradeRetryAt=DateTimeOffset.UtcNow.AddSeconds(UpgradeRetryPolicy.DelaySeconds(upgradeRetryAttempt++));}
 private void ResetUpgradeScope(string status="Click Check watches to check this selection.")
 {upgradeRetryAt=default;upgradeRetryWorlds=[];upgradeRetryAttempt=0;upgradeManualResults=true;upgradeManualRows=[];upgradeManualStatus=status;upgradePage=0;upgradeManualKey=config.PairingKey;upgradeManualActor=Player.ContentId;upgradeManualScope=upgradeScope;}
 private void DrawUpgradeScope()
 {
  var current=CurrentSharedCharacter();
  ImGui.BeginDisabled(upgradeManualTask is not null);
  ImGui.SetNextItemWidth(280);
  if(ImGui.Combo("Check scope",ref upgradeScope,"All paired characters\0Selected upgrade watches\0This character's upgrades\0")){ResetUpgradeScope();}
  if(upgradeScope==1){
   if(ImGui.CollapsingHeader($"Choose watches · {upgradeSelected.Count} selected")){
    if(ImGui.SmallButton("Select all active")){foreach(var w in upgradeWatches.Where(w=>w.Enabled&&w.State=="Watching"))upgradeSelected.Add(w.HouseId);ResetUpgradeScope();}ImGui.SameLine();if(ImGui.SmallButton("Clear selection")){upgradeSelected.Clear();ResetUpgradeScope();}
    foreach(var w in upgradeWatches.Where(w=>w.Enabled&&w.State=="Watching")){var enabled=upgradeSelected.Contains(w.HouseId);if(ImGui.Checkbox($"{w.OwnerName} · {w.HouseName} · {w.World}##{w.HouseId}",ref enabled)){if(enabled)upgradeSelected.Add(w.HouseId);else upgradeSelected.Remove(w.HouseId);ResetUpgradeScope();}}
    if(!upgradeWatches.Any(w=>w.Enabled&&w.State=="Watching"))ImGui.TextDisabled("No active property watches. Configure them in the Journal.");
   }
  }
  else ImGui.TextWrapped(upgradeScope==0?"Checks configured upgrades and favourite plots across all paired home worlds.":current is null?"Waiting for this character's paired identity. Refresh the paired roster first.":$"{current.Name} · {current.World} · includes this character's watches and favourite matches.");
  ImGui.BeginDisabled(!config.SyncEnabled||config.PairingKey.Length!=64||upgradeScope==2&&current is null||upgradeScope==1&&upgradeSelected.Count==0);
  if(ImGui.SmallButton("Check watches")){
   upgradeRetryAt=default;upgradeRetryWorlds=[];upgradeRetryAttempt=0;upgradeCheckTotal=0;upgradeCheckCharacter=current?.Id??"";upgradeCheckSelected=new HashSet<string>(upgradeSelected);
   upgradeManualKey=config.PairingKey;upgradeManualActor=Player.ContentId;upgradeManualScope=upgradeScope;upgradeManualResults=true;upgradeManualRows=[];upgradePage=0;upgradeManualStatus="Checking selected worlds…";
   upgradeManualTask=CheckUpgradeScope(upgradeManualKey,upgradeScope,upgradeCheckCharacter,upgradeCheckSelected);
  }
  ImGui.EndDisabled();ImGui.EndDisabled();
  ImGui.TextWrapped(upgradeManualTask is not null?(upgradeRetryAttempt>0?"Retrying unavailable worlds…":"Checking…"):upgradeManualStatus.Length>0?upgradeManualStatus:upgradeStatus);
  if(upgradeRetryAt!=default)ImGui.TextWrapped($"Automatic retry in {Math.Max(0,(int)Math.Ceiling((upgradeRetryAt-DateTimeOffset.UtcNow).TotalSeconds))}s · successful matches kept"+(Player.IsLoaded&&config.SyncEnabled?"":" · paused until logged in with sync enabled"));
 }
}
