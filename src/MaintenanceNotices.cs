using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private readonly Dalamud.Plugin.Services.IClientState.LogoutDelegate maintenanceLogout;
 private readonly MaintenanceService maintenanceService=new();
 private readonly MaintenanceSession maintenanceSession=new();
 private readonly CancellationTokenSource maintenanceStop=new();
 private readonly Dictionary<string,(uint Id,DalamudLinkPayload Link)> maintenanceLinks=[];
 private Task<MaintenanceNotice[]>? maintenanceTask;
 private ulong maintenanceTaskCharacter;
 private DateTimeOffset maintenanceNextCheck;
 private bool maintenanceFailureShown;
 private void ResetMaintenanceLogin(){lotteryLogin.Reset();lotteryPendingCharacter=0;lotteryPlacard=null;maintenanceSession.Reset();maintenanceTask=null;maintenanceFailureShown=false;maintenanceNextCheck=default;}
 private void UpdateMaintenanceNotices(DateTimeOffset now)
 {
  if(!Player.IsLoaded||Player.ContentId==0)return;
  maintenanceSession.Observe(Player.ContentId,now);
  if(!config.NotifyMaintenance||!maintenanceSession.Ready(now))return;
  if(maintenanceTask?.IsCompleted==true)
  {
   var task=maintenanceTask;maintenanceTask=null;
   if(maintenanceTaskCharacter!=Player.ContentId){_ = task.Exception;return;}
   if(task.IsCompletedSuccessfully)
   {
    maintenanceNextCheck=now.AddMinutes(15);maintenanceFailureShown=false;
    var world=Player.HomeWorld.Value.Name.ToString();var dc=Player.HomeWorld.Value.DataCenter.Value.Name.ToString();
    foreach(var notice in task.Result.Where(n=>n.Relevant(world,dc)&&n.Scheduled(now)).Take(3))
     if(maintenanceSession.New(notice))PrintMaintenance(notice.Message(now,TimeZoneInfo.Local),notice.Url);
   }
   else
   {
    _ = task.Exception;maintenanceNextCheck=now.AddMinutes(2);
    if(!maintenanceFailureShown){maintenanceFailureShown=true;Log.Warning("Maintenance check unavailable; will retry quietly.");}
   }
  }
  if(maintenanceTask is null&&now>=maintenanceNextCheck)
  {maintenanceTaskCharacter=Player.ContentId;maintenanceTask=Task.Run(()=>maintenanceService.Fetch(maintenanceStop.Token));}
 }
 private void PrintMaintenance(string message,string url)
 {
  if(!maintenanceLinks.TryGetValue(url,out var entry))
  {
   var id=(uint)(10600+maintenanceLinks.Count);
   var link=Chat.AddChatLinkHandler(id,(_,_)=>{try{Dalamud.Utility.Util.OpenLink(url);}catch(Exception ex){Log.Warning(ex,"Could not open maintenance notice");}});
   maintenanceLinks[url]=entry=(id,link);
  }
  Chat.Print(new SeStringBuilder().AddText("[Equinox] "+message+" ").Add(entry.Link).AddUiForeground(45).AddText("[Official Lodestone notice]").AddUiForegroundOff().Add(RawPayload.LinkTerminator).Build());
 }
 private void DisposeMaintenanceNotices(){maintenanceStop.Cancel();maintenanceStop.Dispose();maintenanceService.Dispose();foreach(var link in maintenanceLinks.Values)Chat.RemoveChatLinkHandler(link.Id);}
}
