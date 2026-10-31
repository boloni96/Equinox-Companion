using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Input;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static unsafe nint KeyboundGameWindow()
    {
        var game=GameWindow.Instance();
        return game==null?0:game->WindowHandle;
    }
    // Framework-thread only. Dispatch directly into this client's input processor:
    // no OS keyboard injection, focus stealing, or queued window messages that
    // could arrive after a stalled frame and duplicate a retry.
    private static bool KeyboundMessage(nint window,uint message,nint value,nint detail)
    {
        if(window==0||window!=KeyboundGameWindow())return false;
        KeyboardDevice.ProcessKeyboardInputMessage(window,message,value,detail);
        return true;
    }
    private static bool KeyboundSend(nint window,char character)=>KeyboundMessage(window,0x102,(nint)character,1);
    private static bool KeyboundEnter(nint window)=>KeyboundMessage(window,0x100,13,0x001c0001)&&KeyboundMessage(window,0x101,13,unchecked((nint)(int)0xc01c0001));
    private static bool KeyboundClear(nint window)=>KeyboundMessage(window,0x100,0x1b,0x00010001)&&KeyboundMessage(window,0x101,0x1b,unchecked((nint)(int)0xc0010001));
    private static bool KeyboundUp(nint window)=>KeyboundMessage(window,0x100,0x26,0x01480001)&&KeyboundMessage(window,0x101,0x26,unchecked((nint)(int)0xc1480001));
}
