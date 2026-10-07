using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe delegate void FollowWorldCommandDelegate(UIModule* module,Utf8String* text,nint extra,bool history);
    private Hook<FollowWorldCommandDelegate>? followWorldCommandHook;
    private bool worldCommandHookFailed;
    private uint sharedWorldIntent;
    private FollowPortalSignal? announcedWorldTrip;
    private string announcedWorldKey="";
    private bool SharingWorldIntent {
        get {
            if(sharedWorldIntent==0)return false;
            if(FollowWorldIntentPolicy.Clear(sharedWorldIntent,DateTimeOffset.UtcNow-sharedWorldIntentAt,Player.IsLoaded,sharedWorldCharacter,Player.ContentId,Player.CurrentWorld.RowId)){
                RecordFollowTravel("World intent cleared",new {destination=sharedWorldIntent,current=Player.CurrentWorld.RowId,reason="arrival, character change or expiry"});sharedWorldIntent=0;return false;
            }
            return true;
        }
    }
    private ulong sharedWorldCharacter;
    private DateTimeOffset sharedWorldIntentAt;
    private unsafe void UpdateFollowWorldCommandHook()
    {
        if(SharingTravel&&!worldCommandHookFailed&&followWorldCommandHook==null){
            try{followWorldCommandHook=Interop.HookFromAddress<FollowWorldCommandDelegate>(UIModule.MemberFunctionPointers.ProcessChatBoxEntry,ObserveFollowWorldCommand);}
            catch(Exception e){worldCommandHookFailed=true;errorJournal.Record("follow-world-command",e.Message,exceptionType:e.GetType().Name);}
        }
        if(followWorldCommandHook!=null){if(SharingTravel&&!followWorldCommandHook.IsEnabled)followWorldCommandHook.Enable();else if(!SharingTravel&&followWorldCommandHook.IsEnabled)followWorldCommandHook.Disable();}
    }
    private uint ResolveFollowWorld(string name)
    {
        var matches=FollowWorldNames.Matches(DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().Where(x=>x.IsPublic).Select(x=>(x.RowId,x.Name.ToString())),name);
        return matches.Length==1?matches[0].Id:0;
    }
    private void ShareFollowWorldIntent(uint world)
    {
        if(!SharingTravel||!Player.IsLoaded||Objects.LocalPlayer is not {} self||config.PairingKey.Length!=64||world==0||world==Player.CurrentWorld.RowId)return;
        var now=DateTimeOffset.UtcNow;
        if(sharedWorldIntent==world&&now-sharedWorldIntentAt<TimeSpan.FromSeconds(10))return;
        var signal=TravelSignal("world",0,"",0,self.Position);if(signal==null)return;
        sharedWorldIntent=world;sharedWorldIntentAt=now;sharedWorldCharacter=Player.ContentId;outgoingTravel=null;transportCapture=null;boundaryDeparture=null;
        announcedWorldTrip=signal with {DestinationWorld=world};announcedWorldKey=config.PairingKey;
        EnqueueOutgoingTravel(config.PairingKey,announcedWorldTrip,portalRelay.HasFollowers(config.PairingKey,signal.Name,signal.HomeWorld));
        RecordFollowTravel("World travel intent",new {destinationWorld=world});
    }
    private unsafe void ObserveFollowWorldCommand(UIModule* module,Utf8String* text,nint extra,bool history)
    {
        uint world=0;
        try{
            if(SharingTravel&&text!=null&&text->BufUsed<128){
                var command=text->ToString();var split=command.IndexOf(' ');
                if(split>0&&command[..split] is "/li" or "/lifestream")world=ResolveFollowWorld(command[(split+1)..]);
            }
        }catch(Exception){ /* Never inspect or record unrelated chat. */ }
        followWorldCommandHook!.Original(module,text,extra,history);
        if(world!=0){try{if(LifestreamBusy())ShareFollowWorldIntent(world);}catch(Exception){}}
    }
    private void StartSharedWorldTravel(string worldName)
    {
        var world=ResolveFollowWorld(worldName);
        if(world==0){
            var matches=FollowWorldNames.Matches(DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().Where(x=>x.IsPublic).Select(x=>(x.RowId,x.Name.ToString())),worldName);
            FollowChatNotice(matches.Length>1?"TRAVEL — Ambiguous World: "+string.Join(", ",matches.Select(x=>x.Name)):"TRAVEL — Use /eqtravel WorldName or a unique prefix, for example /eqtravel sir.");return;
        }
        try{
            if(LifestreamBusy()){FollowChatNotice("TRAVEL — Lifestream is already busy.");return;}
            if(Pi.GetIpcSubscriber<uint,bool>("Lifestream.ChangeWorldById").InvokeFunc(world))ShareFollowWorldIntent(world);
            else FollowChatNotice("TRAVEL — Lifestream declined that World destination.");
        }catch(Exception){FollowChatNotice("TRAVEL — Lifestream must be installed and enabled for World travel.");}
    }
}
