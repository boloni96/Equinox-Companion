using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool followStopPending,followStopIssued,nativeFollowRequested,followStopUnconfirmed;
    private DateTimeOffset followStopNext,followStopPulseUntil;
    private ulong followStopCharacter;
    private int followStopKey;
    private nint followStopWindow;
    private readonly FollowStationaryGate followStopStationary=new();
    private static readonly InputId[] FollowMovementBindings=[InputId.MOVE_FORE,InputId.MOVE_BACK,InputId.MOVE_LEFT,InputId.MOVE_RIGHT,InputId.MOVE_STRIFE_L,InputId.MOVE_STRIFE_R];

    // Read the stored, UI-filtered input data. Never call InputManager.GetInputStatus:
    // the .64 stop hook received no reads and that path produced unwanted actions.
    private unsafe bool FollowMovementKeysHeld()
    {
        var input=UIInputData.Instance();if(input==null)return false;
        if(Math.Abs(input->GamepadInputs.LeftStickX)>20||Math.Abs(input->GamepadInputs.LeftStickY)>20)return true;
        foreach(var id in FollowMovementBindings){
            if(input->Keybinds==null||(int)id>=input->NumKeybinds)continue;
            foreach(var binding in input->Keybinds[(int)id].KeySettings){
                var key=(int)binding.Key;
                if(key<=0||key>=input->KeyboardInputs.KeyState.Length||key==followStopKey&&DateTimeOffset.UtcNow<followStopPulseUntil.AddMilliseconds(100))continue;
                if(binding.KeyModifier==input->CurrentKeyModifier&&(input->GetKeyState(key)&KeyStateFlags.Down)!=0)return true;
            }
        }
        return false;
    }
    private unsafe int FollowStopBoundKey()
    {
        var input=UIInputData.Instance();if(input==null||input->Keybinds==null||input->NumKeybinds<=(int)InputId.MOVE_BACK||input->CurrentKeyModifier!=0)return 0;
        foreach(var binding in input->Keybinds[(int)InputId.MOVE_BACK].KeySettings){
            var key=(int)binding.Key;
            if(FollowStopKeyPolicy.Allowed(key,(int)binding.KeyModifier)&&key<input->KeyboardInputs.KeyState.Length&&(input->GetKeyState(key)&KeyStateFlags.Down)==0)return key;
        }
        return 0;
    }
    private unsafe bool FollowStopTextEntryActive()
    {
        var stage=AtkStage.Instance();
        return stage==null||stage->AtkInputManager==null||stage->AtkInputManager->IsTextInputActive||Dalamud.Bindings.ImGui.ImGui.GetIO().WantTextInput;
    }
    private void ReleaseFollowStopKey()
    {
        if(followStopWindow==0)return;
        FollowWindowInput.Release(followStopWindow,followStopKey);followStopWindow=0;
    }
    private void RequestFollowMovementStop()
    {
        if(followStopPending)return;
        followStopIssued=false;followStopUnconfirmed=false;followStopPending=true;followStopCharacter=Player.IsLoaded?Player.ContentId:followLogin;followStopNext=DateTimeOffset.UtcNow.AddSeconds(5);followStopStationary.Reset();
        UpdateFollowMovementStop(DateTimeOffset.UtcNow);
    }
    private unsafe void UpdateFollowMovementStop(DateTimeOffset now)
    {
        if(followStopWindow!=0&&(now>=followStopPulseUntil||!Player.IsLoaded||Player.ContentId!=followStopCharacter||FollowStopTextEntryActive()))ReleaseFollowStopKey();
        if(followStopPending&&Player.IsLoaded&&Player.ContentId!=followStopCharacter){ReleaseFollowStopKey();followStopPending=false;nativeFollowRequested=false;followStopUnconfirmed=false;return;}
        // A real movement input cancels native follow even if its chat notice was missed.
        // Ignore our synthetic key pulse and require release plus stationary confirmation.
        if((followStopPending||followStopUnconfirmed)&&CanIssueFollowMovement()&&!FollowStopTextEntryActive()&&followStopWindow==0&&now>=followStopPulseUntil.AddMilliseconds(100)&&FollowMovementKeysHeld()){
            nativeFollowRequested=false;followStopUnconfirmed=false;followStopPending=true;followStopStationary.Reset();
        }
        if(!followStopPending)return;
        if(nativeFollowRequested&&FollowStopTextEntryActive()){
            ReleaseFollowStopKey();followStopIssued=false;
            if(now>=followStopNext){followStopNext=now.AddSeconds(30);TravelDiagnostic("Stop queued; close text input to finish stopping.");}
            return;
        }
        if(!nativeFollowRequested){
            ReleaseFollowStopKey();
            if(followStopStationary.Observe(now,Objects.LocalPlayer?.Position??default,Player.IsLoaded&&!FollowMovementKeysHeld())){
                followStopPending=false;followStopUnconfirmed=false;if(!followSession.Armed){followStatus="STOPPED — Game follow cancelled; movement stopped.";RefreshFollowBar();}RecordFollowTravel("Movement stop confirmed",new {stationary=true,nativeFollowCancelled=true});
            }
            return;
        }
        if(!followStopIssued){
            if(!CanIssueFollowMovement()||FollowStopTextEntryActive()||FollowMovementKeysHeld()){
                // Keep the stop armed while chat or a transition prevents safe input.
                // Closing chat must execute it without requiring a manual movement tap.
                if(now>=followStopNext){followStopNext=now.AddSeconds(30);TravelDiagnostic("Stop queued; waiting for text input or loading to finish.");}
                return;
            }
            followStopKey=FollowStopBoundKey();
            if(followStopKey==0||!FollowWindowInput.Press(followStopKey,out followStopWindow)){
                followStopPending=false;followStopUnconfirmed=true;
                RecordFollowTravel("Movement stop unavailable",new {reason=followStopKey==0?"No unmodified backward movement binding available":"Current game window unavailable"});
                TravelDiagnostic("Automatic stop unavailable; tap a movement key to cancel game follow.");return;
            }
            followStopPulseUntil=now.AddMilliseconds(100);followStopNext=now.AddSeconds(2);followStopIssued=true;
            RecordFollowTravel("Movement stop requested",new {method="current-process window movement key",key=followStopKey,durationMs=100,process=Environment.ProcessId});return;
        }
        if(now>=followStopNext){
            ReleaseFollowStopKey();followStopPending=false;followStopUnconfirmed=true;
            RecordFollowTravel("Movement stop unconfirmed",new {nativeFollowCancelled=false});
            TravelDiagnostic("Game follow cancellation was not confirmed. Tap a movement key; travel is held.");
        }
    }
}
