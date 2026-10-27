using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;
internal static class LotteryMenuText
{
    internal static unsafe string Read(byte* text)
    {
        if(text==null)return "";
        var length=0;
        while(length<16384&&text[length]!=0)length++;
        return length==16384?"":SeString.Parse(new ReadOnlySpan<byte>(text,length)).TextValue.Trim();
    }
}
