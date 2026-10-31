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
        Framework.Update+=UpdateKeyboundBuying;
        keyboundCommandRegistered=Commands.AddHandler("/eqkeybound",new CommandInfo((_,_)=>OpenKeybound()){HelpMessage="Open the optional Keybound Brawler typing tool."});
    }
    private void DisposeKeybound(){Framework.Update-=UpdateKeyboundBuying;StopKeyboundBuying("Stopped");StopKeybound();if(keyboundCommandRegistered)Commands.RemoveHandler("/eqkeybound");}
    private void OpenKeybound(){if(RequireOptionalTool("Keybound Brawler"))keyboundWindow.IsOpen=true;}
    private sealed class KeyboundToolWindow:Window
    {
        private readonly Plugin plugin;
        public KeyboundToolWindow(Plugin plugin):base("Keybound Brawler · Equinox"){
            this.plugin=plugin;Flags=ImGuiWindowFlags.AlwaysAutoResize;
            SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(360,0),MaximumSize=new Vector2(360,float.MaxValue)};
            RespectCloseHotkey=false;
        }
        public override void Draw()=>plugin.DrawKeyboundHelper();
    }
}

