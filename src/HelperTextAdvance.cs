namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private const string HelperTextAdvanceOwner="EquinoxCompanion.Cutscene";
    private bool helperTextAdvanceOwned;
    private string helperTextAdvanceAction="",helperTextAdvanceScene="";
    private DateTimeOffset helperTextAdvanceUntil,helperTextAdvanceReleaseRetry;
    private bool StartHelperTextAdvance(HelperAction action,DateTimeOffset now)
    {
        if(helperTextAdvanceOwned){BlockHelper("Previous TextAdvance control has not been released.");return false;}
        try{
            if(Pi.GetIpcSubscriber<bool>("TextAdvance.IsInExternalControl").InvokeFunc()){
                BlockHelper("TextAdvance is controlled by another plugin. Stop its automation before using Companion cutscene mirroring.");return false;
            }
            if(Pi.GetIpcSubscriber<bool>("TextAdvance.IsPaused").InvokeFunc()){
                BlockHelper("TextAdvance is paused; cutscene skip was not started.");return false;
            }
            if(!Pi.GetIpcSubscriber<string,HelperTextAdvanceOptions,bool>("TextAdvance.EnableExternalControl")
                .InvokeFunc(HelperTextAdvanceOwner,new HelperTextAdvanceOptions(false))){
                BlockHelper("TextAdvance declined cutscene control.");return false;
            }
            helperTextAdvanceOwned=true;helperTextAdvanceAction=action.Id;helperTextAdvanceScene=action.Scene;
            helperTextAdvanceUntil=now.AddSeconds(15);
            helperQuestStatus="TextAdvance is opening the matching cutscene skip prompt.";
            RecordFollowTravel("Helper TextAdvance skip started",new {action.Scene,action.QuestId});
            return true;
        }catch(Exception ex){BlockHelper("Install or enable TextAdvance for cutscene mirroring ("+ex.GetType().Name+").");return false;}
    }
    private void ReleaseHelperTextAdvance()
    {
        if(!helperTextAdvanceOwned)return;
        try{
            // A false result means a different requester owns it now; never disable that requester.
            var released=Pi.GetIpcSubscriber<string,bool>("TextAdvance.DisableExternalControl").InvokeFunc(HelperTextAdvanceOwner);
            helperTextAdvanceOwned=false;
            RecordFollowTravel("Helper TextAdvance control released",new {released});
        }catch(Exception){
            // Retry on subsequent framework updates if IPC is temporarily unavailable.
            helperTextAdvanceReleaseRetry=DateTimeOffset.UtcNow.AddSeconds(1);
        }
    }
    private void ObserveHelperTextAdvance(DateTimeOffset now)
    {
        if(!helperTextAdvanceOwned)return;
        var active=helperIncoming.TryPeek(out var a)&&a.Kind=="skip"&&a.Id==helperTextAdvanceAction;
        var permitted=config.EnableFollowThem&&helperPermission.Active&&helperPermission.Quest&&helperPermission.Skip&&!HelperPaused&&!helperPermission.QuestPaused&&helperBlocked.Length==0&&Player.IsLoaded;
        if(!HelperTextAdvancePolicy.Keep(permitted,active,HelperInCutscene(),helperTextAdvanceScene,HelperScene(),now,helperTextAdvanceUntil)&&now>=helperTextAdvanceReleaseRetry)
            ReleaseHelperTextAdvance();
    }
}
