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
    private bool SharingWorldIntent=>sharedWorldIntent!=0&&DateTimeOffset.UtcNow-sharedWorldIntentAt<TimeSpan.FromMinutes(30);
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
        var matches=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().Where(x=>x.IsPublic&&string.Equals(x.Name.ToString(),name.Trim(),StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length==1?matches[0].RowId:0;
    }
    private void ShareFollowWorldIntent(uint world)
    {
        if(!SharingTravel||!Player.IsLoaded||Objects.LocalPlayer is not {} self||config.PairingKey.Length!=64||world==0||world==Player.CurrentWorld.RowId)return;
        var now=DateTimeOffset.UtcNow;
        if(sharedWorldIntent==world&&now-sharedWorldIntentAt<TimeSpan.FromSeconds(10))return;
        var signal=TravelSignal("world",0,"",0,self.Position);if(signal==null)return;
        sharedWorldIntent=world;sharedWorldIntentAt=now;outgoingTravel=null;transportCapture=null;boundaryDeparture=null;
        EnqueueOutgoingTravel(config.PairingKey,signal with {DestinationWorld=world},portalRelay.HasFollowers(config.PairingKey,signal.Name,signal.HomeWorld));
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
        if(world==0){FollowChatNotice("TRAVEL — Choose an exact World name: /equinox travel WorldName.");return;}
        try{
            if(LifestreamBusy()){FollowChatNotice("TRAVEL — Lifestream is already busy.");return;}
            if(Pi.GetIpcSubscriber<uint,bool>("Lifestream.ChangeWorldById").InvokeFunc(world))ShareFollowWorldIntent(world);
            else FollowChatNotice("TRAVEL — Lifestream declined that World destination.");
        }catch(Exception){FollowChatNotice("TRAVEL — Lifestream must be installed and enabled for World travel.");}
    }
}
