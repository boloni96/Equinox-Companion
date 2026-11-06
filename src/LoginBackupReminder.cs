using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private int backupReminderCursor;
    private DateTimeOffset backupReminderNext;
    private unsafe bool UpdateLoginBackupReminder(DateTimeOffset now)
    {
        if(Player.IsLoaded||Client.IsLoggedIn||nativeWorldTrip==null&&estateHomeReturn==null&&assignedLogin==null&&!titleLoginProbe.Active&&!loginMenuReturn.Active)return false;
        if(now<backupReminderNext)return false;
        var started=Stopwatch.GetTimestamp();
        try{
            var stage=AtkStage.Instance();if(stage==null||stage->RaptureAtkUnitManager==null)return false;
            var manager=(AtkUnitManager*)stage->RaptureAtkUnitManager;
            var list=&manager->AllLoadedUnitsList;
            if(list->Count>256)return false;
            // Discover by exact reminder text rather than guessing an addon name
            // or clicking generic OK dialogs (which can be login queues).
            for(var n=0;n<4&&backupReminderCursor<list->Count;n++){
                var addon=list->Entries[backupReminderCursor++].Value;
                if(addon==null||!addon->IsVisible||!addon->IsReady)continue;
                var layout=&addon->UldManager;
                if(layout->NodeList==null||layout->NodeListCount>256)continue;
                var texts=TravelNodeText(layout);var buttons=new List<nint>();
                for(var i=0;i<layout->NodeListCount;i++){
                    var node=layout->NodeList[i];if(node==null||!node->IsVisible())continue;
                    var component=node->GetAsAtkComponentNode();if(component==null||component->Component==null)continue;
                    var child=&component->Component->UldManager;
                    if(child->NodeListCount>64)continue;
                    var childTexts=TravelNodeText(child);texts.AddRange(childTexts);
                    // Checkboxes and window-close controls are not acknowledgement buttons.
                    if(component->Component->GetComponentType()==ComponentType.Button&&childTexts.Any(x=>x.Trim()=="OK"))buttons.Add((nint)component->Component);
                }
                if(!LoginBackupReminderPolicy.Matches(texts)||buttons.Count!=1)continue;
                var button=(AtkComponentButton*)buttons[0];
                RecordTravelPrompt("Backup reminder detected",new{addon=addon->NameString,texts});
                if(NativeEventButton(addon,button))RecordTravelPrompt("Backup reminder dismissed",new{addon=addon->NameString,action="OK",settingsChanged=false});
                backupReminderNext=now.AddSeconds(2);return true;
            }
            if(backupReminderCursor>=list->Count){backupReminderCursor=0;backupReminderNext=now.AddMilliseconds(250);}
        }catch(Exception ex){backupReminderCursor=0;backupReminderNext=now.AddSeconds(5);Log.Debug(ex,"Backup reminder observation failed");}
        finally{RecordPerformanceElapsed("Login: backup reminder slice",started);}
        return false;
    }
}
