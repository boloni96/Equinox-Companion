using System.Runtime.InteropServices;
using System.Text;
namespace EquinoxCompanion;
// Window-addressed messages only: never SendInput, global key state or foreground switching.
internal static class FollowWindowInput
{
    private delegate bool WindowVisitor(nint hwnd,nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor,nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(nint hwnd,StringBuilder name,int count);
    [DllImport("user32.dll")] private static extern bool PostMessageW(nint hwnd,uint message,nuint key,nint data);
    [DllImport("user32.dll")] private static extern uint MapVirtualKeyW(uint key,uint mode);
    private static bool OwnWindow(nint hwnd){GetWindowThreadProcessId(hwnd,out var pid);return pid==(uint)Environment.ProcessId;}
    public static bool Press(int key,out nint window)
    {
        window=0;if(!FollowStopKeyPolicy.Allowed(key,0))return false;
        return PressOwnWindow(key,out window);
    }
    internal static bool PressCutsceneEscape(out nint window)=>PressOwnWindow(0x1B,out window);
    private static bool PressOwnWindow(int key,out nint window)
    {
        window=0;if(!OperatingSystem.IsWindows())return false;
        nint found=0;
        EnumWindows((hwnd,_)=>{if(!OwnWindow(hwnd))return true;var name=new StringBuilder(128);GetClassName(hwnd,name,name.Capacity);if(name.ToString()!="FFXIVGAME")return true;found=hwnd;return false;},0);
        if(found==0||!OwnWindow(found))return false;
        if(!PostMessageW(found,0x100,(nuint)key,KeyData(key,false)))return false;
        window=found;return true;
    }
    public static void Release(nint window,int key)
    {
        if(OperatingSystem.IsWindows()&&window!=0&&OwnWindow(window))PostMessageW(window,0x101,(nuint)key,KeyData(key,true));
    }
    private static nint KeyData(int key,bool up)
    {
        uint bits=1|(MapVirtualKeyW((uint)key,0)<<16);
        if(key is >=0x25 and <=0x28)bits|=1u<<24;
        if(up)bits|=3u<<30;
        return (nint)(nuint)bits;
    }
}
