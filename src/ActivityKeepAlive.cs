using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset activityCheckAt,activityPulseAt,activityLastPulse;
    private long activityPulseCount;
    private string activityStatus="Off";
    [DllImport("user32.dll",EntryPoint="GetAsyncKeyState")] private static extern short ActivityKeyState(int key);
    private unsafe void UpdateActivityKeepAlive(IFramework framework)
    {
        var now=DateTimeOffset.UtcNow;
        var eventRun=config.EnableKeyboundBrawler&&keyboundEnabled&&keyboundAntiAfk&&KeyboundPolicy.LoopTerritory(Client.TerritoryType);
        var followRun=config.EnableFollowThem&&config.FollowAntiAfk&&followSession.Armed;
        if(!eventRun&&!followRun||!Client.IsLoggedIn){activityStatus="Off";activityCheckAt=activityPulseAt=default;return;}
        if(now<activityCheckAt)return;activityCheckAt=now.AddSeconds(1);
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){activityStatus="Waiting for loading";return;}
        try{
            var timers=InputTimerModule.Instance();
            if(timers!=null){
                timers->AfkTimer=timers->InputTimer=timers->ContentInputTimer=0;
                foreach(ref var timer in timers->ControllerInputTimers)timer.Timer=0;
                foreach(ref var timer in timers->MouseInputTimers)timer.Timer=0;
            }
            if(now<activityPulseAt)return;
            // Never interfere with a physical modifier the player is holding.
            if((ActivityKeyState(0x10)&0x8000)!=0||(ActivityKeyState(0x11)&0x8000)!=0||(ActivityKeyState(0x12)&0x8000)!=0){activityStatus="Waiting for modifier release";return;}
            if(eventRun&&now<keyboundHold){activityStatus="Waiting for no-typing cast";return;}
            var game=FFXIVClientStructs.FFXIV.Client.System.Framework.GameWindow.Instance();
            if(game==null||game->WindowHandle==0){activityStatus="Waiting for game input";return;}
            // Deliver a modifier-only pulse straight to THIS client's keyboard
            // message processor. No global SendInput, focus change or other window.
            KeyboardDevice.ProcessKeyboardInputMessage(game->WindowHandle,0x100,0x10,0x002a0001);
            KeyboardDevice.ProcessKeyboardInputMessage(game->WindowHandle,0x101,0x10,unchecked((nint)(int)0xc02a0001));
            activityLastPulse=now;activityPulseCount++;activityPulseAt=now.AddSeconds(20);
            activityStatus="Active";
        }catch(Exception ex){
            activityStatus="Input pulse unavailable";activityPulseAt=now.AddSeconds(20);
            Log.Warning(ex,"Anti-AFK input pulse unavailable");
        }
    }
}
