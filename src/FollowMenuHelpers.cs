using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static unsafe string? TravelMenuText(byte* text)
    {
        if(text==null)return null;
        var length=0;
        while(length<2048&&text[length]!=0)length++;
        if(length==2048)return null;
        return SeString.Parse(new ReadOnlySpan<byte>(text,length)).TextValue.Trim();
    }
    private static unsafe void SelectTravelChoice(AtkUnitBase* menu,int index)
    {
        if(menu==null||!menu->IsVisible||index<0)return;
        var value=new AtkValue{Type=AtkValueType.Int,Int=index};
        menu->FireCallback(1,&value,true);
    }
}
