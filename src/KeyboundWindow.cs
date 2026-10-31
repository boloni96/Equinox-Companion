using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private KeyboundToolWindow keyboundWindow=null!;
    private bool keyboundCommandRegistered;
    private void InitializeKeybound(){
        keyboundWindow=new KeyboundToolWindow(this);windows.AddWindow(keyboundWindow);
        keyboundCommandRegistered=Commands.AddHandler("/eqkeybound",new CommandInfo((_,_)=>OpenKeybound()){HelpMessage="Open the optional Keybound Brawler typing tool."});
    }
    private void DisposeKeybound(){StopKeybound();if(keyboundCommandRegistered)Commands.RemoveHandler("/eqkeybound");}
    private void OpenKeybound(){if(RequireOptionalTool("Keybound Brawler"))keyboundWindow.IsOpen=true;}
    private sealed class KeyboundToolWindow:Window
    {
        private readonly Plugin plugin;
        public KeyboundToolWindow(Plugin plugin):base("Keybound Brawler · Equinox"){
            this.plugin=plugin;Size=new Vector2(450,330);SizeCondition=ImGuiCond.FirstUseEver;
            SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(320,160),MaximumSize=new Vector2(float.MaxValue)};
            RespectCloseHotkey=false;
        }
        public override void Draw()=>plugin.DrawWindowWithLauncher(plugin.DrawKeyboundHelper);
    }
}
