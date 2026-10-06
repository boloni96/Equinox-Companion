using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe delegate bool FollowInputDelegate(InputManager* manager,InputCode code);
    private Hook<FollowInputDelegate>? followInputHook;
    private bool followInputUnavailable,followStopPending,followStopIssued,nativeFollowRequested,followStopUnconfirmed;
    private DateTimeOffset followStopNext,followStopPulseUntil;
    private ulong followStopCharacter;
    private int followStopPulseReads;
    private readonly FollowStationaryGate followStopStationary=new();
    // A short client-local backward-input pulse uses the same cancellation path
    // as manual movement. No OS key events are sent to either game window.
    private unsafe bool FollowInputDetour(InputManager* manager,InputCode code)
    {
        if(code==InputCode.MOVE_BACK&&DateTimeOffset.UtcNow<followStopPulseUntil&&Player.IsLoaded&&Player.ContentId==followStopCharacter&&CanIssueFollowMovement()){
            followStopPulseReads++;return true;
        }
        return followInputHook!.Original(manager,code);
    }
    private unsafe bool ReadFollowInput(InputManager* input,InputCode code)=>input!=null&&(followInputHook?.IsEnabled==true?followInputHook.Original(input,code):input->GetInputStatus(code));
    private unsafe bool FollowMovementKeysHeld()
    {
        var input=InputManager.Instance();
        return ReadFollowInput(input,InputCode.MOVE_FORE)||ReadFollowInput(input,InputCode.MOVE_BACK)||ReadFollowInput(input,InputCode.MOVE_LEFT)||ReadFollowInput(input,InputCode.MOVE_RIGHT)||ReadFollowInput(input,InputCode.MOVE_STRIFE_L)||ReadFollowInput(input,InputCode.MOVE_STRIFE_R);
    }
    private void RequestFollowMovementStop()
    {
        if(followStopPending)return;
        followStopIssued=false;followStopUnconfirmed=false;followStopPending=true;followStopCharacter=Player.IsLoaded?Player.ContentId:followLogin;followStopNext=default;followStopStationary.Reset();
        UpdateFollowMovementStop(DateTimeOffset.UtcNow);
    }
    private unsafe void UpdateFollowMovementStop(DateTimeOffset now)
    {
        if(followStopPending&&Player.IsLoaded&&Player.ContentId!=followStopCharacter){followStopPending=false;followStopPulseUntil=default;nativeFollowRequested=false;followStopUnconfirmed=false;return;}
        if(now>=followStopPulseUntil&&followInputHook is {IsEnabled:true})followInputHook.Disable();
        if(!followStopPending)return;
        if(!followStopIssued){
            if(!CanIssueFollowMovement())return;
            if(!followInputUnavailable&&followInputHook==null){
                try{followInputHook=Interop.HookFromAddress<FollowInputDelegate>(InputManager.MemberFunctionPointers.GetInputStatus,FollowInputDetour);followInputHook.Enable();}
                catch(Exception e){followInputUnavailable=true;errorJournal.Record("follow-stop", "Movement interrupt unavailable",exceptionType:e.GetType().Name);}
            }
            if(followInputUnavailable){followStopPending=false;TravelDiagnostic("Movement interruption unavailable; tap a movement key to stop game follow.");return;}
            if(followInputHook is {IsEnabled:false})followInputHook.Enable();
            followStopPulseReads=0;followStopPulseUntil=now.AddMilliseconds(80);followStopNext=now.AddSeconds(2);followStopIssued=true;
            RecordFollowTravel("Movement stop requested",new {method="client-local backward input",durationMs=80});return;
        }
        if(now<followStopPulseUntil)return;
        if(followStopStationary.Observe(now,Objects.LocalPlayer?.Position??default,Player.IsLoaded&&!nativeFollowRequested&&!FollowMovementKeysHeld())){
            followStopPending=false;RecordFollowTravel("Movement stop observation",new {inputReads=followStopPulseReads,stationary=true,nativeFollowStateVerified=false});return;
        }
        if(now>=followStopNext){
            followStopPending=false;followStopUnconfirmed=nativeFollowRequested;RecordFollowTravel("Movement stop observation",new {inputReads=followStopPulseReads,stationary=false,nativeFollowStateVerified=false});
            TravelDiagnostic("Movement has not stopped after the interrupt. Tap a movement key; automatic stop is not confirmed.");
        }
    }
}
