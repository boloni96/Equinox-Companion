namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private const string GardenPauseOwner="EquinoxCompanion.Gardening";
    private HashSet<string>? gardenDialoguePauseRequests;
    private void SetGardenDialoguePause(bool paused)
    {
        if(!paused){
            gardenDialoguePauseRequests?.Remove(GardenPauseOwner);
            gardenDialoguePauseRequests=null;
            return;
        }
        if(!Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName=="YesAlready"))return;
        // YesAlready's public shared stop-request set preserves user settings
        // and independent pause owners. Reacquire if YesAlready reloads it.
        var requests=Pi.GetOrCreateData<HashSet<string>>("YesAlready.StopRequests",()=>new());
        if(!ReferenceEquals(requests,gardenDialoguePauseRequests))gardenDialoguePauseRequests?.Remove(GardenPauseOwner);
        gardenDialoguePauseRequests=requests;
        if(requests.Add(GardenPauseOwner))RecordTravelPrompt("Gardening dialogue pause",new{plugin="YesAlready",paused=true});
    }
}
