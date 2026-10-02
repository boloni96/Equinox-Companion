namespace EquinoxCompanion;
// Release-to-save capture, kept independent of ImGui so focus/cancel/chord edges are testable.
public sealed class ShortcutCapture
{
    public Shortcut? Pending { get; private set; }
    public string Error { get; private set; } = "";
    private bool rejected;
    public Shortcut? Step(string[] keys, bool ctrl, bool alt, bool shift, bool super)
    {
        var released=keys.Length==0&&!ctrl&&!alt&&!shift&&!super;
        if(rejected){if(released){rejected=false;Pending=null;}return null;}
        if(super||keys.Length>1||keys.Length==1&&Pending is not null&&Pending.Key!=keys[0])
        {Error="Use one key with optional Ctrl, Alt or Shift. Release and try again.";rejected=true;Pending=null;return null;}
        if(keys.Length==1)
        {
            Error="";
            Pending??=new Shortcut{Key=keys[0]};
            Pending.Ctrl|=ctrl;Pending.Alt|=alt;Pending.Shift|=shift;
        }
        if(released&&Pending is not null){var result=Pending;Pending=null;return result;}
        return null;
    }
}
